// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Octockup.Server.Database;
using Octockup.Server.Services;

namespace Octockup.Tests
{
    [Category("Integration")]
    public class AdminUserServiceTests
    {
        private PostgresTestDatabase _database = null!;

        [OneTimeSetUp]
        public async Task CreateDatabaseAsync()
        {
            _database = await PostgresTestDatabase.CreateAsync();
        }

        [OneTimeTearDown]
        public async Task DropDatabaseAsync()
        {
            await _database.DisposeAsync();
        }

        [SetUp]
        public async Task ResetDatabaseAsync()
        {
            await using PostgresDbContext dbContext = CreateDbContext();
            await dbContext.RefreshTokens.ExecuteDeleteAsync();
            await dbContext.UserExternalIdentities.ExecuteDeleteAsync();
            await dbContext.OidcLoginStates.ExecuteDeleteAsync();
            await dbContext.OidcProviders.ExecuteDeleteAsync();
            await dbContext.AuthenticationSettings.ExecuteDeleteAsync();
            await dbContext.Users.ExecuteDeleteAsync();
        }

        [TestCase(false, false)]
        [TestCase(true, true)]
        public async Task UpdateAccess_WhenChangingLastEnabledAdmin_IsRejected(
            bool isAdmin,
            bool isDisabled)
        {
            await using PostgresDbContext dbContext = CreateDbContext();
            User admin = new()
            {
                Username = $"last-admin-{Guid.NewGuid():N}",
                PasswordPhc = "not-used",
                IsAdmin = true,
                IsDisabled = false,
            };
            await dbContext.Users.AddAsync(admin);
            await dbContext.SaveChangesAsync();
            AdminUserService service = new(dbContext);

            AuthApiException exception = Assert.ThrowsAsync<AuthApiException>(async () =>
                await service.UpdateAccessAsync(
                    admin.Id,
                    admin.Id,
                    isAdmin,
                    isDisabled,
                    CancellationToken.None))!;

            Assert.That(exception.StatusCode, Is.EqualTo(409));
        }

        [Test]
        public async Task UpdateAccess_WhenUserIsDisabled_RevokesRefreshTokens()
        {
            await using PostgresDbContext dbContext = CreateDbContext();
            User admin = new()
            {
                Username = $"admin-{Guid.NewGuid():N}",
                PasswordPhc = "not-used",
                IsAdmin = true,
                IsDisabled = false,
            };
            User user = new()
            {
                Username = $"member-{Guid.NewGuid():N}",
                PasswordPhc = "not-used",
                IsAdmin = false,
                IsDisabled = false,
            };
            await dbContext.Users.AddRangeAsync(admin, user);
            await dbContext.SaveChangesAsync();
            await dbContext.RefreshTokens.AddAsync(new()
            {
                UserId = user.Id,
                Token = Guid.NewGuid().ToString("N"),
            });
            await dbContext.SaveChangesAsync();
            AdminUserService service = new(dbContext);

            await service.UpdateAccessAsync(
                admin.Id,
                user.Id,
                isAdmin: false,
                isDisabled: true,
                CancellationToken.None);

            Assert.That(
                await dbContext.RefreshTokens.AnyAsync(x => x.UserId == user.Id && x.RevokedAt == null),
                Is.False);
        }

        [Test]
        public async Task UpdateAccess_WhenPasswordLoginIsDisabled_RejectsActivatingUnlinkedUser()
        {
            await using PostgresDbContext dbContext = CreateDbContext();
            User admin = new()
            {
                Username = $"admin-{Guid.NewGuid():N}",
                PasswordPhc = "not-used",
                IsAdmin = true,
                IsDisabled = false,
            };
            User user = new()
            {
                Username = $"disabled-{Guid.NewGuid():N}",
                PasswordPhc = "not-used",
                IsAdmin = false,
                IsDisabled = true,
            };
            await dbContext.Users.AddRangeAsync(admin, user);
            await dbContext.AuthenticationSettings.AddAsync(new AuthenticationSettings
            {
                Name = AuthenticationSettings.GlobalName,
                PasswordLoginEnabled = false,
            });
            await dbContext.SaveChangesAsync();
            AdminUserService service = new(dbContext);

            AuthApiException exception = Assert.ThrowsAsync<AuthApiException>(async () =>
                await service.UpdateAccessAsync(
                    admin.Id,
                    user.Id,
                    isAdmin: false,
                    isDisabled: false,
                    CancellationToken.None))!;

            Assert.That(exception.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
        }

        private PostgresDbContext CreateDbContext()
        {
            DbContextOptions<PostgresDbContext> options = new DbContextOptionsBuilder<PostgresDbContext>()
                .UseNpgsql(_database.ConnectionString)
                .Options;
            return new PostgresDbContext(options);
        }
    }
}
