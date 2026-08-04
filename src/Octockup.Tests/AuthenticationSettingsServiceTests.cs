// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore;
using Octockup.Server.Database;
using Octockup.Server.Services;

namespace Octockup.Tests
{
    [Category("Integration")]
    public class AuthenticationSettingsServiceTests
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

        [Test]
        public async Task DisablePasswordLogin_WhenActiveUserHasNoExternalIdentity_IsRejected()
        {
            await using PostgresDbContext dbContext = CreateDbContext();
            User user = CreateUser("unlinked");
            await dbContext.Users.AddAsync(user);
            await dbContext.SaveChangesAsync();
            AuthenticationSettingsService service = new(dbContext);

            AuthApiException exception = Assert.ThrowsAsync<AuthApiException>(async () =>
                await service.SetPasswordLoginEnabledAsync(false, CancellationToken.None))!;

            Assert.That(exception.StatusCode, Is.EqualTo(409));
            Assert.That(await service.IsPasswordLoginEnabledAsync(CancellationToken.None), Is.True);
        }

        [Test]
        public async Task DisablePasswordLogin_WhenEveryActiveUserHasEnabledExternalIdentity_Succeeds()
        {
            await using PostgresDbContext dbContext = CreateDbContext();
            string suffix = Guid.NewGuid().ToString("N");
            User user = CreateUser($"linked-{suffix}");
            OidcProvider provider = CreateProvider(suffix);
            UserExternalIdentity identity = new()
            {
                User = user,
                Provider = provider,
                Issuer = provider.Issuer,
                Subject = $"subject-{suffix}",
            };
            await dbContext.AddRangeAsync(user, provider, identity);
            await dbContext.SaveChangesAsync();
            AuthenticationSettingsService service = new(dbContext);

            await service.SetPasswordLoginEnabledAsync(false, CancellationToken.None);

            Assert.That(await service.IsPasswordLoginEnabledAsync(CancellationToken.None), Is.False);
        }

        [Test]
        public async Task DisablePasswordLogin_WhenIdentityIssuerDoesNotMatchProvider_IsRejected()
        {
            await using PostgresDbContext dbContext = CreateDbContext();
            string suffix = Guid.NewGuid().ToString("N");
            User user = CreateUser($"mismatched-{suffix}");
            OidcProvider provider = CreateProvider(suffix);
            UserExternalIdentity identity = new()
            {
                User = user,
                Provider = provider,
                Issuer = "https://old-issuer.example",
                Subject = $"subject-{suffix}",
            };
            await dbContext.AddRangeAsync(user, provider, identity);
            await dbContext.SaveChangesAsync();
            AuthenticationSettingsService service = new(dbContext);

            AuthApiException exception = Assert.ThrowsAsync<AuthApiException>(async () =>
                await service.SetPasswordLoginEnabledAsync(false, CancellationToken.None))!;

            Assert.That(exception.StatusCode, Is.EqualTo(409));
            Assert.That(await service.IsPasswordLoginEnabledAsync(CancellationToken.None), Is.True);
        }

        [Test]
        public async Task EnsureCanUnlink_WhenPasswordLoginDisabledAndIdentityIsLast_IsRejected()
        {
            await using PostgresDbContext dbContext = CreateDbContext();
            string suffix = Guid.NewGuid().ToString("N");
            User user = CreateUser($"unlink-{suffix}");
            OidcProvider provider = CreateProvider(suffix);
            UserExternalIdentity identity = new()
            {
                User = user,
                Provider = provider,
                Issuer = provider.Issuer,
                Subject = $"subject-{suffix}",
            };
            AuthenticationSettings settings = new()
            {
                Name = AuthenticationSettings.GlobalName,
                PasswordLoginEnabled = false,
            };
            await dbContext.AddRangeAsync(user, provider, identity, settings);
            await dbContext.SaveChangesAsync();
            AuthenticationSettingsService service = new(dbContext);

            AuthApiException exception = Assert.ThrowsAsync<AuthApiException>(async () =>
                await service.EnsureCanUnlinkAsync(user.Id, identity.Id, CancellationToken.None))!;

            Assert.That(exception.StatusCode, Is.EqualTo(409));
        }

        [Test]
        public async Task DisablePasswordLogin_WhenDisabledUserHasNoExternalIdentity_Succeeds()
        {
            await using PostgresDbContext dbContext = CreateDbContext();
            string suffix = Guid.NewGuid().ToString("N");
            User activeUser = CreateUser($"active-{suffix}");
            User disabledUser = CreateUser($"disabled-{suffix}");
            disabledUser.IsDisabled = true;
            OidcProvider provider = CreateProvider(suffix);
            UserExternalIdentity identity = new()
            {
                User = activeUser,
                Provider = provider,
                Issuer = provider.Issuer,
                Subject = $"subject-{suffix}",
            };
            await dbContext.AddRangeAsync(activeUser, disabledUser, provider, identity);
            await dbContext.SaveChangesAsync();
            AuthenticationSettingsService service = new(dbContext);

            await service.SetPasswordLoginEnabledAsync(false, CancellationToken.None);

            Assert.That(await service.IsPasswordLoginEnabledAsync(CancellationToken.None), Is.False);
        }

        private PostgresDbContext CreateDbContext()
        {
            DbContextOptions<PostgresDbContext> options = new DbContextOptionsBuilder<PostgresDbContext>()
                .UseNpgsql(_database.ConnectionString)
                .Options;
            return new PostgresDbContext(options);
        }

        private static User CreateUser(string username)
        {
            return new User
            {
                Username = $"{username}-{Guid.NewGuid():N}",
                PasswordPhc = "not-used",
                IsAdmin = true,
                IsDisabled = false,
            };
        }

        private static OidcProvider CreateProvider(string suffix)
        {
            return new OidcProvider
            {
                Name = $"Provider {suffix}",
                Slug = $"provider-{suffix}",
                Issuer = $"https://issuer-{suffix}.example",
                PublicBaseUrl = "https://octockup.example",
                ClientId = $"client-{suffix}",
                Scopes = ["openid", "profile", "email"],
                IsEnabled = true,
            };
        }
    }
}
