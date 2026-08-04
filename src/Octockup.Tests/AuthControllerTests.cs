// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.AspNetCore.Authorization.Models.Dto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Octockup.Server.Abstractions;
using Octockup.Server.Controllers;
using Octockup.Server.Database;
using Octockup.Server.Services;

namespace Octockup.Tests
{
    [Category("Integration")]
    public class AuthControllerTests
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
        public async Task Login_WhenPasswordLoginIsDisabled_DoesNotCreateUser()
        {
            await using PostgresDbContext dbContext = CreateDbContext();
            await dbContext.AuthenticationSettings.AddAsync(new AuthenticationSettings
            {
                Name = AuthenticationSettings.GlobalName,
                PasswordLoginEnabled = false,
            });
            await dbContext.SaveChangesAsync();
            RecordingAuthSessionIssuer sessionIssuer = new();
            AuthController controller = CreateController(dbContext, sessionIssuer);

            IActionResult result = await controller.LoginAsync(
                new LoginRequestDto { Username = "new-user", Password = "secret" },
                CancellationToken.None);

            Assert.That(result, Is.InstanceOf<ObjectResult>());
            Assert.That(((ObjectResult)result).StatusCode, Is.EqualTo(StatusCodes.Status401Unauthorized));
            Assert.That(await dbContext.Users.CountAsync(), Is.Zero);
            Assert.That(sessionIssuer.IssueCount, Is.Zero);
        }

        [Test]
        public async Task Login_WhenUserIsDisabled_DoesNotIssueSession()
        {
            await using PostgresDbContext dbContext = CreateDbContext();
            await dbContext.Users.AddAsync(new User
            {
                Username = "disabled-user",
                PasswordPhc = "hash:secret",
                IsAdmin = true,
                IsDisabled = true,
            });
            await dbContext.SaveChangesAsync();
            RecordingAuthSessionIssuer sessionIssuer = new();
            AuthController controller = CreateController(dbContext, sessionIssuer);

            IActionResult result = await controller.LoginAsync(
                new LoginRequestDto { Username = "disabled-user", Password = "secret" },
                CancellationToken.None);

            Assert.That(result, Is.InstanceOf<ObjectResult>());
            Assert.That(((ObjectResult)result).StatusCode, Is.EqualTo(StatusCodes.Status401Unauthorized));
            Assert.That(sessionIssuer.IssueCount, Is.Zero);
        }

        [Test]
        public async Task Login_WhenExistingUsernameUsesLegacyCharacters_IssuesSession()
        {
            await using PostgresDbContext dbContext = CreateDbContext();
            await dbContext.Users.AddAsync(new User
            {
                Username = "legacy@example.com",
                PasswordPhc = "hash:secret",
                IsAdmin = true,
                IsDisabled = false,
            });
            await dbContext.SaveChangesAsync();
            RecordingAuthSessionIssuer sessionIssuer = new();
            AuthController controller = CreateController(dbContext, sessionIssuer);

            IActionResult result = await controller.LoginAsync(
                new LoginRequestDto { Username = "legacy@example.com", Password = "secret" },
                CancellationToken.None);

            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            Assert.That(sessionIssuer.IssueCount, Is.EqualTo(1));
        }

        [Test]
        public async Task Login_WhenCreatingFirstUser_MakesUserAdministrator()
        {
            await using PostgresDbContext dbContext = CreateDbContext();
            RecordingAuthSessionIssuer sessionIssuer = new();
            AuthController controller = CreateController(dbContext, sessionIssuer);

            IActionResult result = await controller.LoginAsync(
                new LoginRequestDto { Username = "first-user", Password = "secret" },
                CancellationToken.None);

            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            User user = await dbContext.Users.SingleAsync();
            Assert.That(user.IsAdmin, Is.True);
            Assert.That(user.IsDisabled, Is.False);
            Assert.That(sessionIssuer.IssueCount, Is.EqualTo(1));
        }

        [Test]
        public async Task Login_WhenUserDoesNotExistAndApplicationHasUser_DoesNotSelfRegister()
        {
            await using PostgresDbContext dbContext = CreateDbContext();
            await dbContext.Users.AddAsync(new User
            {
                Username = "existing-user",
                PasswordPhc = "hash:secret",
                IsAdmin = true,
                IsDisabled = false,
            });
            await dbContext.SaveChangesAsync();
            RecordingAuthSessionIssuer sessionIssuer = new();
            AuthController controller = CreateController(dbContext, sessionIssuer);

            IActionResult result = await controller.LoginAsync(
                new LoginRequestDto { Username = "unknown-user", Password = "secret" },
                CancellationToken.None);

            Assert.That(result, Is.InstanceOf<ObjectResult>());
            Assert.That(((ObjectResult)result).StatusCode, Is.EqualTo(StatusCodes.Status401Unauthorized));
            Assert.That(await dbContext.Users.CountAsync(), Is.EqualTo(1));
            Assert.That(sessionIssuer.IssueCount, Is.Zero);
        }

        [Test]
        public void RefreshCookie_IsAvailableToRefreshEndpointAfterOidcCallback()
        {
            CookieOptions options = AuthSessionIssuer.CreateRefreshCookieOptions();

            Assert.That(options.Path, Is.EqualTo("/api/v1/auth"));
            Assert.That(options.HttpOnly, Is.True);
            Assert.That(options.Secure, Is.True);
            Assert.That(options.SameSite, Is.EqualTo(SameSiteMode.Strict));
        }

        [Test]
        public async Task Refresh_WhenCookieSessionRotates_ReturnsNewSession()
        {
            await using PostgresDbContext dbContext = CreateDbContext();
            TokenPairResponseDto rotationResult = new()
            {
                AccessToken = "new-access-token",
                RefreshToken = AuthSessionIssuer.SessionMarker,
            };
            RecordingAuthSessionIssuer sessionIssuer = new()
            {
                RotationResult = rotationResult,
            };
            AuthController controller = CreateController(dbContext, sessionIssuer);
            controller.Request.Headers.Cookie = "refresh_token=cookie-token";

            IActionResult result = await controller.RefreshTokenAsync(
                new RefreshTokenRequestDto { RefreshToken = string.Empty },
                CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.InstanceOf<OkObjectResult>());
                Assert.That(((OkObjectResult)result).Value, Is.SameAs(rotationResult));
                Assert.That(sessionIssuer.RotateCount, Is.EqualTo(1));
                Assert.That(sessionIssuer.RotatedToken, Is.EqualTo("cookie-token"));
            });
        }

        [Test]
        public async Task Refresh_WhenRotationRejectsToken_ReturnsUnauthorized()
        {
            await using PostgresDbContext dbContext = CreateDbContext();
            RecordingAuthSessionIssuer sessionIssuer = new();
            AuthController controller = CreateController(dbContext, sessionIssuer);

            IActionResult result = await controller.RefreshTokenAsync(
                new RefreshTokenRequestDto { RefreshToken = "invalid-token" },
                CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.InstanceOf<ObjectResult>());
                Assert.That(((ObjectResult)result).StatusCode, Is.EqualTo(StatusCodes.Status401Unauthorized));
                Assert.That(sessionIssuer.RotateCount, Is.EqualTo(1));
            });
        }

        private AuthController CreateController(
            AppDbContext dbContext,
            IAuthSessionIssuer sessionIssuer)
        {
            AuthController controller = new(
                dbContext,
                NullLogger<Microsoft.AspNetCore.Mvc.ActionContext>.Instance,
                new TestPasswordHashService(),
                new AuthenticationSettingsService(dbContext),
                sessionIssuer);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext(),
            };
            return controller;
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
