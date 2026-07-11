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
using Octockup.Server.Models.Dto;
using Octockup.Server.Models.Enums;
using Octockup.Server.Models.Requests;
using Quartz.Impl;
using System.Security.Claims;

namespace Octockup.Tests
{
    public class ControllerAuthorizationTests
    {
        private SqliteConnection _connection = null!;
        private SqliteDbContext _dbContext = null!;
        private Guid _firstUserId;
        private Guid _firstUserModuleId;
        private Guid _firstUserScheduleId;
        private Guid _secondUserId;
        private Guid _secondUserModuleId;

        [SetUp]
        public async Task Setup()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            await _connection.OpenAsync();
            DbContextOptions<SqliteDbContext> options = new DbContextOptionsBuilder<SqliteDbContext>()
                .UseSqlite(_connection)
                .Options;
            _dbContext = new SqliteDbContext(options);
            await _dbContext.Database.EnsureCreatedAsync();

            User firstUser = new()
            {
                Username = "first-user",
                PasswordPhc = "password"
            };
            User secondUser = new()
            {
                Username = "second-user",
                PasswordPhc = "password"
            };
            Module firstSource = CreateModule(firstUser, "first-source", ModuleDestination.Source);
            Module firstStorage = CreateModule(firstUser, "first-storage", ModuleDestination.Target);
            Module secondSource = CreateModule(secondUser, "second-source", ModuleDestination.Source);
            Module secondStorage = CreateModule(secondUser, "second-storage", ModuleDestination.Target);
            Backup firstBackup = new()
            {
                Source = firstSource,
                Storage = firstStorage,
                Tag = "first-backup"
            };
            Schedule firstSchedule = new()
            {
                Backup = firstBackup,
                StartAt = DateTime.UtcNow,
                Status = ScheduleStatus.Created
            };

            await _dbContext.AddRangeAsync(
                firstUser,
                secondUser,
                firstSource,
                firstStorage,
                secondSource,
                secondStorage,
                firstBackup,
                firstSchedule);
            await _dbContext.SaveChangesAsync();

            _firstUserId = firstUser.Id;
            _firstUserModuleId = firstStorage.Id;
            _firstUserScheduleId = firstSchedule.Id;
            _secondUserId = secondUser.Id;
            _secondUserModuleId = secondStorage.Id;
        }

        [TearDown]
        public async Task TearDown()
        {
            await _dbContext.DisposeAsync();
            await _connection.DisposeAsync();
        }

        [Test]
        public async Task RenameModule_WhenModuleBelongsToAnotherUser_ReturnsNotFound()
        {
            ModuleController controller = CreateModuleController(_secondUserId);

            IActionResult result = await controller.RenameModule(
                _firstUserModuleId,
                new RenameModuleRequest { NewTag = "unauthorized-rename" });
            Module module = await _dbContext.Modules
                .AsNoTracking()
                .SingleAsync(x => x.Id == _firstUserModuleId);

            Assert.Multiple(() =>
            {
                Assert.That(GetStatusCode(result), Is.EqualTo(StatusCodes.Status404NotFound));
                Assert.That(module.Tag, Is.EqualTo("first-storage"));
            });
        }

        [Test]
        public async Task DeleteModule_WhenModuleBelongsToAnotherUser_ReturnsNotFound()
        {
            ModuleController controller = CreateModuleController(_secondUserId);

            IActionResult result = await controller.DeleteUserBackupStorage(_firstUserModuleId);
            bool moduleExists = await _dbContext.Modules
                .AsNoTracking()
                .AnyAsync(x => x.Id == _firstUserModuleId);

            Assert.Multiple(() =>
            {
                Assert.That(GetStatusCode(result), Is.EqualTo(StatusCodes.Status404NotFound));
                Assert.That(moduleExists, Is.True);
            });
        }

        [Test]
        public async Task CancelSchedule_WhenScheduleBelongsToAnotherUser_ReturnsNotFound()
        {
            ScheduleController controller = CreateScheduleController(_secondUserId);

            IActionResult result = await controller.CancelSchedule(_firstUserScheduleId);
            Schedule schedule = await _dbContext.Schedules
                .AsNoTracking()
                .SingleAsync(x => x.Id == _firstUserScheduleId);

            Assert.Multiple(() =>
            {
                Assert.That(GetStatusCode(result), Is.EqualTo(StatusCodes.Status404NotFound));
                Assert.That(schedule.Status, Is.EqualTo(ScheduleStatus.Created));
            });
        }

        [Test]
        public async Task GetUserModules_ReturnsOnlyCurrentUsersModules()
        {
            ModuleController controller = CreateModuleController(_secondUserId);

            List<ModuleDto> result = (await controller.GetUserModules()).ToList();

            Assert.Multiple(() =>
            {
                Assert.That(result, Has.Count.EqualTo(2));
                Assert.That(result.Select(x => x.Id), Does.Contain(_secondUserModuleId));
                Assert.That(result.Select(x => x.Id), Does.Not.Contain(_firstUserModuleId));
            });
        }

        private static Module CreateModule(
            User user,
            string tag,
            ModuleDestination destination)
        {
            return new Module
            {
                User = user,
                Tag = tag,
                BackupModuleId = tag + "-provider",
                Destination = destination
            };
        }

        private ModuleController CreateModuleController(Guid userId)
        {
            ModuleController controller = new(
                new TestCipher(),
                _dbContext,
                NullLogger<ModuleController>.Instance,
                Array.Empty<IBackupProvider>());
            controller.ControllerContext = CreateControllerContext(userId);
            return controller;
        }

        private ScheduleController CreateScheduleController(Guid userId)
        {
            ScheduleController controller = new(_dbContext, new StdSchedulerFactory());
            controller.ControllerContext = CreateControllerContext(userId);
            return controller;
        }

        private static ControllerContext CreateControllerContext(Guid userId)
        {
            ClaimsIdentity identity = new(
                [new Claim("sub", userId.ToString())],
                "TestAuthentication");
            DefaultHttpContext httpContext = new()
            {
                User = new ClaimsPrincipal(identity)
            };
            return new ControllerContext
            {
                HttpContext = httpContext
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
