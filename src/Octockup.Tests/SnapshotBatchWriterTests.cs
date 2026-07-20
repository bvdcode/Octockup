// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore;
using Octockup.Server.Database;
using Octockup.Server.Jobs;
using Octockup.Server.Models.Enums;

namespace Octockup.Tests
{
    [Category("Integration")]
    public class SnapshotBatchWriterTests
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
        public async Task FlushAsync_WhenWritingMultipleBatches_KeepsTrackedGraphBounded()
        {
            DbContextOptions<PostgresDbContext> options = new DbContextOptionsBuilder<PostgresDbContext>()
                .UseNpgsql(_database.ConnectionString)
                .Options;
            await using PostgresDbContext dbContext = new(options);
            Schedule schedule = await SeedScheduleAsync(dbContext);
            SnapshotBatchWriter writer = new(dbContext);
            Snapshot snapshot = await writer.CreateAsync(
                schedule.BackupId,
                schedule,
                CancellationToken.None);

            await AddFilesAsync(writer, snapshot, start: 0, count: 100);
            await writer.FlushAsync(snapshot, schedule, CancellationToken.None);
            await AssertBatchStateAsync(dbContext, schedule, snapshot, expectedFiles: 100);

            await AddFilesAsync(writer, snapshot, start: 100, count: 100);
            await writer.FlushAsync(snapshot, schedule, CancellationToken.None);
            await AssertBatchStateAsync(dbContext, schedule, snapshot, expectedFiles: 200);

            await AddFilesAsync(writer, snapshot, start: 200, count: 1);
            await writer.CompleteAsync(snapshot, schedule, CancellationToken.None);

            Snapshot completedSnapshot = await dbContext.Snapshots
                .AsNoTracking()
                .SingleAsync(x => x.Id == snapshot.Id);
            Assert.Multiple(() =>
            {
                Assert.That(completedSnapshot.CompletedAt, Is.Not.Null);
                Assert.That(completedSnapshot.FilesCount, Is.EqualTo(201));
                Assert.That(completedSnapshot.TotalSize, Is.EqualTo(201));
                Assert.That(snapshot.Files, Is.Empty);
                AssertTrackedScheduleOnly(dbContext, schedule);
            });
        }

        private static async Task AddFilesAsync(
            SnapshotBatchWriter writer,
            Snapshot snapshot,
            int start,
            int count)
        {
            for (int index = start; index < start + count; index++)
            {
                string fileName = $"file-{index}.bin";
                SnapshotFile snapshotFile = new()
                {
                    Path = fileName,
                    Name = fileName,
                    Size = 1,
                    Hashsum = index.ToString("x64"),
                    ChunkHashes = [],
                };

                await writer.AddFileAsync(snapshot, snapshotFile, CancellationToken.None);
            }
        }

        private static async Task AssertBatchStateAsync(
            AppDbContext dbContext,
            Schedule schedule,
            Snapshot snapshot,
            int expectedFiles)
        {
            Snapshot persistedSnapshot = await dbContext.Snapshots
                .AsNoTracking()
                .SingleAsync(x => x.Id == snapshot.Id);
            int persistedFiles = await dbContext.SnapshotFiles
                .AsNoTracking()
                .CountAsync(x => x.SnapshotId == snapshot.Id);

            Assert.Multiple(() =>
            {
                Assert.That(persistedSnapshot.FilesCount, Is.EqualTo(expectedFiles));
                Assert.That(persistedSnapshot.TotalSize, Is.EqualTo(expectedFiles));
                Assert.That(persistedFiles, Is.EqualTo(expectedFiles));
                Assert.That(snapshot.Files, Is.Empty);
                AssertTrackedScheduleOnly(dbContext, schedule);
            });
        }

        private static void AssertTrackedScheduleOnly(AppDbContext dbContext, Schedule schedule)
        {
            object[] trackedEntities = dbContext.ChangeTracker
                .Entries()
                .Select(x => x.Entity)
                .ToArray();

            Assert.That(trackedEntities, Is.EqualTo(new object[] { schedule }));
        }

        private static async Task<Schedule> SeedScheduleAsync(AppDbContext dbContext)
        {
            string suffix = Guid.NewGuid().ToString("N");
            User user = new()
            {
                Username = $"snapshot-writer-{suffix}",
                PasswordPhc = "password",
            };
            Module source = new()
            {
                User = user,
                Tag = $"snapshot-writer-source-{suffix}",
                BackupModuleId = "source",
                Destination = ModuleDestination.Source,
            };
            Module storage = new()
            {
                User = user,
                Tag = $"snapshot-writer-storage-{suffix}",
                BackupModuleId = "storage",
                Destination = ModuleDestination.Target,
            };
            Backup backup = new()
            {
                Source = source,
                Storage = storage,
                Tag = $"snapshot-writer-backup-{suffix}",
            };
            Schedule schedule = new()
            {
                Backup = backup,
                StartAt = DateTime.UtcNow,
                Status = ScheduleStatus.Running,
            };

            await dbContext.Schedules.AddAsync(schedule);
            await dbContext.SaveChangesAsync();
            dbContext.ChangeTracker.Clear();

            return await dbContext.Schedules
                .Include(x => x.Backup)
                    .ThenInclude(x => x.Source)
                .Include(x => x.Backup)
                    .ThenInclude(x => x.Storage)
                .SingleAsync(x => x.Id == schedule.Id);
        }
    }
}
