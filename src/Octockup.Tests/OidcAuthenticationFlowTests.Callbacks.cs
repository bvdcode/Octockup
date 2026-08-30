// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Octockup.Server.Database;
using Octockup.Server.Services;

namespace Octockup.Tests
{
    public partial class OidcAuthenticationFlowTests
    {
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
            handler.IdToken = CreateIdToken(provider, query["nonce"].ToString(), identity.Subject);
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
            handler.IdToken = CreateIdToken(provider, query["nonce"].ToString(), identity.Subject);
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
            string state = query["state"].ToString();
            DefaultHttpContext missingCookieContext = new();
            DefaultHttpContext wrongCookieContext = CreateCallbackContext(beginContext, useValidValue: false);

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
    }
}
