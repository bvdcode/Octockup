// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using EasyExtensions.Crypto;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Octockup.Server.Database;
using Octockup.Server.Models.Dto;
using Octockup.Server.Models.Requests;
using Octockup.Server.Services;
using System.Security.Cryptography;

namespace Octockup.Tests
{
    [Category("Integration")]
    public class OidcProviderServiceTests
    {
        private PostgresTestDatabase _database = null!;
        private AesGcmStreamCipher _cipher = null!;

        [OneTimeSetUp]
        public async Task CreateDatabaseAsync()
        {
            _database = await PostgresTestDatabase.CreateAsync();
            _cipher = new AesGcmStreamCipher(RandomNumberGenerator.GetBytes(32));
        }

        [OneTimeTearDown]
        public async Task DropDatabaseAsync()
        {
            await _database.DisposeAsync();
            _cipher.Dispose();
        }

        [Test]
        public async Task Update_WhenSecretIsBlank_PreservesEncryptedSecretUntilExplicitlyCleared()
        {
            await using PostgresDbContext dbContext = CreateDbContext();
            OidcProviderService service = new(dbContext, _cipher);
            OidcProviderRequest request = CreateRequest();
            request.ClientSecret = "client-secret";
            OidcProviderDto created = await service.CreateAsync(request, CancellationToken.None);
            OidcProvider provider = await dbContext.OidcProviders.SingleAsync(x => x.Id == created.Id);
            string? encryptedSecret = provider.ClientSecretEncrypted;

            Assert.Multiple(() =>
            {
                Assert.That(created.HasClientSecret, Is.True);
                Assert.That(encryptedSecret, Is.Not.Null.And.Not.EqualTo(request.ClientSecret));
                Assert.That(service.DecryptClientSecret(provider), Is.EqualTo("client-secret"));
            });

            OidcProviderRequest blankUpdate = CreateRequest(provider);
            blankUpdate.ClientSecret = "   ";
            OidcProviderDto preserved = await service.UpdateAsync(
                provider.Id,
                blankUpdate,
                CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(preserved.HasClientSecret, Is.True);
                Assert.That(provider.ClientSecretEncrypted, Is.EqualTo(encryptedSecret));
            });

            OidcProviderRequest clearUpdate = CreateRequest(provider);
            clearUpdate.ClearClientSecret = true;
            OidcProviderDto cleared = await service.UpdateAsync(
                provider.Id,
                clearUpdate,
                CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(cleared.HasClientSecret, Is.False);
                Assert.That(provider.ClientSecretEncrypted, Is.Null);
            });
        }

        [Test]
        public async Task Update_WhenIdentityIsLinked_RejectsIssuerOrClientIdChange()
        {
            await using PostgresDbContext dbContext = CreateDbContext();
            OidcProviderService service = new(dbContext, _cipher);
            OidcProviderDto created = await service.CreateAsync(CreateRequest(), CancellationToken.None);
            OidcProvider provider = await dbContext.OidcProviders.SingleAsync(x => x.Id == created.Id);
            User user = new()
            {
                Username = $"oidc-{Guid.NewGuid():N}",
                PasswordPhc = "not-used",
                IsAdmin = true,
                IsDisabled = true,
            };
            UserExternalIdentity identity = new()
            {
                User = user,
                Provider = provider,
                Issuer = provider.Issuer,
                Subject = "subject",
            };
            await dbContext.AddRangeAsync(user, identity);
            await dbContext.SaveChangesAsync();
            OidcProviderRequest update = CreateRequest(provider);
            update.Issuer = "https://another-issuer.example";

            AuthApiException exception = Assert.ThrowsAsync<AuthApiException>(async () =>
                await service.UpdateAsync(provider.Id, update, CancellationToken.None))!;

            Assert.That(exception.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
        }

        [Test]
        public async Task Delete_RejectsLinkedIdentityAndRemovesTransientStatesExplicitly()
        {
            await using PostgresDbContext dbContext = CreateDbContext();
            OidcProviderService service = new(dbContext, _cipher);
            OidcProviderDto created = await service.CreateAsync(CreateRequest(), CancellationToken.None);
            OidcProvider provider = await dbContext.OidcProviders.SingleAsync(x => x.Id == created.Id);
            User user = new()
            {
                Username = $"delete-{Guid.NewGuid():N}",
                PasswordPhc = "not-used",
                IsAdmin = true,
                IsDisabled = true,
            };
            UserExternalIdentity identity = new()
            {
                User = user,
                Provider = provider,
                Issuer = provider.Issuer,
                Subject = "subject",
            };
            OidcLoginState loginState = new()
            {
                Provider = provider,
                StateHash = RandomNumberGenerator.GetHexString(32).ToLowerInvariant(),
                CodeVerifierEncrypted = "encrypted",
                NonceEncrypted = "encrypted",
                ReturnUrl = "/login",
                ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            };
            await dbContext.AddRangeAsync(user, identity, loginState);
            await dbContext.SaveChangesAsync();

            AuthApiException exception = Assert.ThrowsAsync<AuthApiException>(async () =>
                await service.DeleteAsync(provider.Id, CancellationToken.None))!;
            Assert.That(exception.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));

            dbContext.UserExternalIdentities.Remove(identity);
            await dbContext.SaveChangesAsync();
            await service.DeleteAsync(provider.Id, CancellationToken.None);
            bool providerExists = await dbContext.OidcProviders.AnyAsync(x => x.Id == provider.Id);
            bool loginStateExists = await dbContext.OidcLoginStates.AnyAsync(x => x.Id == loginState.Id);

            Assert.Multiple(() =>
            {
                Assert.That(providerExists, Is.False);
                Assert.That(loginStateExists, Is.False);
            });
        }

        [Test]
        public async Task Update_WhenPasswordLoginIsDisabled_RejectsStrandingActiveUser()
        {
            await using PostgresDbContext dbContext = CreateDbContext();
            OidcProviderService service = new(dbContext, _cipher);
            OidcProviderDto created = await service.CreateAsync(CreateRequest(), CancellationToken.None);
            OidcProvider provider = await dbContext.OidcProviders.SingleAsync(x => x.Id == created.Id);
            User user = new()
            {
                Username = $"disable-provider-{Guid.NewGuid():N}",
                PasswordPhc = "not-used",
                IsAdmin = true,
                IsDisabled = false,
            };
            UserExternalIdentity identity = new()
            {
                User = user,
                Provider = provider,
                Issuer = provider.Issuer,
                Subject = "subject",
            };
            AuthenticationSettings settings = new()
            {
                Name = AuthenticationSettings.GlobalName,
                PasswordLoginEnabled = false,
            };
            await dbContext.AddRangeAsync(user, identity, settings);
            await dbContext.SaveChangesAsync();
            OidcProviderRequest update = CreateRequest(provider);
            update.IsEnabled = false;

            AuthApiException exception = Assert.ThrowsAsync<AuthApiException>(async () =>
                await service.UpdateAsync(provider.Id, update, CancellationToken.None))!;

            Assert.Multiple(() =>
            {
                Assert.That(exception.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
                Assert.That(provider.IsEnabled, Is.True);
            });
        }

        private PostgresDbContext CreateDbContext()
        {
            DbContextOptions<PostgresDbContext> options = new DbContextOptionsBuilder<PostgresDbContext>()
                .UseNpgsql(_database.ConnectionString)
                .Options;
            return new PostgresDbContext(options);
        }

        private static OidcProviderRequest CreateRequest()
        {
            string suffix = Guid.NewGuid().ToString("N");
            return new OidcProviderRequest
            {
                Name = "Provider " + suffix,
                Slug = "provider-" + suffix,
                Issuer = "https://issuer-" + suffix + ".example",
                PublicBaseUrl = "https://octockup.example",
                ClientId = "client-" + suffix,
                Scopes = ["openid", "profile", "email"],
                IsEnabled = true,
            };
        }

        private static OidcProviderRequest CreateRequest(OidcProvider provider)
        {
            return new OidcProviderRequest
            {
                Name = provider.Name,
                Slug = provider.Slug,
                Issuer = provider.Issuer,
                PublicBaseUrl = provider.PublicBaseUrl,
                ClientId = provider.ClientId,
                Scopes = provider.Scopes,
                IsEnabled = provider.IsEnabled,
            };
        }
    }
}
