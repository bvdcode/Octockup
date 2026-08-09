// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore;
using Octockup.Server.Database;
using Octockup.Server.Handlers.Administration;
using Octockup.Server.Models.Dto;
using Octockup.Server.Models.Enums;

namespace Octockup.Tests
{
    [Category("Integration")]
    public class StorageCleanupRunQueryTests
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

        [Test]
        public async Task Handle_ReturnsNewestRunsWithStorageMetadata()
        {
            DbContextOptions<PostgresDbContext> options = new DbContextOptionsBuilder<PostgresDbContext>()
                .UseNpgsql(_database.ConnectionString)
                .Options;
            await using PostgresDbContext dbContext = new(options);
            string suffix = Guid.NewGuid().ToString("N");
            User user = new()
            {
                Username = $"cleanup-history-{suffix}",
                PasswordPhc = "not-used",
            };
            Module storage = new()
            {
                User = user,
                Tag = $"History storage {suffix}",
                Destination = ModuleDestination.Target,
                BackupModuleId = "test-storage",
            };
            StorageCleanupRun olderRun = new()
            {
                Module = storage,
                Status = StorageCleanupStatus.Completed,
                StartedAt = DateTime.UtcNow.AddHours(-2),
                CompletedAt = DateTime.UtcNow.AddHours(-1),
            };
            StorageCleanupRun latestRun = new()
            {
                Module = storage,
                Status = StorageCleanupStatus.Completed,
                StartedAt = DateTime.UtcNow.AddMinutes(-30),
                CompletedAt = DateTime.UtcNow,
                ScannedChunks = 12_000,
                DeletedChunks = 80,
                ReclaimedBytes = 4_096,
            };
            await dbContext.AddRangeAsync(user, storage, olderRun, latestRun);
            await dbContext.SaveChangesAsync();
            GetStorageCleanupRunsQueryHandler handler = new(dbContext);

            IReadOnlyCollection<StorageCleanupRunDto> result = await handler.Handle(
                new GetStorageCleanupRunsQuery(1),
                CancellationToken.None);

            StorageCleanupRunDto run = result.Single();
            Assert.Multiple(() =>
            {
                Assert.That(run.Id, Is.EqualTo(latestRun.Id));
                Assert.That(run.ModuleTag, Is.EqualTo(storage.Tag));
                Assert.That(run.ScannedChunks, Is.EqualTo(12_000));
                Assert.That(run.DeletedChunks, Is.EqualTo(80));
                Assert.That(run.ReclaimedBytes, Is.EqualTo(4_096));
            });
        }
    }
}
