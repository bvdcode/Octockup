// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Octockup.Server.Database;
using Octockup.Server.Jobs;
using Octockup.Server.Models.Enums;
using Octockup.Server.Services;

namespace Octockup.Tests
{
    public class SnapshotBatchWriterTests
    {
        [Test]
        public async Task FlushAsync_WhenWritingLargeSnapshot_KeepsTrackedGraphBounded()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using SqliteDbContext dbContext = await CreateDbContextAsync(connection);
            Schedule schedule = await SeedScheduleAsync(dbContext);
            SnapshotBatchWriter writer = CreateWriter(dbContext);
            Snapshot snapshot = await writer.CreateAsync(
                schedule.BackupId,
                schedule,
                CancellationToken.None);

            await AddFilesAsync(writer, snapshot, schedule, start: 0, count: 100);
            await writer.FlushAsync(snapshot, schedule, CancellationToken.None);
            await AssertBatchStateAsync(dbContext, schedule, snapshot, expectedFiles: 100);

            await AddFilesAsync(writer, snapshot, schedule, start: 100, count: 100);
            await writer.FlushAsync(snapshot, schedule, CancellationToken.None);
            await AssertBatchStateAsync(dbContext, schedule, snapshot, expectedFiles: 200);

            await AddFilesAsync(writer, snapshot, schedule, start: 200, count: 1);
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

        [Test]
        public async Task AddFileAsync_WhenFileHasManyChunks_WritesBoundedReferenceBatches()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using SqliteDbContext dbContext = await CreateDbContextAsync(connection);
            Schedule schedule = await SeedScheduleAsync(dbContext);
            SnapshotBatchWriter writer = CreateWriter(dbContext);
            Snapshot snapshot = await writer.CreateAsync(
                schedule.BackupId,
                schedule,
                CancellationToken.None);
            SnapshotFile snapshotFile = new()
            {
                Path = "large.bin",
                Name = "large.bin",
                Size = 501,
                Hashsum = "large-hash",
                ChunkHashes = Enumerable.Range(0, 501)
                    .Select(index => "chunk-" + index.ToString("D4"))
                    .ToList()
            };

            await writer.AddFileAsync(
                snapshot,
                schedule,
                schedule.Backup.StorageId,
                snapshotFile,
                CancellationToken.None);
            await writer.CompleteAsync(snapshot, schedule, CancellationToken.None);
            SnapshotFile persistedFile = await dbContext.SnapshotFiles
                .AsNoTracking()
                .SingleAsync(x => x.Id == snapshotFile.Id);
            List<int> ordinals = await dbContext.SnapshotChunkReferences
                .AsNoTracking()
                .Where(x => x.SnapshotFileId == snapshotFile.Id)
                .OrderBy(x => x.Ordinal)
                .Select(x => x.Ordinal)
                .ToListAsync();

            Assert.Multiple(() =>
            {
                Assert.That(persistedFile.ChunkReferencesIndexed, Is.True);
                Assert.That(ordinals, Has.Count.EqualTo(501));
                Assert.That(ordinals.First(), Is.Zero);
                Assert.That(ordinals.Last(), Is.EqualTo(500));
                AssertTrackedScheduleOnly(dbContext, schedule);
            });
        }

        private static async Task AddFilesAsync(
            SnapshotBatchWriter writer,
            Snapshot snapshot,
            Schedule schedule,
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
                    ChunkHashes = []
                };

                await writer.AddFileAsync(
                    snapshot,
                    schedule,
                    schedule.Backup.StorageId,
                    snapshotFile,
                    CancellationToken.None);
            }
        }

        private static SnapshotBatchWriter CreateWriter(AppDbContext dbContext)
        {
            SnapshotChunkReferenceWriter referenceWriter = new(
                dbContext,
                NullLogger<SnapshotChunkReferenceWriter>.Instance);
            return new SnapshotBatchWriter(dbContext, referenceWriter);
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

        private static async Task<SqliteDbContext> CreateDbContextAsync(SqliteConnection connection)
        {
            DbContextOptions<SqliteDbContext> options = new DbContextOptionsBuilder<SqliteDbContext>()
                .UseSqlite(connection)
                .Options;
            SqliteDbContext dbContext = new(options);
            await dbContext.Database.EnsureCreatedAsync();
            return dbContext;
        }

        private static async Task<Schedule> SeedScheduleAsync(AppDbContext dbContext)
        {
            User user = new()
            {
                Username = "snapshot-writer-user",
                PasswordPhc = "password"
            };
            Module source = new()
            {
                User = user,
                Tag = "snapshot-writer-source",
                BackupModuleId = "source",
                Destination = ModuleDestination.Source
            };
            Module storage = new()
            {
                User = user,
                Tag = "snapshot-writer-storage",
                BackupModuleId = "storage",
                Destination = ModuleDestination.Target
            };
            Backup backup = new()
            {
                Source = source,
                Storage = storage,
                Tag = "snapshot-writer-backup"
            };
            Schedule schedule = new()
            {
                Backup = backup,
                StartAt = DateTime.UtcNow,
                Status = ScheduleStatus.Running
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
