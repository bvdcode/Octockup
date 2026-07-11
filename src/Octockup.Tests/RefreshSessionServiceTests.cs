// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Octockup.Server.Database;
using Octockup.Server.Models.Enums;
using Octockup.Server.Models.Options;
using Octockup.Server.Models.Results;
using Octockup.Server.Services;

namespace Octockup.Tests
{
    public class RefreshSessionServiceTests
    {
        private SqliteConnection _connection = null!;
        private SqliteDbContext _dbContext = null!;
        private RefreshSessionService _service = null!;
        private Guid _userId;

        [SetUp]
        public async Task Setup()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            await _connection.OpenAsync();
            DbContextOptions<SqliteDbContext> dbOptions =
                new DbContextOptionsBuilder<SqliteDbContext>()
                    .UseSqlite(_connection)
                    .Options;
            _dbContext = new SqliteDbContext(dbOptions);
            await _dbContext.Database.EnsureCreatedAsync();
            User user = new()
            {
                Username = "refresh-user",
                PasswordPhc = "password"
            };
            await _dbContext.Users.AddAsync(user);
            await _dbContext.SaveChangesAsync();
            _userId = user.Id;
            _service = new RefreshSessionService(
                _dbContext,
                TimeProvider.System,
                Options.Create(new RefreshSessionOptions
                {
                    Lifetime = TimeSpan.FromDays(30)
                }));
        }

        [TearDown]
        public async Task TearDown()
        {
            await _dbContext.DisposeAsync();
            await _connection.DisposeAsync();
        }

        [Test]
        public async Task CreateAsync_StoresOnlyTokenHashWithServerExpiry()
        {
            DateTime beforeIssue = DateTime.UtcNow;
            RefreshTokenIssue issue = await _service.CreateAsync(
                _userId,
                CancellationToken.None);
            RefreshSession session = await _dbContext.RefreshSessions
                .AsNoTracking()
                .SingleAsync();

            Assert.Multiple(() =>
            {
                Assert.That(session.TokenHash, Is.Not.EqualTo(issue.RefreshToken));
                Assert.That(session.TokenHash, Has.Length.EqualTo(64));
                Assert.That(session.ExpiresAt, Is.EqualTo(issue.ExpiresAt));
                Assert.That(session.ExpiresAt, Is.GreaterThan(beforeIssue.AddDays(29)));
                Assert.That(session.RevokedAt, Is.Null);
            });
        }

        [Test]
        public async Task RotateAsync_RevokesCurrentSessionAndCreatesFamilySuccessor()
        {
            RefreshTokenIssue first = await _service.CreateAsync(
                _userId,
                CancellationToken.None);
            _dbContext.ChangeTracker.Clear();

            RefreshTokenIssue? second = await _service.RotateAsync(
                first.RefreshToken,
                CancellationToken.None);
            List<RefreshSession> sessions = await _dbContext.RefreshSessions
                .AsNoTracking()
                .OrderBy(x => x.CreatedAt)
                .ToListAsync();

            Assert.Multiple(() =>
            {
                Assert.That(second, Is.Not.Null);
                Assert.That(second!.RefreshToken, Is.Not.EqualTo(first.RefreshToken));
                Assert.That(sessions, Has.Count.EqualTo(2));
                Assert.That(sessions.Select(x => x.FamilyId).Distinct().Count(), Is.EqualTo(1));
                Assert.That(sessions.Count(x => x.RevokedAt == null), Is.EqualTo(1));
                Assert.That(sessions.Single(x => x.RevokedAt != null).RevocationReason,
                    Is.EqualTo(RefreshSessionRevocationReason.Rotated));
            });
        }

        [Test]
        public async Task RotateAsync_WhenRotatedTokenIsReused_RevokesActiveFamily()
        {
            RefreshTokenIssue first = await _service.CreateAsync(
                _userId,
                CancellationToken.None);
            _dbContext.ChangeTracker.Clear();
            RefreshTokenIssue? second = await _service.RotateAsync(
                first.RefreshToken,
                CancellationToken.None);
            _dbContext.ChangeTracker.Clear();

            RefreshTokenIssue? reuseResult = await _service.RotateAsync(
                first.RefreshToken,
                CancellationToken.None);
            _dbContext.ChangeTracker.Clear();
            RefreshTokenIssue? successorResult = await _service.RotateAsync(
                second!.RefreshToken,
                CancellationToken.None);
            List<RefreshSession> sessions = await _dbContext.RefreshSessions
                .AsNoTracking()
                .ToListAsync();

            Assert.Multiple(() =>
            {
                Assert.That(reuseResult, Is.Null);
                Assert.That(successorResult, Is.Null);
                Assert.That(sessions, Has.Count.EqualTo(2));
                Assert.That(sessions, Has.None.Matches<RefreshSession>(x => x.RevokedAt == null));
                Assert.That(sessions.Single(x =>
                        x.RevocationReason == RefreshSessionRevocationReason.ReuseDetected),
                    Is.Not.Null);
            });
        }

        [Test]
        public async Task RotateAsync_WhenSessionExpired_RejectsAndMarksExpired()
        {
            RefreshTokenIssue issue = await _service.CreateAsync(
                _userId,
                CancellationToken.None);
            RefreshSession session = await _dbContext.RefreshSessions.SingleAsync();
            session.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await _dbContext.SaveChangesAsync();
            _dbContext.ChangeTracker.Clear();

            RefreshTokenIssue? result = await _service.RotateAsync(
                issue.RefreshToken,
                CancellationToken.None);
            RefreshSession expired = await _dbContext.RefreshSessions
                .AsNoTracking()
                .SingleAsync();

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.Null);
                Assert.That(expired.RevokedAt, Is.Not.Null);
                Assert.That(expired.RevocationReason,
                    Is.EqualTo(RefreshSessionRevocationReason.Expired));
            });
        }

        [Test]
        public async Task RevokeAsync_RevokesTheActiveSessionFamily()
        {
            RefreshTokenIssue first = await _service.CreateAsync(
                _userId,
                CancellationToken.None);
            _dbContext.ChangeTracker.Clear();
            RefreshTokenIssue? second = await _service.RotateAsync(
                first.RefreshToken,
                CancellationToken.None);
            _dbContext.ChangeTracker.Clear();

            bool revoked = await _service.RevokeAsync(
                second!.RefreshToken,
                CancellationToken.None);
            List<RefreshSession> sessions = await _dbContext.RefreshSessions
                .AsNoTracking()
                .ToListAsync();

            Assert.Multiple(() =>
            {
                Assert.That(revoked, Is.True);
                Assert.That(sessions, Has.None.Matches<RefreshSession>(x => x.RevokedAt == null));
                Assert.That(sessions.Single(x =>
                        x.RevocationReason == RefreshSessionRevocationReason.Logout),
                    Is.Not.Null);
            });
        }

        [Test]
        public async Task RevokeAllForPasswordChangeAsync_RevokesEveryActiveSession()
        {
            await _service.CreateAsync(_userId, CancellationToken.None);
            _dbContext.ChangeTracker.Clear();
            await _service.CreateAsync(_userId, CancellationToken.None);
            _dbContext.ChangeTracker.Clear();

            int revoked = await _service.RevokeAllForPasswordChangeAsync(
                _userId,
                CancellationToken.None);
            List<RefreshSession> sessions = await _dbContext.RefreshSessions
                .AsNoTracking()
                .ToListAsync();

            Assert.Multiple(() =>
            {
                Assert.That(revoked, Is.EqualTo(2));
                Assert.That(sessions, Has.All.Matches<RefreshSession>(x =>
                    x.RevocationReason == RefreshSessionRevocationReason.PasswordChanged));
            });
        }
    }
}
