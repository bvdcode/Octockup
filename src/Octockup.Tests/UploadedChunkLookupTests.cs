// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.Models.Enums;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Octockup.Server.Database;
using Octockup.Server.Models.Enums;
using Octockup.Server.Models.Options;
using Octockup.Server.Services;

namespace Octockup.Tests
{
    public class UploadedChunkLookupTests
    {
        private SqliteConnection _connection = null!;
        private SqliteDbContext _dbContext = null!;
        private Guid _storageId;

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
                Username = "lookup-user",
                PasswordPhc = "password"
            };
            Module storage = new()
            {
                User = user,
                Tag = "lookup-storage",
                BackupModuleId = "lookup-provider",
                Destination = ModuleDestination.Target
            };
            await _dbContext.AddRangeAsync(user, storage);
            await _dbContext.SaveChangesAsync();
            _storageId = storage.Id;

            List<UploadedHash> hashes = Enumerable.Range(0, 1_000)
                .Select(index => new UploadedHash
                {
                    ModuleId = storage.Id,
                    Hash = "stored-" + index.ToString("D6"),
                    OriginalSize = 10,
                    StoredSize = 10,
                    CompressionAlgorithm = CompressionAlgorithm.None
                })
                .ToList();
            await _dbContext.UploadedHashes.AddRangeAsync(hashes);
            await _dbContext.SaveChangesAsync();
            _dbContext.ChangeTracker.Clear();
        }

        [TearDown]
        public async Task TearDown()
        {
            await _dbContext.DisposeAsync();
            await _connection.DisposeAsync();
        }

        [Test]
        public async Task ContainsAsync_UsesBoundedFilterWithExactDatabaseConfirmation()
        {
            List<long> progress = [];
            UploadedChunkLookup lookup = CreateLookup();
            await lookup.InitializeAsync(
                _storageId,
                progress.Add,
                CancellationToken.None);

            bool existing = await lookup.ContainsAsync(
                "stored-000500",
                CancellationToken.None);
            bool missing = await lookup.ContainsAsync(
                "missing-value",
                CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(existing, Is.True);
                Assert.That(missing, Is.False);
                Assert.That(lookup.IndexedCount, Is.EqualTo(1_000));
                Assert.That(lookup.FilterByteCount, Is.EqualTo(64 * 1024));
                Assert.That(progress.Last(), Is.EqualTo(1_000));
            });
        }

        [Test]
        public async Task PendingHash_IsVisibleUntilCommittedWithoutGrowingUnboundedState()
        {
            UploadedChunkLookup lookup = CreateLookup();
            await lookup.InitializeAsync(_storageId, null, CancellationToken.None);

            bool firstMark = lookup.MarkPending("pending-hash");
            bool duplicateMark = lookup.MarkPending("pending-hash");
            bool pending = await lookup.ContainsAsync(
                "pending-hash",
                CancellationToken.None);
            lookup.CommitPending();
            bool afterCommitWithoutDatabaseRow = await lookup.ContainsAsync(
                "pending-hash",
                CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(firstMark, Is.True);
                Assert.That(duplicateMark, Is.False);
                Assert.That(pending, Is.True);
                Assert.That(afterCommitWithoutDatabaseRow, Is.False);
            });
        }

        private UploadedChunkLookup CreateLookup()
        {
            return new UploadedChunkLookup(
                _dbContext,
                Options.Create(new BackupExecutionOptions
                {
                    MaxChunkLookupMemoryBytes = 64 * 1024
                }));
        }
    }
}
