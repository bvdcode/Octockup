// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using EasyExtensions.Crypto;
using EasyExtensions.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using Octockup.Server.Database;
using Octockup.Server.Models.Requests;
using Octockup.Server.Services;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Octockup.Tests
{
    [Category("Integration")]
    public class OidcAuthenticationFlowTests
    {
        private PostgresTestDatabase _database = null!;
        private AesGcmStreamCipher _cipher = null!;
        private RSA _rsa = null!;

        [OneTimeSetUp]
        public async Task CreateDatabaseAsync()
        {
            _database = await PostgresTestDatabase.CreateAsync();
            _cipher = new AesGcmStreamCipher(RandomNumberGenerator.GetBytes(32));
            _rsa = RSA.Create(2048);
        }

        [OneTimeTearDown]
        public async Task DropDatabaseAsync()
        {
            await _database.DisposeAsync();
            _cipher.Dispose();
            _rsa.Dispose();
        }

        [Test]
        public async Task BeginSignIn_StoresHashedStateAndEncryptedPkceMaterial()
        {
            await using PostgresDbContext dbContext = CreateDbContext();
            using OidcTestHttpMessageHandler handler = new(_rsa);
            using HttpClient httpClient = new(handler);
            RecordingAuthSessionIssuer sessionIssuer = new();
            OidcAuthenticationService service = CreateService(
                dbContext,
                httpClient,
                sessionIssuer);
            OidcProvider provider = await CreateProviderAsync(dbContext);
            DefaultHttpContext beginContext = new();

            string authorizationUrl = await service.BeginSignInAsync(
                provider.Slug,
                "/login",
                beginContext.Response,
                CancellationToken.None);

            Dictionary<string, Microsoft.Extensions.Primitives.StringValues> query = QueryHelpers.ParseQuery(
                new Uri(authorizationUrl).Query);
            string state = query["state"].ToString();
            string nonce = query["nonce"].ToString();
            OidcLoginState loginState = await dbContext.OidcLoginStates
                .SingleAsync(x => x.ProviderId == provider.Id);
            string codeVerifier = _cipher.DecryptString(
                Convert.FromBase64String(loginState.CodeVerifierEncrypted));
            string expectedChallenge = WebEncoders.Base64UrlEncode(
                SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));
            string correlationCookie = beginContext.Response.Headers.SetCookie.ToString().ToLowerInvariant();

            Assert.Multiple(() =>
            {
                Assert.That(
                    loginState.StateHash,
                    Is.EqualTo(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(state)))));
                Assert.That(loginState.StateHash, Is.Not.EqualTo(state));
                Assert.That(loginState.NonceEncrypted, Is.Not.EqualTo(nonce));
                Assert.That(query["code_challenge"].ToString(), Is.EqualTo(expectedChallenge));
                Assert.That(query["code_challenge_method"].ToString(), Is.EqualTo("S256"));
                Assert.That(correlationCookie, Does.Contain("httponly"));
                Assert.That(correlationCookie, Does.Contain("secure"));
                Assert.That(correlationCookie, Does.Contain("samesite=lax"));
                Assert.That(correlationCookie, Does.Contain("path=/api/v1/auth/oidc/callback"));
            });
        }

        [Test]
        public async Task CompleteSignIn_WhenSubjectIsNotLinked_DoesNotCreateUser()
        {
            await using PostgresDbContext dbContext = CreateDbContext();
            using OidcTestHttpMessageHandler handler = new(_rsa);
            using HttpClient httpClient = new(handler);
            RecordingAuthSessionIssuer sessionIssuer = new();
            OidcAuthenticationService service = CreateService(
                dbContext,
                httpClient,
                sessionIssuer);
            OidcProvider provider = await CreateProviderAsync(dbContext);
            int userCount = await dbContext.Users.CountAsync();
            DefaultHttpContext beginContext = new();
            string authorizationUrl = await service.BeginSignInAsync(
                provider.Slug,
                "/login",
                beginContext.Response,
                CancellationToken.None);
            Dictionary<string, Microsoft.Extensions.Primitives.StringValues> query = QueryHelpers.ParseQuery(
                new Uri(authorizationUrl).Query);
            handler.IdToken = CreateIdToken(
                provider,
                query["nonce"].ToString(),
                "unknown-subject");
            DefaultHttpContext callbackContext = CreateCallbackContext(beginContext, useValidValue: true);

            OidcCallbackException exception = Assert.ThrowsAsync<OidcCallbackException>(async () =>
                await service.CompleteCallbackAsync(
                    query["state"].ToString(),
                    "authorization-code",
                    callbackContext.Request,
                    callbackContext.Response,
                    CancellationToken.None))!;
            int currentUserCount = await dbContext.Users.CountAsync();

            Assert.Multiple(() =>
            {
                Assert.That(exception.ReturnUrl, Is.EqualTo("/login?oidc=error"));
                Assert.That(exception.InnerException, Is.InstanceOf<AuthApiException>());
                Assert.That(
                    ((AuthApiException)exception.InnerException!).StatusCode,
                    Is.EqualTo(StatusCodes.Status403Forbidden));
                Assert.That(currentUserCount, Is.EqualTo(userCount));
                Assert.That(sessionIssuer.IssuedUserId, Is.Null);
            });
        }

        [Test]
        public async Task CompleteLink_WhenTokenValidationFails_ReturnsToSettingsError()
        {
            await using PostgresDbContext dbContext = CreateDbContext();
            using OidcTestHttpMessageHandler handler = new(_rsa)
            {
                IdToken = "not-a-jwt",
            };
            using HttpClient httpClient = new(handler);
            RecordingAuthSessionIssuer sessionIssuer = new();
            OidcAuthenticationService service = CreateService(dbContext, httpClient, sessionIssuer);
            OidcProvider provider = await CreateProviderAsync(dbContext);
            User user = new()
            {
                Username = $"invalid-token-{Guid.NewGuid():N}",
                PasswordPhc = "not-used",
                IsAdmin = true,
                IsDisabled = false,
            };
            await dbContext.Users.AddAsync(user);
            await dbContext.SaveChangesAsync();
            DefaultHttpContext beginContext = new();
            string authorizationUrl = await service.BeginLinkAsync(
                user.Id,
                provider.Slug,
                "/settings",
                beginContext.Response,
                CancellationToken.None);
            Dictionary<string, Microsoft.Extensions.Primitives.StringValues> query = QueryHelpers.ParseQuery(
                new Uri(authorizationUrl).Query);
            DefaultHttpContext callbackContext = CreateCallbackContext(beginContext, useValidValue: true);

            OidcCallbackException exception = Assert.ThrowsAsync<OidcCallbackException>(async () =>
                await service.CompleteCallbackAsync(
                    query["state"].ToString(),
                    "authorization-code",
                    callbackContext.Request,
                    callbackContext.Response,
                    CancellationToken.None))!;

            Assert.That(exception.ReturnUrl, Is.EqualTo("/settings?oidc=error"));
            Assert.That(await dbContext.OidcLoginStates.AnyAsync(x => x.ProviderId == provider.Id), Is.False);
            Assert.That(sessionIssuer.IssuedUserId, Is.Null);
        }

        [Test]
        public async Task CompleteLink_RenewsSessionAndThenSignInUsesTheExistingUser()
        {
            await using PostgresDbContext dbContext = CreateDbContext();
            using OidcTestHttpMessageHandler handler = new(_rsa);
            using HttpClient httpClient = new(handler);
            RecordingAuthSessionIssuer sessionIssuer = new();
            OidcAuthenticationService service = CreateService(
                dbContext,
                httpClient,
                sessionIssuer);
            OidcProvider provider = await CreateProviderAsync(dbContext);
            User user = new()
            {
                Username = $"flow-{Guid.NewGuid():N}",
                PasswordPhc = "not-used",
                IsAdmin = true,
                IsDisabled = false,
            };
            await dbContext.Users.AddAsync(user);
            await dbContext.SaveChangesAsync();

            DefaultHttpContext linkBeginContext = new();
            string linkAuthorizationUrl = await service.BeginLinkAsync(
                user.Id,
                provider.Slug,
                "/settings",
                linkBeginContext.Response,
                CancellationToken.None);
            Dictionary<string, Microsoft.Extensions.Primitives.StringValues> linkQuery = QueryHelpers.ParseQuery(
                new Uri(linkAuthorizationUrl).Query);
            handler.IdToken = CreateIdToken(provider, linkQuery["nonce"].ToString(), "linked-subject");
            DefaultHttpContext linkContext = CreateCallbackContext(linkBeginContext, useValidValue: true);
            string linkReturnUrl = await service.CompleteCallbackAsync(
                linkQuery["state"].ToString(),
                "link-code",
                linkContext.Request,
                linkContext.Response,
                CancellationToken.None);
            bool identityExists = await dbContext.UserExternalIdentities.AnyAsync(
                x => x.UserId == user.Id
                    && x.ProviderId == provider.Id
                    && x.Subject == "linked-subject");

            Assert.Multiple(() =>
            {
                Assert.That(linkReturnUrl, Is.EqualTo("/settings?oidc=linked"));
                Assert.That(sessionIssuer.IssuedUserId, Is.EqualTo(user.Id));
                Assert.That(sessionIssuer.IssueCount, Is.EqualTo(1));
                Assert.That(identityExists, Is.True);
            });

            DefaultHttpContext signInBeginContext = new();
            string signInAuthorizationUrl = await service.BeginSignInAsync(
                provider.Slug,
                "/login",
                signInBeginContext.Response,
                CancellationToken.None);
            Dictionary<string, Microsoft.Extensions.Primitives.StringValues> signInQuery = QueryHelpers.ParseQuery(
                new Uri(signInAuthorizationUrl).Query);
            handler.IdToken = CreateIdToken(provider, signInQuery["nonce"].ToString(), "linked-subject");
            DefaultHttpContext signInContext = CreateCallbackContext(signInBeginContext, useValidValue: true);
            string signInReturnUrl = await service.CompleteCallbackAsync(
                signInQuery["state"].ToString(),
                "sign-in-code",
                signInContext.Request,
                signInContext.Response,
                CancellationToken.None);
            string deletedCorrelationCookie = signInContext.Response.Headers.SetCookie
                .ToString()
                .ToLowerInvariant();

            Assert.Multiple(() =>
            {
                Assert.That(signInReturnUrl, Is.EqualTo("/login?oidc=success"));
                Assert.That(sessionIssuer.IssuedUserId, Is.EqualTo(user.Id));
                Assert.That(sessionIssuer.IssueCount, Is.EqualTo(2));
                Assert.That(
                    deletedCorrelationCookie,
                    Does.Contain("expires=thu, 01 jan 1970 00:00:00 gmt"));
            });

            AuthApiException replayException = Assert.ThrowsAsync<AuthApiException>(async () =>
                await service.CompleteCallbackAsync(
                    signInQuery["state"].ToString(),
                    "sign-in-code",
                    signInContext.Request,
                    signInContext.Response,
                    CancellationToken.None))!;

            Assert.Multiple(() =>
            {
                Assert.That(replayException.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
                Assert.That(sessionIssuer.IssueCount, Is.EqualTo(2));
            });
        }

        [Test]
        public async Task CompleteLinkAndProviderIdentityChange_WhenConcurrent_PreservesUsableConfiguration()
        {
            OidcProvider provider;
            User user = new()
            {
                Username = $"provider-race-{Guid.NewGuid():N}",
                PasswordPhc = "not-used",
                IsAdmin = true,
                IsDisabled = false,
            };
            string authorizationUrl;
            DefaultHttpContext beginContext = new();
            using OidcTestHttpMessageHandler handler = new(_rsa);
            using HttpClient httpClient = new(handler);
            await using (PostgresDbContext setupContext = CreateDbContext())
            {
                RecordingAuthSessionIssuer setupSessionIssuer = new();
                OidcAuthenticationService setupService = CreateService(
                    setupContext,
                    httpClient,
                    setupSessionIssuer);
                provider = await CreateProviderAsync(setupContext);
                await setupContext.Users.AddAsync(user);
                await setupContext.SaveChangesAsync();
                authorizationUrl = await setupService.BeginLinkAsync(
                    user.Id,
                    provider.Slug,
                    "/settings",
                    beginContext.Response,
                    CancellationToken.None);
            }

            Dictionary<string, Microsoft.Extensions.Primitives.StringValues> query = QueryHelpers.ParseQuery(
                new Uri(authorizationUrl).Query);
            handler.IdToken = CreateIdToken(
                provider,
                query["nonce"].ToString(),
                "provider-race-subject");
            DefaultHttpContext callbackContext = CreateCallbackContext(beginContext, useValidValue: true);
            SaveChangesBarrierInterceptor barrier = new(
                2,
                typeof(UserExternalIdentity),
                typeof(OidcProvider));
            await using PostgresDbContext linkContext = CreateDbContext(barrier);
            await using PostgresDbContext providerContext = CreateDbContext(barrier);
            OidcAuthenticationService linkService = CreateService(
                linkContext,
                httpClient,
                new RecordingAuthSessionIssuer());
            OidcProviderService providerService = new(providerContext, _cipher);
            string replacementIssuer = $"https://replacement-{Guid.NewGuid():N}.example";
            OidcProviderRequest updateRequest = new()
            {
                Name = provider.Name,
                Slug = provider.Slug,
                Issuer = replacementIssuer,
                PublicBaseUrl = provider.PublicBaseUrl,
                ClientId = "replacement-client",
                Scopes = provider.Scopes,
                IsEnabled = true,
            };

            Task<Exception?> completeLink = CaptureExceptionAsync(() => linkService.CompleteCallbackAsync(
                query["state"].ToString(),
                "authorization-code",
                callbackContext.Request,
                callbackContext.Response,
                CancellationToken.None));
            Task<Exception?> updateProvider = CaptureExceptionAsync(() => providerService.UpdateAsync(
                provider.Id,
                updateRequest,
                CancellationToken.None));
            Exception?[] results = await Task.WhenAll(completeLink, updateProvider);

            Assert.That(results.Count(result => result is null), Is.EqualTo(1));
            Exception failure = results.Single(result => result is not null)!;
            AuthApiException? conflict = failure as AuthApiException
                ?? (failure as OidcCallbackException)?.InnerException as AuthApiException;
            Assert.That(conflict?.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));

            await using PostgresDbContext verificationContext = CreateDbContext();
            OidcProvider savedProvider = await verificationContext.OidcProviders
                .AsNoTracking()
                .SingleAsync(x => x.Id == provider.Id);
            UserExternalIdentity? savedIdentity = await verificationContext.UserExternalIdentities
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.ProviderId == provider.Id);
            if (savedIdentity is null)
            {
                Assert.That(savedProvider.Issuer, Is.EqualTo(replacementIssuer));
            }
            else
            {
                Assert.Multiple(() =>
                {
                    Assert.That(savedIdentity.Issuer, Is.EqualTo(savedProvider.Issuer));
                    Assert.That(savedProvider.Issuer, Is.EqualTo(provider.Issuer));
                    Assert.That(savedProvider.ClientId, Is.EqualTo(provider.ClientId));
                });
            }
        }

        [Test]
        public async Task CompleteSignIn_WhenNonceDoesNotMatch_ConsumesStateWithoutIssuingSession()
        {
            await using PostgresDbContext dbContext = CreateDbContext();
            using OidcTestHttpMessageHandler handler = new(_rsa);
            using HttpClient httpClient = new(handler);
            RecordingAuthSessionIssuer sessionIssuer = new();
            OidcAuthenticationService service = CreateService(dbContext, httpClient, sessionIssuer);
            OidcProvider provider = await CreateProviderAsync(dbContext);
            DefaultHttpContext beginContext = new();
            string authorizationUrl = await service.BeginSignInAsync(
                provider.Slug,
                "/login",
                beginContext.Response,
                CancellationToken.None);
            Dictionary<string, Microsoft.Extensions.Primitives.StringValues> query = QueryHelpers.ParseQuery(
                new Uri(authorizationUrl).Query);
            handler.IdToken = CreateIdToken(provider, "wrong-nonce", "linked-subject");
            DefaultHttpContext callbackContext = CreateCallbackContext(beginContext, useValidValue: true);

            OidcCallbackException exception = Assert.ThrowsAsync<OidcCallbackException>(async () =>
                await service.CompleteCallbackAsync(
                    query["state"].ToString(),
                    "authorization-code",
                    callbackContext.Request,
                    callbackContext.Response,
                    CancellationToken.None))!;

            Assert.Multiple(() =>
            {
                Assert.That(exception.ReturnUrl, Is.EqualTo("/login?oidc=error"));
                Assert.That(exception.InnerException, Is.InstanceOf<AuthApiException>());
                Assert.That(sessionIssuer.IssueCount, Is.Zero);
            });
            Assert.That(
                await dbContext.OidcLoginStates.AnyAsync(x => x.ProviderId == provider.Id),
                Is.False);
        }

        [Test]
        public async Task CompleteSignIn_WhenLinkedUserIsDisabled_DoesNotIssueSession()
        {
            await using PostgresDbContext dbContext = CreateDbContext();
            using OidcTestHttpMessageHandler handler = new(_rsa);
            using HttpClient httpClient = new(handler);
            RecordingAuthSessionIssuer sessionIssuer = new();
            OidcAuthenticationService service = CreateService(dbContext, httpClient, sessionIssuer);
            OidcProvider provider = await CreateProviderAsync(dbContext);
            User user = new()
            {
                Username = $"disabled-{Guid.NewGuid():N}",
                PasswordPhc = "not-used",
                IsAdmin = false,
                IsDisabled = true,
            };
            UserExternalIdentity identity = new()
            {
                User = user,
                Provider = provider,
                Issuer = provider.Issuer,
                Subject = "disabled-subject",
            };
            await dbContext.AddRangeAsync(user, identity);
            await dbContext.SaveChangesAsync();
            DefaultHttpContext beginContext = new();
            string authorizationUrl = await service.BeginSignInAsync(
                provider.Slug,
                "/login",
                beginContext.Response,
                CancellationToken.None);
            Dictionary<string, Microsoft.Extensions.Primitives.StringValues> query = QueryHelpers.ParseQuery(
                new Uri(authorizationUrl).Query);
            handler.IdToken = CreateIdToken(
                provider,
                query["nonce"].ToString(),
                identity.Subject);
            DefaultHttpContext callbackContext = CreateCallbackContext(beginContext, useValidValue: true);

            OidcCallbackException exception = Assert.ThrowsAsync<OidcCallbackException>(async () =>
                await service.CompleteCallbackAsync(
                    query["state"].ToString(),
                    "authorization-code",
                    callbackContext.Request,
                    callbackContext.Response,
                    CancellationToken.None))!;

            Assert.Multiple(() =>
            {
                Assert.That(exception.InnerException, Is.InstanceOf<AuthApiException>());
                Assert.That(
                    ((AuthApiException)exception.InnerException!).StatusCode,
                    Is.EqualTo(StatusCodes.Status403Forbidden));
                Assert.That(sessionIssuer.IssueCount, Is.Zero);
            });
        }

        [Test]
        public async Task CompleteLink_WhenSubjectBelongsToAnotherUser_RejectsCollision()
        {
            await using PostgresDbContext dbContext = CreateDbContext();
            using OidcTestHttpMessageHandler handler = new(_rsa);
            using HttpClient httpClient = new(handler);
            RecordingAuthSessionIssuer sessionIssuer = new();
            OidcAuthenticationService service = CreateService(dbContext, httpClient, sessionIssuer);
            OidcProvider provider = await CreateProviderAsync(dbContext);
            User owner = new()
            {
                Username = $"owner-{Guid.NewGuid():N}",
                PasswordPhc = "not-used",
                IsAdmin = false,
                IsDisabled = false,
            };
            User linkingUser = new()
            {
                Username = $"linking-{Guid.NewGuid():N}",
                PasswordPhc = "not-used",
                IsAdmin = false,
                IsDisabled = false,
            };
            UserExternalIdentity identity = new()
            {
                User = owner,
                Provider = provider,
                Issuer = provider.Issuer,
                Subject = "owned-subject",
            };
            await dbContext.AddRangeAsync(owner, linkingUser, identity);
            await dbContext.SaveChangesAsync();
            DefaultHttpContext beginContext = new();
            string authorizationUrl = await service.BeginLinkAsync(
                linkingUser.Id,
                provider.Slug,
                "/settings",
                beginContext.Response,
                CancellationToken.None);
            Dictionary<string, Microsoft.Extensions.Primitives.StringValues> query = QueryHelpers.ParseQuery(
                new Uri(authorizationUrl).Query);
            handler.IdToken = CreateIdToken(
                provider,
                query["nonce"].ToString(),
                identity.Subject);
            DefaultHttpContext callbackContext = CreateCallbackContext(beginContext, useValidValue: true);

            OidcCallbackException exception = Assert.ThrowsAsync<OidcCallbackException>(async () =>
                await service.CompleteCallbackAsync(
                    query["state"].ToString(),
                    "authorization-code",
                    callbackContext.Request,
                    callbackContext.Response,
                    CancellationToken.None))!;

            Assert.Multiple(() =>
            {
                Assert.That(exception.ReturnUrl, Is.EqualTo("/settings?oidc=error"));
                Assert.That(exception.InnerException, Is.InstanceOf<AuthApiException>());
                Assert.That(
                    ((AuthApiException)exception.InnerException!).StatusCode,
                    Is.EqualTo(StatusCodes.Status409Conflict));
                Assert.That(sessionIssuer.IssueCount, Is.Zero);
            });
            UserExternalIdentity savedIdentity = await dbContext.UserExternalIdentities
                .SingleAsync(x => x.ProviderId == provider.Id);
            Assert.That(savedIdentity.UserId, Is.EqualTo(owner.Id));
        }

        [Test]
        public async Task CompleteSignIn_WhenCorrelationCookieIsMissingOrWrong_RejectsWithoutConsumingState()
        {
            await using PostgresDbContext dbContext = CreateDbContext();
            using OidcTestHttpMessageHandler handler = new(_rsa);
            using HttpClient httpClient = new(handler);
            RecordingAuthSessionIssuer sessionIssuer = new();
            OidcAuthenticationService service = CreateService(
                dbContext,
                httpClient,
                sessionIssuer);
            OidcProvider provider = await CreateProviderAsync(dbContext);
            DefaultHttpContext beginContext = new();
            string authorizationUrl = await service.BeginSignInAsync(
                provider.Slug,
                "/login",
                beginContext.Response,
                CancellationToken.None);
            Dictionary<string, Microsoft.Extensions.Primitives.StringValues> query = QueryHelpers.ParseQuery(
                new Uri(authorizationUrl).Query);
            string state = query["state"].ToString();
            DefaultHttpContext missingCookieContext = new();
            DefaultHttpContext wrongCookieContext = CreateCallbackContext(
                beginContext,
                useValidValue: false);

            AuthApiException missingException = Assert.ThrowsAsync<AuthApiException>(async () =>
                await service.CompleteCallbackAsync(
                    state,
                    "authorization-code",
                    missingCookieContext.Request,
                    missingCookieContext.Response,
                    CancellationToken.None))!;
            AuthApiException wrongException = Assert.ThrowsAsync<AuthApiException>(async () =>
                await service.CompleteCallbackAsync(
                    state,
                    "authorization-code",
                    wrongCookieContext.Request,
                    wrongCookieContext.Response,
                    CancellationToken.None))!;
            bool stateStillExists = await dbContext.OidcLoginStates.AnyAsync(
                x => x.ProviderId == provider.Id);

            Assert.Multiple(() =>
            {
                Assert.That(missingException.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
                Assert.That(wrongException.StatusCode, Is.EqualTo(StatusCodes.Status400BadRequest));
                Assert.That(stateStillExists, Is.True);
                Assert.That(sessionIssuer.IssuedUserId, Is.Null);
            });
        }

        [Test]
        public async Task CancelCallback_WhenCorrelationCookieMatches_ConsumesStateAndCookie()
        {
            await using PostgresDbContext dbContext = CreateDbContext();
            using OidcTestHttpMessageHandler handler = new(_rsa);
            using HttpClient httpClient = new(handler);
            RecordingAuthSessionIssuer sessionIssuer = new();
            OidcAuthenticationService service = CreateService(
                dbContext,
                httpClient,
                sessionIssuer);
            OidcProvider provider = await CreateProviderAsync(dbContext);
            DefaultHttpContext beginContext = new();
            string authorizationUrl = await service.BeginSignInAsync(
                provider.Slug,
                "/login",
                beginContext.Response,
                CancellationToken.None);
            Dictionary<string, Microsoft.Extensions.Primitives.StringValues> query = QueryHelpers.ParseQuery(
                new Uri(authorizationUrl).Query);
            DefaultHttpContext callbackContext = CreateCallbackContext(beginContext, useValidValue: true);

            string returnUrl = await service.CancelCallbackAsync(
                query["state"].ToString(),
                callbackContext.Request,
                callbackContext.Response,
                CancellationToken.None);
            bool stateExists = await dbContext.OidcLoginStates.AnyAsync(
                x => x.ProviderId == provider.Id);
            string deletedCookie = callbackContext.Response.Headers.SetCookie.ToString().ToLowerInvariant();

            Assert.Multiple(() =>
            {
                Assert.That(returnUrl, Is.EqualTo("/login?oidc=error"));
                Assert.That(stateExists, Is.False);
                Assert.That(deletedCookie, Does.Contain("expires=thu, 01 jan 1970 00:00:00 gmt"));
                Assert.That(sessionIssuer.IssuedUserId, Is.Null);
            });
        }

        private OidcAuthenticationService CreateService(
            PostgresDbContext dbContext,
            HttpClient httpClient,
            RecordingAuthSessionIssuer sessionIssuer)
        {
            OidcDiscoveryService discovery = new(
                httpClient,
                NullLogger<OidcDiscoveryService>.Instance);
            OidcProviderService providers = new(dbContext, _cipher);
            AuthenticationSettingsService settings = new(dbContext);
            return new OidcAuthenticationService(
                dbContext,
                discovery,
                providers,
                settings,
                sessionIssuer,
                _cipher,
                NullLogger<OidcAuthenticationService>.Instance);
        }

        private async Task<OidcProvider> CreateProviderAsync(PostgresDbContext dbContext)
        {
            string suffix = Guid.NewGuid().ToString("N");
            OidcProvider provider = new()
            {
                Name = "Provider " + suffix,
                Slug = "provider-" + suffix,
                Issuer = "https://issuer-" + suffix + ".example",
                PublicBaseUrl = "https://octockup.example",
                ClientId = "client-" + suffix,
                Scopes = ["openid", "profile", "email"],
                IsEnabled = true,
            };
            await dbContext.OidcProviders.AddAsync(provider);
            await dbContext.SaveChangesAsync();
            return provider;
        }

        private string CreateIdToken(OidcProvider provider, string nonce, string subject)
        {
            RsaSecurityKey securityKey = new(_rsa)
            {
                KeyId = "test-key",
            };
            SigningCredentials credentials = new(securityKey, SecurityAlgorithms.RsaSha256);
            Claim[] claims =
            [
                new Claim(JwtRegisteredClaimNames.Sub, subject),
                new Claim("nonce", nonce),
                new Claim(JwtRegisteredClaimNames.Email, "user@example.com"),
                new Claim("name", "Test User"),
            ];
            JwtSecurityToken token = new(
                issuer: provider.Issuer + "/",
                audience: provider.ClientId,
                claims: claims,
                notBefore: DateTime.UtcNow.AddMinutes(-1),
                expires: DateTime.UtcNow.AddMinutes(5),
                signingCredentials: credentials);
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private PostgresDbContext CreateDbContext(SaveChangesBarrierInterceptor? interceptor = null)
        {
            DbContextOptionsBuilder<PostgresDbContext> builder = new DbContextOptionsBuilder<PostgresDbContext>()
                .UseNpgsql(_database.ConnectionString);
            if (interceptor is not null)
            {
                builder.AddInterceptors(interceptor);
            }

            return new PostgresDbContext(builder.Options);
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

        private static DefaultHttpContext CreateCallbackContext(
            DefaultHttpContext beginContext,
            bool useValidValue)
        {
            string setCookie = beginContext.Response.Headers.SetCookie.ToString();
            string cookiePair = setCookie.Split(';', 2)[0];
            string cookieName = cookiePair.Split('=', 2)[0];
            DefaultHttpContext callbackContext = new();
            callbackContext.Request.Headers.Cookie = useValidValue
                ? cookiePair
                : cookieName + "=wrong-state";
            return callbackContext;
        }
    }
}
