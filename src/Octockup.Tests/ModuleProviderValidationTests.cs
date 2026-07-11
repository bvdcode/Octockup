// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Octockup.Server.Abstractions;
using Octockup.Server.Controllers;
using Octockup.Server.Database;
using Octockup.Server.Models.Enums;
using Octockup.Server.Models.Requests;
using System.Security.Claims;

namespace Octockup.Tests
{
    public class ModuleProviderValidationTests
    {
        private SqliteConnection _connection = null!;
        private SqliteDbContext _dbContext = null!;
        private TestStorage _provider = null!;
        private Guid _userId;

        [SetUp]
        public async Task Setup()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            await _connection.OpenAsync();
            DbContextOptions<SqliteDbContext> options =
                new DbContextOptionsBuilder<SqliteDbContext>()
                    .UseSqlite(_connection)
                    .Options;
            _dbContext = new SqliteDbContext(options);
            await _dbContext.Database.EnsureCreatedAsync();
            User user = new()
            {
                Username = "module-user",
                PasswordPhc = "password"
            };
            await _dbContext.Users.AddAsync(user);
            await _dbContext.SaveChangesAsync();
            _userId = user.Id;
            _provider = new TestStorage();
        }

        [TearDown]
        public async Task TearDown()
        {
            await _dbContext.DisposeAsync();
            await _connection.DisposeAsync();
        }

        [Test]
        public async Task CreateBackupModule_WhenRequestProviderDiffersFromRoute_ReturnsBadRequest()
        {
            ModuleController controller = CreateController();
            CreateModuleRequest request = CreateRequest(
                "different-provider",
                ModuleDestination.Target,
                "mismatch");

            IActionResult result = await controller.CreateBackupModule(_provider.Id, request);

            Assert.Multiple(() =>
            {
                Assert.That(GetStatusCode(result), Is.EqualTo(StatusCodes.Status400BadRequest));
                Assert.That(_dbContext.Modules, Is.Empty);
            });
        }

        [Test]
        public async Task CreateBackupModule_WhenProviderDoesNotSupportDestination_ReturnsBadRequest()
        {
            ModuleController controller = CreateController();
            CreateModuleRequest request = CreateRequest(
                _provider.Id,
                ModuleDestination.Source,
                "unsupported");

            IActionResult result = await controller.CreateBackupModule(_provider.Id, request);

            Assert.Multiple(() =>
            {
                Assert.That(GetStatusCode(result), Is.EqualTo(StatusCodes.Status400BadRequest));
                Assert.That(_dbContext.Modules, Is.Empty);
            });
        }

        [Test]
        public async Task CreateBackupModule_WhenProviderMatchesTarget_PersistsCanonicalProviderId()
        {
            ModuleController controller = CreateController();
            CreateModuleRequest request = CreateRequest(
                _provider.Id,
                ModuleDestination.Target,
                "valid-storage");

            IActionResult result = await controller.CreateBackupModule(_provider.Id, request);
            Module module = await _dbContext.Modules.AsNoTracking().SingleAsync();

            Assert.Multiple(() =>
            {
                Assert.That(GetStatusCode(result), Is.EqualTo(StatusCodes.Status200OK));
                Assert.That(module.UserId, Is.EqualTo(_userId));
                Assert.That(module.BackupModuleId, Is.EqualTo(_provider.Id));
                Assert.That(module.Destination, Is.EqualTo(ModuleDestination.Target));
            });
        }

        private ModuleController CreateController()
        {
            ModuleController controller = new(
                new TestCipher(),
                _dbContext,
                NullLogger<ModuleController>.Instance,
                new IBackupProvider[] { _provider });
            ClaimsIdentity identity = new(
                [new Claim("sub", _userId.ToString())],
                "TestAuthentication");
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(identity)
                }
            };
            return controller;
        }

        private static CreateModuleRequest CreateRequest(
            string providerId,
            ModuleDestination destination,
            string tag)
        {
            return new CreateModuleRequest
            {
                BackupModuleId = providerId,
                Destination = destination,
                Tag = tag,
                Parameters = []
            };
        }

        private static int? GetStatusCode(IActionResult result)
        {
            return result switch
            {
                ObjectResult objectResult => objectResult.StatusCode,
                StatusCodeResult statusCodeResult => statusCodeResult.StatusCode,
                _ => null
            };
        }
    }
}
