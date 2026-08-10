// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using EasyExtensions.AspNetCore.Authorization.Models.Dto;
using EasyExtensions.Crypto;
using EasyExtensions.EntityFrameworkCore.Database;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Octockup.Server.Abstractions;
using Octockup.Server.Controllers;
using Octockup.Server.Database;
using Octockup.Server.Models.Requests;
using Octockup.Server.Services;
using System.Security.Cryptography;

namespace Octockup.Tests
{
    [Category("Integration")]
    public class AuthenticationConcurrencyTests
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

        [TestCase(false)]
        [TestCase(true)]
        public async Task UpdateAccess_WhenTwoAdminsAreRemovedConcurrently_PreservesActiveAdministrator(
            bool disableUsers)
        {
            User firstAdmin = CreateAdmin("first-admin");
            User secondAdmin = CreateAdmin("second-admin");
            await using (PostgresDbContext seedContext = CreateDbContext())
            {
                await seedContext.Users.AddRangeAsync(firstAdmin, secondAdmin);
                await seedContext.SaveChangesAsync();
            }

            SaveChangesBarrierInterceptor barrier = new(2);
            await using PostgresDbContext firstContext = CreateDbContext(barrier);
            await using PostgresDbContext secondContext = CreateDbContext(barrier);
            AdminUserService firstService = new(firstContext);
            AdminUserService secondService = new(secondContext);

            Task<Exception?> firstOperation = CaptureExceptionAsync(() => firstService.UpdateAccessAsync(
                firstAdmin.Id,
                secondAdmin.Id,
                isAdmin: disableUsers,
                isDisabled: disableUsers,
                CancellationToken.None));
            Task<Exception?> secondOperation = CaptureExceptionAsync(() => secondService.UpdateAccessAsync(
                secondAdmin.Id,
                firstAdmin.Id,
                isAdmin: disableUsers,
                isDisabled: disableUsers,
                CancellationToken.None));
            Exception?[] results = await Task.WhenAll(firstOperation, secondOperation);

            AssertConcurrentConflict(results);
            await using PostgresDbContext verificationContext = CreateDbContext();
            int activeAdministratorCount = await verificationContext.Users.CountAsync(
                user => user.IsAdmin && !user.IsDisabled);
            Assert.That(activeAdministratorCount, Is.EqualTo(1));
        }

        [Test]
        public async Task DisablePasswordLoginAndProvider_WhenConcurrent_PreservesSignInMethod()
        {
            string suffix = Guid.NewGuid().ToString("N");
            User user = CreateAdmin($"linked-{suffix}");
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
                PasswordLoginEnabled = true,
            };
            await using (PostgresDbContext seedContext = CreateDbContext())
            {
                await seedContext.AddRangeAsync(user, provider, identity, settings);
                await seedContext.SaveChangesAsync();
            }

            SaveChangesBarrierInterceptor barrier = new(2);
            await using PostgresDbContext settingsContext = CreateDbContext(barrier);
            await using PostgresDbContext providerContext = CreateDbContext(barrier);
            AuthenticationSettingsService settingsService = new(settingsContext);
            OidcProviderService providerService = new(providerContext, _cipher);
            OidcProviderRequest disableProviderRequest = CreateDisableRequest(provider);

            Task<Exception?> disablePasswordLogin = CaptureExceptionAsync(() =>
                settingsService.SetPasswordLoginEnabledAsync(false, CancellationToken.None));
            Task<Exception?> disableProvider = CaptureExceptionAsync(() => providerService.UpdateAsync(
                provider.Id,
                disableProviderRequest,
                CancellationToken.None));
            Exception?[] results = await Task.WhenAll(disablePasswordLogin, disableProvider);

            AssertConcurrentConflict(results);
            await using PostgresDbContext verificationContext = CreateDbContext();
            bool passwordLoginEnabled = await verificationContext.AuthenticationSettings
                .Where(current => current.Name == AuthenticationSettings.GlobalName)
                .Select(current => current.PasswordLoginEnabled)
                .SingleAsync();
            bool providerEnabled = await verificationContext.OidcProviders
                .Where(current => current.Id == provider.Id)
                .Select(current => current.IsEnabled)
                .SingleAsync();

            Assert.Multiple(() =>
            {
                Assert.That(passwordLoginEnabled || providerEnabled, Is.True);
                Assert.That(passwordLoginEnabled, Is.Not.EqualTo(providerEnabled));
            });
        }

        [Test]
        public async Task Login_WhenTwoDifferentFirstUsersRace_CreatesOneAdministrator()
        {
            SaveChangesBarrierInterceptor barrier = new(2);
            await using PostgresDbContext firstContext = CreateDbContext(barrier);
            await using PostgresDbContext secondContext = CreateDbContext(barrier);
            RecordingAuthSessionIssuer firstSessionIssuer = new();
            RecordingAuthSessionIssuer secondSessionIssuer = new();
            AuthController firstController = CreateAuthController(firstContext, firstSessionIssuer);
            AuthController secondController = CreateAuthController(secondContext, secondSessionIssuer);

            Task<Exception?> firstLogin = CaptureExceptionAsync(() => firstController.LoginAsync(
                new() { Username = "first-bootstrap-user", Password = "secret" },
                CancellationToken.None));
            Task<Exception?> secondLogin = CaptureExceptionAsync(() => secondController.LoginAsync(
                new() { Username = "second-bootstrap-user", Password = "secret" },
                CancellationToken.None));
            Exception?[] results = await Task.WhenAll(firstLogin, secondLogin);

            AssertConcurrentConflict(results);
            await using PostgresDbContext verificationContext = CreateDbContext();
            List<User> users = await verificationContext.Users.AsNoTracking().ToListAsync();
            Assert.Multiple(() =>
            {
                Assert.That(users, Has.Count.EqualTo(1));
                Assert.That(users[0].IsAdmin, Is.True);
                Assert.That(users[0].IsDisabled, Is.False);
                Assert.That(
                    firstSessionIssuer.IssueCount + secondSessionIssuer.IssueCount,
                    Is.EqualTo(1));
            });
        }

        [Test]
        public async Task IssueSessionAndDisableUser_WhenConcurrent_PreservesDisabledSessionInvariant()
        {
            User actor = CreateAdmin("session-admin");
            User target = CreateAdmin("session-target");
            target.IsAdmin = false;
            await using (PostgresDbContext seedContext = CreateDbContext())
            {
                await seedContext.Users.AddRangeAsync(actor, target);
                await seedContext.SaveChangesAsync();
            }

            SaveChangesBarrierInterceptor barrier = new(2);
            await using PostgresDbContext issueContext = CreateDbContext(barrier);
            await using PostgresDbContext disableContext = CreateDbContext(barrier);
            AuthSessionIssuer issuer = new(
                new TestTokenProvider(),
                issueContext,
                NullLogger<AuthSessionIssuer>.Instance);
            AdminUserService adminUsers = new(disableContext);
            DefaultHttpContext httpContext = new();
            TokenPairResponseDto? issuedTokens = null;

            Task<Exception?> issueSession = CaptureExceptionAsync(async () =>
                issuedTokens = await issuer.IssueAsync(
                    target,
                    httpContext.Response,
                    CancellationToken.None));
            Task<Exception?> disableUser = CaptureExceptionAsync(() => adminUsers.UpdateAccessAsync(
                actor.Id,
                target.Id,
                isAdmin: false,
                isDisabled: true,
                CancellationToken.None));
            Exception?[] results = await Task.WhenAll(issueSession, disableUser);

            AssertConcurrentConflict(results);
            await using PostgresDbContext verificationContext = CreateDbContext();
            User savedTarget = await verificationContext.Users
                .AsNoTracking()
                .SingleAsync(x => x.Id == target.Id);
            int activeSessionCount = await verificationContext.RefreshTokens.CountAsync(
                x => x.UserId == target.Id && x.RevokedAt == null);
            bool cookieWasSet = !string.IsNullOrEmpty(httpContext.Response.Headers.SetCookie.ToString());
            if (savedTarget.IsDisabled)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(activeSessionCount, Is.Zero);
                    Assert.That(issuedTokens, Is.Null);
                    Assert.That(cookieWasSet, Is.False);
                });
            }
            else
            {
                Assert.Multiple(() =>
                {
                    Assert.That(activeSessionCount, Is.EqualTo(1));
                    Assert.That(issuedTokens, Is.Not.Null);
                    Assert.That(cookieWasSet, Is.True);
                });
            }
        }

        [Test]
        public async Task IssueSession_WhenAccessTokenCreationFails_RollsBackWithoutSettingCookie()
        {
            User user = CreateAdmin("session-rollback");
            await using (PostgresDbContext seedContext = CreateDbContext())
            {
                await seedContext.Users.AddAsync(user);
                await seedContext.SaveChangesAsync();
            }

            await using PostgresDbContext issueContext = CreateDbContext();
            AuthSessionIssuer issuer = new(
                new TestTokenProvider { FailCreation = true },
                issueContext,
                NullLogger<AuthSessionIssuer>.Instance);
            DefaultHttpContext httpContext = new();

            Assert.ThrowsAsync<InvalidOperationException>(async () => await issuer.IssueAsync(
                user,
                httpContext.Response,
                CancellationToken.None));

            await using PostgresDbContext verificationContext = CreateDbContext();
            bool sessionExists = await verificationContext.RefreshTokens.AnyAsync(
                x => x.UserId == user.Id);
            Assert.Multiple(() =>
            {
                Assert.That(sessionExists, Is.False);
                Assert.That(httpContext.Response.Headers.SetCookie.ToString(), Is.Empty);
            });
        }

        [Test]
        public async Task RotateRefreshToken_WhenTwoRequestsRace_LeavesOneActiveContinuation()
        {
            User user = CreateAdmin("refresh-race");
            string refreshToken = $"refresh-{Guid.NewGuid():N}";
            await using (PostgresDbContext seedContext = CreateDbContext())
            {
                await seedContext.Users.AddAsync(user);
                await seedContext.RefreshTokens.AddAsync(new RefreshToken
                {
                    UserId = user.Id,
                    Token = refreshToken,
                });
                await seedContext.SaveChangesAsync();
            }

            SaveChangesBarrierInterceptor barrier = new(2);
            await using PostgresDbContext firstContext = CreateDbContext(barrier);
            await using PostgresDbContext secondContext = CreateDbContext(barrier);
            AuthSessionIssuer firstIssuer = new(
                new TestTokenProvider(),
                firstContext,
                NullLogger<AuthSessionIssuer>.Instance);
            AuthSessionIssuer secondIssuer = new(
                new TestTokenProvider(),
                secondContext,
                NullLogger<AuthSessionIssuer>.Instance);
            DefaultHttpContext firstHttpContext = new();
            DefaultHttpContext secondHttpContext = new();
            TokenPairResponseDto? firstTokens = null;
            TokenPairResponseDto? secondTokens = null;

            Task<Exception?> firstRotation = CaptureExceptionAsync(async () =>
                firstTokens = await firstIssuer.RotateAsync(
                    refreshToken,
                    firstHttpContext.Response,
                    CancellationToken.None));
            Task<Exception?> secondRotation = CaptureExceptionAsync(async () =>
                secondTokens = await secondIssuer.RotateAsync(
                    refreshToken,
                    secondHttpContext.Response,
                    CancellationToken.None));
            Exception?[] results = await Task.WhenAll(firstRotation, secondRotation);

            AssertConcurrentConflict(results);
            await using PostgresDbContext verificationContext = CreateDbContext();
            List<RefreshToken> sessions = await verificationContext.RefreshTokens
                .AsNoTracking()
                .Where(x => x.UserId == user.Id)
                .ToListAsync();
            int cookieResponseCount = new[] { firstHttpContext, secondHttpContext }
                .Count(context => !string.IsNullOrEmpty(context.Response.Headers.SetCookie.ToString()));
            Assert.Multiple(() =>
            {
                Assert.That(new[] { firstTokens, secondTokens }.Count(tokens => tokens is not null), Is.EqualTo(1));
                Assert.That(sessions, Has.Count.EqualTo(2));
                Assert.That(sessions.Count(session => session.RevokedAt is null), Is.EqualTo(1));
                Assert.That(sessions.Single(session => session.Token == refreshToken).RevokedAt, Is.Not.Null);
                Assert.That(cookieResponseCount, Is.EqualTo(1));
            });
        }

        [Test]
        public async Task RotateRefreshToken_WhenAccessTokenCreationFails_RollsBackWithoutSettingCookie()
        {
            User user = CreateAdmin("refresh-rollback");
            string refreshToken = $"refresh-{Guid.NewGuid():N}";
            await using (PostgresDbContext seedContext = CreateDbContext())
            {
                await seedContext.Users.AddAsync(user);
                await seedContext.RefreshTokens.AddAsync(new RefreshToken
                {
                    UserId = user.Id,
                    Token = refreshToken,
                });
                await seedContext.SaveChangesAsync();
            }

            await using PostgresDbContext failingContext = CreateDbContext();
            AuthSessionIssuer issuer = new(
                new TestTokenProvider { FailCreation = true },
                failingContext,
                NullLogger<AuthSessionIssuer>.Instance);
            DefaultHttpContext httpContext = new();

            Assert.ThrowsAsync<InvalidOperationException>(async () => await issuer.RotateAsync(
                refreshToken,
                httpContext.Response,
                CancellationToken.None));

            await using PostgresDbContext verificationContext = CreateDbContext();
            List<RefreshToken> sessions = await verificationContext.RefreshTokens
                .AsNoTracking()
                .Where(x => x.UserId == user.Id)
                .ToListAsync();
            Assert.Multiple(() =>
            {
                Assert.That(sessions, Has.Count.EqualTo(1));
                Assert.That(sessions[0].Token, Is.EqualTo(refreshToken));
                Assert.That(sessions[0].RevokedAt, Is.Null);
                Assert.That(httpContext.Response.Headers.SetCookie.ToString(), Is.Empty);
            });
        }

        private PostgresDbContext CreateDbContext(IInterceptor? interceptor = null)
        {
            DbContextOptionsBuilder<PostgresDbContext> builder = new DbContextOptionsBuilder<PostgresDbContext>()
                .UseNpgsql(_database.ConnectionString);
            if (interceptor is not null)
            {
                builder.AddInterceptors(interceptor);
            }

            return new PostgresDbContext(builder.Options);
        }

        private static AuthController CreateAuthController(
            AppDbContext dbContext,
            IAuthSessionIssuer sessionIssuer)
        {
            AuthController controller = new(
                dbContext,
                NullLogger<ActionContext>.Instance,
                new TestPasswordHashService(),
                new AuthenticationSettingsService(dbContext),
                sessionIssuer);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext(),
            };
            return controller;
        }

        private static User CreateAdmin(string username)
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

        private static OidcProviderRequest CreateDisableRequest(OidcProvider provider)
        {
            return new OidcProviderRequest
            {
                Name = provider.Name,
                Slug = provider.Slug,
                Issuer = provider.Issuer,
                PublicBaseUrl = provider.PublicBaseUrl,
                ClientId = provider.ClientId,
                Scopes = provider.Scopes,
                IsEnabled = false,
            };
        }

        private static async Task<Exception?> CaptureExceptionAsync(Func<Task> operation)
        {
            try
            {
                await operation();
                return null;
            }
            catch (Exception exception)
            {
                return exception;
            }
        }

        private static void AssertConcurrentConflict(Exception?[] results)
        {
            Assert.That(results.Count(result => result is null), Is.EqualTo(1));
            Exception? failure = results.Single(result => result is not null);
            Assert.That(failure, Is.TypeOf<AuthApiException>());
            Assert.That(
                ((AuthApiException)failure!).StatusCode,
                Is.EqualTo(StatusCodes.Status409Conflict));
        }
    }
}
