// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using EasyExtensions.AspNetCore.Authorization.Abstractions;
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

namespace Octockup.Tests
{
    public partial class AuthenticationConcurrencyTests
    {
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
