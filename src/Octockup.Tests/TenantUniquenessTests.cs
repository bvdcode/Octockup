// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Octockup.Server.Database;
using Octockup.Server.Models.Enums;
using Octockup.Server.Services;

namespace Octockup.Tests
{
    public class TenantUniquenessTests
    {
        private SqliteConnection _connection = null!;
        private SqliteDbContext _dbContext = null!;
        private User _firstUser = null!;
        private User _secondUser = null!;
        private Module _firstSource = null!;
        private Module _firstStorage = null!;
        private Module _secondSource = null!;
        private Module _secondStorage = null!;

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

            _firstUser = CreateUser("first-user");
            _secondUser = CreateUser("second-user");
            _firstSource = CreateModule(_firstUser, "first-source", ModuleDestination.Source);
            _firstStorage = CreateModule(_firstUser, "first-storage", ModuleDestination.Target);
            _secondSource = CreateModule(_secondUser, "second-source", ModuleDestination.Source);
            _secondStorage = CreateModule(_secondUser, "second-storage", ModuleDestination.Target);
            await _dbContext.AddRangeAsync(
                _firstUser,
                _secondUser,
                _firstSource,
                _firstStorage,
                _secondSource,
                _secondStorage);
            await _dbContext.SaveChangesAsync();
        }

        [TearDown]
        public async Task TearDown()
        {
            await _dbContext.DisposeAsync();
            await _connection.DisposeAsync();
        }

        [Test]
        public async Task ModuleTags_AreUniquePerUser()
        {
            Module first = CreateModule(
                _firstUser,
                "shared-module",
                ModuleDestination.Target);
            Module second = CreateModule(
                _secondUser,
                "shared-module",
                ModuleDestination.Target);
            await _dbContext.Modules.AddRangeAsync(first, second);
            await _dbContext.SaveChangesAsync();

            Module duplicate = CreateModule(
                _firstUser,
                "shared-module",
                ModuleDestination.Source);
            await _dbContext.Modules.AddAsync(duplicate);

            Assert.Multiple(() =>
            {
                Assert.That(_dbContext.Modules.Count(x => x.Tag == "shared-module"),
                    Is.EqualTo(2));
                Assert.That(
                    async () => await _dbContext.SaveChangesAsync(),
                    Throws.InstanceOf<DbUpdateException>());
            });
        }

        [Test]
        public async Task BackupTags_AreUniquePerUser()
        {
            Backup first = CreateBackup(
                _firstUser.Id,
                _firstSource,
                _firstStorage,
                "shared-backup");
            Backup second = CreateBackup(
                _secondUser.Id,
                _secondSource,
                _secondStorage,
                "shared-backup");
            await _dbContext.Backups.AddRangeAsync(first, second);
            await _dbContext.SaveChangesAsync();

            Backup duplicate = CreateBackup(
                _firstUser.Id,
                _firstSource,
                _firstStorage,
                "shared-backup");
            await _dbContext.Backups.AddAsync(duplicate);

            Assert.Multiple(() =>
            {
                Assert.That(_dbContext.Backups.Count(x => x.Tag == "shared-backup"),
                    Is.EqualTo(2));
                Assert.That(
                    async () => await _dbContext.SaveChangesAsync(),
                    Throws.InstanceOf<DbUpdateException>());
            });
        }

        [Test]
        public async Task BackupOwnershipInitializer_BackfillsLegacyTenantKey()
        {
            Backup legacy = CreateBackup(
                Guid.Empty,
                _firstSource,
                _firstStorage,
                "legacy-backup");
            await _dbContext.Backups.AddAsync(legacy);
            await _dbContext.SaveChangesAsync();
            _dbContext.ChangeTracker.Clear();
            BackupOwnershipInitializer initializer = new(
                _dbContext,
                NullLogger<BackupOwnershipInitializer>.Instance);

            await initializer.InitializeAsync(CancellationToken.None);
            Backup backfilled = await _dbContext.Backups
                .AsNoTracking()
                .SingleAsync(x => x.Id == legacy.Id);

            Assert.That(backfilled.UserId, Is.EqualTo(_firstUser.Id));
        }

        private static User CreateUser(string username)
        {
            return new User
            {
                Username = username,
                PasswordPhc = "password"
            };
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

        private static Backup CreateBackup(
            Guid userId,
            Module source,
            Module storage,
            string tag)
        {
            return new Backup
            {
                UserId = userId,
                Source = source,
                Storage = storage,
                Tag = tag
            };
        }
    }
}
