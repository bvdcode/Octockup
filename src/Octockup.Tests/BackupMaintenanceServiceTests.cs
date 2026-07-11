// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.Abstractions;
using EasyExtensions.Models.Enums;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Octockup.Server.Abstractions;
using Octockup.Server.Database;
using Octockup.Server.Helpers;
using Octockup.Server.Models;
using Octockup.Server.Models.Dto;
using Octockup.Server.Models.Enums;
using Octockup.Server.Models.Results;
using Octockup.Server.Services;
using System.Runtime.CompilerServices;

namespace Octockup.Tests
{
    public class BackupMaintenanceServiceTests
    {
        private const string ReferencedHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        private const string OrphanHash = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";

        [Test]
        public async Task DeleteAsync_WhenBackupHasMetadata_RemovesDependentRowsBeforeBackup()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using SqliteDbContext dbContext = await CreateDbContextAsync(connection);

            (Guid userId, Guid backupId, _, Guid storageId, Guid snapshotId) =
                await SeedBackupAsync(dbContext);
            await SeedArchiveHistoryAsync(dbContext, userId, snapshotId);
            ImmediateStorageOperationCoordinator coordinator = new();
            BackupDeletionService service = new(
                dbContext,
                coordinator);

            var result = await service.DeleteAsync(userId, backupId, CancellationToken.None);

            dbContext.ChangeTracker.Clear();

            Assert.Multiple(() =>
            {
                Assert.That(result.Deleted, Is.True);
                Assert.That(result.DeletedSchedules, Is.EqualTo(1));
                Assert.That(result.DeletedSnapshots, Is.EqualTo(1));
                Assert.That(result.DeletedSnapshotFiles, Is.EqualTo(1));
                Assert.That(dbContext.Backups.Count(), Is.Zero);
                Assert.That(dbContext.Schedules.Count(), Is.Zero);
                Assert.That(dbContext.Snapshots.Count(), Is.Zero);
                Assert.That(dbContext.SnapshotFiles.Count(), Is.Zero);
                Assert.That(dbContext.SnapshotChunkReferences.Count(), Is.Zero);
                Assert.That(dbContext.SnapshotArchiveJobs.Count(), Is.Zero);
                Assert.That(dbContext.DownloadTickets.Count(), Is.Zero);
                Assert.That(dbContext.UploadedHashes.Count(), Is.EqualTo(1));
                Assert.That(coordinator.RequestedStorageId, Is.EqualTo(storageId));
                Assert.That(coordinator.RequestedKind, Is.EqualTo(StorageOperationKind.Maintenance));
                Assert.That(coordinator.Lease?.Disposed, Is.True);
            });
        }

        [Test]
        public async Task DeleteAsync_WhenStorageIsBusy_KeepsBackupRows()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using SqliteDbContext dbContext = await CreateDbContextAsync(connection);
            (Guid userId, Guid backupId, _, _, _) = await SeedBackupAsync(dbContext);
            ImmediateStorageOperationCoordinator coordinator = new()
            {
                RejectAcquisition = true
            };
            BackupDeletionService service = new(dbContext, coordinator);

            BackupDeletionResult result = await service.DeleteAsync(
                userId,
                backupId,
                CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(result.Deleted, Is.False);
                Assert.That(result.ErrorMessage, Does.Contain("busy"));
                Assert.That(coordinator.RequestedKind, Is.EqualTo(StorageOperationKind.Maintenance));
                Assert.That(dbContext.Backups.Count(), Is.EqualTo(1));
                Assert.That(dbContext.Snapshots.Count(), Is.EqualTo(1));
            });
        }

        [Test]
        public async Task DeleteAsync_WhenBackupIsRunning_KeepsBackupRows()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using SqliteDbContext dbContext = await CreateDbContextAsync(connection);

            (Guid userId, Guid backupId, Guid scheduleId, _, _) = await SeedBackupAsync(dbContext);
            Schedule schedule = await dbContext.Schedules.SingleAsync(x => x.Id == scheduleId);
            schedule.Status = ScheduleStatus.Running;
            await dbContext.SaveChangesAsync();

            BackupDeletionService service = new(
                dbContext,
                new ImmediateStorageOperationCoordinator());

            var result = await service.DeleteAsync(userId, backupId, CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(result.Deleted, Is.False);
                Assert.That(result.ErrorMessage, Is.Not.Null);
                Assert.That(dbContext.Backups.Count(), Is.EqualTo(1));
                Assert.That(dbContext.Schedules.Count(), Is.EqualTo(1));
                Assert.That(dbContext.Snapshots.Count(), Is.EqualTo(1));
                Assert.That(dbContext.SnapshotFiles.Count(), Is.EqualTo(1));
            });
        }

        [Test]
        public async Task DeleteAsync_WhenSnapshotArchiveIsActive_KeepsBackupRows()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using SqliteDbContext dbContext = await CreateDbContextAsync(connection);

            (Guid userId, Guid backupId, _, _, Guid snapshotId) = await SeedBackupAsync(dbContext);
            await dbContext.SnapshotArchiveJobs.AddAsync(new SnapshotArchiveJob
            {
                UserId = userId,
                SnapshotId = snapshotId,
                ActiveSnapshotId = snapshotId,
                Status = SnapshotArchiveStatus.Running,
                Phase = SnapshotArchivePhase.Streaming,
                RunId = Guid.NewGuid(),
                StartedAt = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
            BackupDeletionService service = new(
                dbContext,
                new ImmediateStorageOperationCoordinator());

            BackupDeletionResult result = await service.DeleteAsync(
                userId,
                backupId,
                CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(result.Deleted, Is.False);
                Assert.That(result.ErrorMessage, Does.Contain("archive"));
                Assert.That(dbContext.Backups.Count(), Is.EqualTo(1));
                Assert.That(dbContext.Snapshots.Count(), Is.EqualTo(1));
                Assert.That(dbContext.SnapshotArchiveJobs.Count(), Is.EqualTo(1));
            });
        }

        [Test]
        public async Task DeleteSnapshotAsync_WhenSnapshotHasFiles_RemovesSnapshotMetadataOnly()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using SqliteDbContext dbContext = await CreateDbContextAsync(connection);

            (Guid userId, Guid backupId, _, Guid storageId, Guid snapshotId) =
                await SeedBackupAsync(dbContext);
            await SeedArchiveHistoryAsync(dbContext, userId, snapshotId);
            ImmediateStorageOperationCoordinator coordinator = new();
            SnapshotDeletionService service = new(
                dbContext,
                coordinator);

            SnapshotDeletionResult result = await service.DeleteAsync(
                userId,
                snapshotId,
                CancellationToken.None);

            dbContext.ChangeTracker.Clear();

            Assert.Multiple(() =>
            {
                Assert.That(result.Deleted, Is.True);
                Assert.That(result.BackupId, Is.EqualTo(backupId));
                Assert.That(result.DeletedSnapshotFiles, Is.EqualTo(1));
                Assert.That(result.DeletedSnapshotFileBytes, Is.EqualTo(20));
                Assert.That(dbContext.Backups.Count(), Is.EqualTo(1));
                Assert.That(dbContext.Schedules.Count(), Is.EqualTo(1));
                Assert.That(dbContext.Snapshots.Count(), Is.Zero);
                Assert.That(dbContext.SnapshotFiles.Count(), Is.Zero);
                Assert.That(dbContext.SnapshotChunkReferences.Count(), Is.Zero);
                Assert.That(dbContext.SnapshotArchiveJobs.Count(), Is.Zero);
                Assert.That(dbContext.DownloadTickets.Count(), Is.Zero);
                Assert.That(dbContext.UploadedHashes.Count(), Is.EqualTo(1));
                Assert.That(coordinator.RequestedStorageId, Is.EqualTo(storageId));
                Assert.That(coordinator.RequestedKind, Is.EqualTo(StorageOperationKind.Maintenance));
                Assert.That(coordinator.Lease?.Disposed, Is.True);
            });
        }

        [Test]
        public async Task DeleteSnapshotAsync_WhenStorageIsBusy_KeepsSnapshotRows()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using SqliteDbContext dbContext = await CreateDbContextAsync(connection);
            (Guid userId, _, _, _, Guid snapshotId) = await SeedBackupAsync(dbContext);
            ImmediateStorageOperationCoordinator coordinator = new()
            {
                RejectAcquisition = true
            };
            SnapshotDeletionService service = new(dbContext, coordinator);

            SnapshotDeletionResult result = await service.DeleteAsync(
                userId,
                snapshotId,
                CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(result.Deleted, Is.False);
                Assert.That(result.ErrorMessage, Does.Contain("busy"));
                Assert.That(coordinator.RequestedKind, Is.EqualTo(StorageOperationKind.Maintenance));
                Assert.That(dbContext.Snapshots.Count(), Is.EqualTo(1));
                Assert.That(dbContext.SnapshotFiles.Count(), Is.EqualTo(1));
            });
        }

        [Test]
        public async Task DeleteSnapshotAsync_WhenArchiveIsActive_KeepsSnapshotRows()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using SqliteDbContext dbContext = await CreateDbContextAsync(connection);

            (Guid userId, _, _, _, Guid snapshotId) = await SeedBackupAsync(dbContext);
            await dbContext.SnapshotArchiveJobs.AddAsync(new SnapshotArchiveJob
            {
                UserId = userId,
                SnapshotId = snapshotId,
                ActiveSnapshotId = snapshotId,
                Status = SnapshotArchiveStatus.Pending,
                Phase = SnapshotArchivePhase.Waiting,
                StartedAt = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
            SnapshotDeletionService service = new(
                dbContext,
                new ImmediateStorageOperationCoordinator());

            var result = await service.DeleteAsync(
                userId,
                snapshotId,
                CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(result.Deleted, Is.False);
                Assert.That(result.ErrorMessage, Does.Contain("archive"));
                Assert.That(dbContext.Snapshots.Count(), Is.EqualTo(1));
                Assert.That(dbContext.SnapshotFiles.Count(), Is.EqualTo(1));
                Assert.That(dbContext.SnapshotArchiveJobs.Count(), Is.EqualTo(1));
            });
        }

        [Test]
        public async Task DeleteSnapshotAsync_WhenUserDoesNotOwnSnapshot_KeepsSnapshotRows()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using SqliteDbContext dbContext = await CreateDbContextAsync(connection);

            (_, _, _, _, Guid snapshotId) = await SeedBackupAsync(dbContext);
            SnapshotDeletionService service = new(
                dbContext,
                new ImmediateStorageOperationCoordinator());

            var result = await service.DeleteAsync(Guid.NewGuid(), snapshotId, CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(result.Deleted, Is.False);
                Assert.That(result.ErrorMessage, Is.Not.Null);
                Assert.That(dbContext.Snapshots.Count(), Is.EqualTo(1));
                Assert.That(dbContext.SnapshotFiles.Count(), Is.EqualTo(1));
            });
        }

        [Test]
        public async Task DeleteSnapshotAsync_WhenBackupIsRunning_KeepsSnapshotRows()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using SqliteDbContext dbContext = await CreateDbContextAsync(connection);

            (Guid userId, _, Guid scheduleId, _, Guid snapshotId) = await SeedBackupAsync(dbContext);
            Schedule schedule = await dbContext.Schedules.SingleAsync(x => x.Id == scheduleId);
            schedule.Status = ScheduleStatus.Running;
            await dbContext.SaveChangesAsync();
            SnapshotDeletionService service = new(
                dbContext,
                new ImmediateStorageOperationCoordinator());

            var result = await service.DeleteAsync(userId, snapshotId, CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(result.Deleted, Is.False);
                Assert.That(result.ErrorMessage, Is.Not.Null);
                Assert.That(dbContext.Snapshots.Count(), Is.EqualTo(1));
                Assert.That(dbContext.SnapshotFiles.Count(), Is.EqualTo(1));
            });
        }

        [Test]
        public async Task RunAsync_WhenStorageContainsOrphanChunk_DeletesOnlyUnreferencedChunk()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using SqliteDbContext dbContext = await CreateDbContextAsync(connection);

            (Guid userId, _, _, Guid storageId, _) = await SeedBackupAsync(dbContext);
            await dbContext.UploadedHashes.AddAsync(new UploadedHash
            {
                ModuleId = storageId,
                Hash = OrphanHash,
                OriginalSize = 20,
                StoredSize = 10,
                CompressionAlgorithm = CompressionHelpers.Algorithm
            });
            await dbContext.SaveChangesAsync();

            string referencedPath = ChunkStorageHelpers.GetStoragePath(ReferencedHash, '/');
            string orphanPath = ChunkStorageHelpers.GetStoragePath(OrphanHash, '/');
            TestStorage storage = new();
            storage.Files[referencedPath] = new BackupFileInfo
            {
                Path = referencedPath,
                Name = Path.GetFileName(referencedPath),
                Size = 12
            };
            storage.Files[orphanPath] = new BackupFileInfo
            {
                Path = orphanPath,
                Name = Path.GetFileName(orphanPath),
                Size = 10
            };
            StorageCleanupRunner runner = new(
                new TestCipher(),
                dbContext,
                NullLogger<StorageCleanupRunner>.Instance,
                [storage],
                CreateReferenceIndexer(dbContext));
            StorageCleanupJobState state = new(
                Guid.NewGuid(),
                userId,
                storageId,
                "storage",
                DateTime.UtcNow);
            ImmediateStorageOperationLease storageLease = new(storageId);

            await runner.RunAsync(
                state,
                (_, _) => Task.CompletedTask,
                (_, _) => Task.CompletedTask,
                storageLease,
                CancellationToken.None);

            dbContext.ChangeTracker.Clear();
            var result = state.Snapshot();

            Assert.Multiple(() =>
            {
                Assert.That(result.ChunkObjectsScanned, Is.EqualTo(2));
                Assert.That(result.ReferencedChunks, Is.EqualTo(1));
                Assert.That(result.OrphanObjects, Is.EqualTo(1));
                Assert.That(result.DeletedObjects, Is.EqualTo(1));
                Assert.That(result.FreedBytes, Is.EqualTo(10));
                Assert.That(result.UploadedHashRowsDeleted, Is.EqualTo(1));
                Assert.That(storage.DeletedPaths, Is.EqualTo(new[] { orphanPath }));
                Assert.That(dbContext.UploadedHashes.Select(x => x.Hash), Is.EqualTo(new[]
                {
                    ReferencedHash
                }));
            });
        }

        [Test]
        public async Task RunAsync_WhenInventoryCrossesLookupBatchBoundary_ProcessesEveryChunkExactly()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using SqliteDbContext dbContext = await CreateDbContextAsync(connection);

            (Guid userId, _, _, Guid storageId, _) = await SeedBackupAsync(dbContext);
            TestStorage storage = new();
            string referencedPath = ChunkStorageHelpers.GetStoragePath(ReferencedHash, '/');
            storage.Files[referencedPath] = new BackupFileInfo
            {
                Path = referencedPath,
                Name = Path.GetFileName(referencedPath),
                Size = 12
            };

            const int orphanCount = 500;
            List<UploadedHash> uploadedHashes = new(orphanCount);
            for (int index = 1; index <= orphanCount; index++)
            {
                string hash = index.ToString("x64");
                string path = ChunkStorageHelpers.GetStoragePath(hash, '/');
                storage.Files[path] = new BackupFileInfo
                {
                    Path = path,
                    Name = Path.GetFileName(path),
                    Size = index
                };
                uploadedHashes.Add(new UploadedHash
                {
                    ModuleId = storageId,
                    Hash = hash,
                    OriginalSize = index,
                    StoredSize = index,
                    CompressionAlgorithm = CompressionHelpers.Algorithm
                });
            }

            await dbContext.UploadedHashes.AddRangeAsync(uploadedHashes);
            await dbContext.SaveChangesAsync();

            StorageCleanupRunner runner = new(
                new TestCipher(),
                dbContext,
                NullLogger<StorageCleanupRunner>.Instance,
                [storage],
                CreateReferenceIndexer(dbContext));
            StorageCleanupJobState state = new(
                Guid.NewGuid(),
                userId,
                storageId,
                "storage",
                DateTime.UtcNow);

            await runner.RunAsync(
                state,
                (_, _) => Task.CompletedTask,
                (_, _) => Task.CompletedTask,
                new ImmediateStorageOperationLease(storageId),
                CancellationToken.None);

            dbContext.ChangeTracker.Clear();
            StorageCleanupJobDto result = state.Snapshot();
            List<string> remainingHashes = await dbContext.UploadedHashes
                .Select(x => x.Hash)
                .ToListAsync();

            Assert.Multiple(() =>
            {
                Assert.That(result.ChunkObjectsScanned, Is.EqualTo(orphanCount + 1));
                Assert.That(result.ReferencedObjects, Is.EqualTo(1));
                Assert.That(result.OrphanObjects, Is.EqualTo(orphanCount));
                Assert.That(result.DeletedObjects, Is.EqualTo(orphanCount));
                Assert.That(storage.Files.Keys, Is.EqualTo(new[] { referencedPath }));
                Assert.That(remainingHashes, Is.EqualTo(new[] { ReferencedHash }));
            });
        }

        [Test]
        public async Task RunAsync_WhenCanceledBeforePhysicalDelete_RemovesIndexAndKeepsOrphanObject()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using SqliteDbContext dbContext = await CreateDbContextAsync(connection);

            (Guid userId, _, _, Guid storageId, _) = await SeedBackupAsync(dbContext);
            await dbContext.UploadedHashes.AddAsync(new UploadedHash
            {
                ModuleId = storageId,
                Hash = OrphanHash,
                OriginalSize = 20,
                StoredSize = 10,
                CompressionAlgorithm = CompressionHelpers.Algorithm
            });
            await dbContext.SaveChangesAsync();

            string orphanPath = ChunkStorageHelpers.GetStoragePath(OrphanHash, '/');
            TestStorage storage = new();
            storage.Files[orphanPath] = new BackupFileInfo
            {
                Path = orphanPath,
                Name = Path.GetFileName(orphanPath),
                Size = 10
            };

            using CancellationTokenSource cancellationTokenSource = new();
            storage.DeleteOverride = (_, _) =>
            {
                cancellationTokenSource.Cancel();
                return Task.FromCanceled<bool?>(cancellationTokenSource.Token);
            };

            StorageCleanupRunner runner = new(
                new TestCipher(),
                dbContext,
                NullLogger<StorageCleanupRunner>.Instance,
                [storage],
                CreateReferenceIndexer(dbContext));
            StorageCleanupJobState state = new(
                Guid.NewGuid(),
                userId,
                storageId,
                "storage",
                DateTime.UtcNow);
            ImmediateStorageOperationLease storageLease = new(storageId);

            Assert.CatchAsync<OperationCanceledException>(async () =>
                await runner.RunAsync(
                    state,
                    (_, _) => Task.CompletedTask,
                    (_, _) => Task.CompletedTask,
                    storageLease,
                    cancellationTokenSource.Token));

            dbContext.ChangeTracker.Clear();
            List<string> indexedHashes = await dbContext.UploadedHashes
                .OrderBy(x => x.Hash)
                .Select(x => x.Hash)
                .ToListAsync();

            Assert.Multiple(() =>
            {
                Assert.That(indexedHashes, Is.EqualTo(new[] { ReferencedHash }));
                Assert.That(storage.Files.ContainsKey(orphanPath), Is.True);
                Assert.That(storage.DeletedPaths, Is.Empty);
                Assert.That(state.Snapshot().UploadedHashRowsDeleted, Is.EqualTo(1));
            });
        }

        [Test]
        [NonParallelizable]
        public async Task RunAsync_WhenInventoryExceedsTwoMillionObjects_KeepsMemoryBounded()
        {
            const int objectCount = 2_000_001;
            const long maximumRetainedGrowth = 64L * 1024 * 1024;
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using SqliteDbContext dbContext = await CreateDbContextAsync(connection);
            (Guid userId, _, _, Guid storageId, _) = await SeedBackupAsync(dbContext);
            GeneratedInventoryStorage storage = new(objectCount);
            StorageCleanupRunner runner = new(
                new TestCipher(),
                dbContext,
                NullLogger<StorageCleanupRunner>.Instance,
                [storage],
                CreateReferenceIndexer(dbContext));
            StorageCleanupJobState state = new(
                Guid.NewGuid(),
                userId,
                storageId,
                "storage",
                DateTime.UtcNow);
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(60));
            int checkpointCount = 0;
            long baselineMemory = GC.GetTotalMemory(true);
            long maximumMemory = baselineMemory;

            await runner.RunAsync(
                state,
                (_, _) => Task.CompletedTask,
                (_, _) =>
                {
                    checkpointCount++;
                    if (checkpointCount % 200 == 0)
                    {
                        maximumMemory = Math.Max(maximumMemory, GC.GetTotalMemory(false));
                    }
                    return Task.CompletedTask;
                },
                new ImmediateStorageOperationLease(storageId),
                timeout.Token);

            StorageCleanupJobDto result = state.Snapshot();
            long retainedMemory = GC.GetTotalMemory(true);
            maximumMemory = Math.Max(maximumMemory, retainedMemory);
            Assert.Multiple(() =>
            {
                Assert.That(storage.EnumeratedCount, Is.EqualTo(objectCount));
                Assert.That(result.StorageObjectsScanned, Is.EqualTo(objectCount));
                Assert.That(result.SkippedObjects, Is.EqualTo(objectCount));
                Assert.That(result.ChunkObjectsScanned, Is.Zero);
                Assert.That(result.CurrentPath, Is.Null);
                Assert.That(checkpointCount, Is.EqualTo(4_003));
                Assert.That(
                    maximumMemory - baselineMemory,
                    Is.LessThan(maximumRetainedGrowth));
                Assert.That(
                    retainedMemory - baselineMemory,
                    Is.LessThan(maximumRetainedGrowth));
            });
        }

        private static SnapshotChunkReferenceIndexer CreateReferenceIndexer(AppDbContext dbContext)
        {
            SnapshotChunkReferenceWriter writer = new(
                dbContext,
                NullLogger<SnapshotChunkReferenceWriter>.Instance);
            return new SnapshotChunkReferenceIndexer(dbContext, writer);
        }

        private static async Task SeedArchiveHistoryAsync(
            AppDbContext dbContext,
            Guid userId,
            Guid snapshotId)
        {
            SnapshotArchiveJob job = new()
            {
                UserId = userId,
                SnapshotId = snapshotId,
                Status = SnapshotArchiveStatus.Completed,
                Phase = SnapshotArchivePhase.Streaming,
                StartedAt = DateTime.UtcNow.AddMinutes(-1),
                FinishedAt = DateTime.UtcNow
            };
            await dbContext.SnapshotArchiveJobs.AddAsync(job);
            await dbContext.SaveChangesAsync();
            await dbContext.DownloadTickets.AddAsync(new DownloadTicket
            {
                UserId = userId,
                TokenHash = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
                Kind = DownloadTicketKind.SnapshotArchiveJob,
                ResourceId = job.Id,
                ExpiresAt = DateTime.UtcNow.AddMinutes(1)
            });
            await dbContext.SaveChangesAsync();
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

        private static async Task<(Guid UserId, Guid BackupId, Guid ScheduleId, Guid StorageId, Guid SnapshotId)> SeedBackupAsync(
            AppDbContext dbContext)
        {
            User user = new()
            {
                Username = "user",
                PasswordPhc = "password"
            };

            Module source = new()
            {
                User = user,
                Tag = "source",
                BackupModuleId = nameof(TestSource),
                Destination = ModuleDestination.Source
            };

            Module storage = new()
            {
                User = user,
                Tag = "storage",
                BackupModuleId = nameof(TestStorage),
                Destination = ModuleDestination.Target
            };

            Backup backup = new()
            {
                Source = source,
                Storage = storage,
                Tag = "backup"
            };

            Schedule schedule = new()
            {
                Backup = backup,
                StartAt = DateTime.UtcNow.AddMinutes(-1),
                Status = ScheduleStatus.Completed,
                FinishedAt = DateTime.UtcNow
            };

            Snapshot snapshot = new()
            {
                Backup = backup,
                CompletedAt = DateTime.UtcNow,
                FilesCount = 1,
                TotalSize = 20
            };

            SnapshotFile snapshotFile = new()
            {
                Snapshot = snapshot,
                Path = "file.txt",
                Name = "file.txt",
                Size = 20,
                Hashsum = ReferencedHash,
                ChunkHashes = [ReferencedHash]
            };

            UploadedHash uploadedHash = new()
            {
                Module = storage,
                Hash = ReferencedHash,
                OriginalSize = 20,
                StoredSize = 12,
                CompressionAlgorithm = CompressionHelpers.Algorithm
            };

            await dbContext.Users.AddAsync(user);
            await dbContext.Modules.AddRangeAsync(source, storage);
            await dbContext.SaveChangesAsync();

            backup.UserId = user.Id;
            await dbContext.Backups.AddAsync(backup);
            await dbContext.Schedules.AddAsync(schedule);
            await dbContext.Snapshots.AddAsync(snapshot);
            await dbContext.SnapshotFiles.AddAsync(snapshotFile);
            await dbContext.UploadedHashes.AddAsync(uploadedHash);
            await dbContext.SaveChangesAsync();
            await dbContext.SnapshotChunkReferences.AddAsync(new SnapshotChunkReference
            {
                StorageId = storage.Id,
                SnapshotId = snapshot.Id,
                SnapshotFileId = snapshotFile.Id,
                Ordinal = 0,
                ChunkHash = ReferencedHash
            });
            snapshotFile.ChunkReferencesIndexed = true;
            await dbContext.SaveChangesAsync();

            return (user.Id, backup.Id, schedule.Id, storage.Id, snapshot.Id);
        }

        private class TestSource
        {
        }

        private class TestStorage : IBackupStorage, IBackupStorageInventory
        {
            public string Id => nameof(TestStorage);
            public string Name => nameof(TestStorage);
            public char PathSeparator => '/';
            public IEnumerable<string> RequiredParameters => [];
            public List<string> DeletedPaths { get; } = [];
            public Dictionary<string, BackupFileInfo> Files { get; } = new(StringComparer.Ordinal);
            public Func<string, CancellationToken, Task<bool?>>? DeleteOverride { get; set; }

            public void SetParameters(IReadOnlyDictionary<string, string> parameters)
            {
            }

            public void SetIgnoredPaths(ICollection<string>? ignoredPaths)
            {
            }

            public Task<BackupFileInfo?> GetFileInfoAsync(string path, CancellationToken cancellationToken) =>
                Task.FromResult<BackupFileInfo?>(null);

            public Task<Stream> GetFileStreamAsync(BackupFileInfo file, CancellationToken cancellationToken = default) =>
                Task.FromResult<Stream>(Stream.Null);

            public IEnumerable<string> GetDirectories(bool recursive = false, CancellationToken cancellationToken = default) => [];

            public IEnumerable<BackupFileInfo> GetFiles(bool recursive = false, CancellationToken cancellationToken = default) =>
                Files.Values;

            public async IAsyncEnumerable<BackupFileInfo> GetFilesAsync(
                bool recursive = false,
                [EnumeratorCancellation] CancellationToken cancellationToken = default)
            {
                await foreach (BackupFileInfo file in GetFilesAfterAsync(
                    null,
                    recursive,
                    cancellationToken))
                {
                    yield return file;
                }
            }

            public async IAsyncEnumerable<BackupFileInfo> GetFilesAfterAsync(
                string? afterPath,
                bool recursive = false,
                [EnumeratorCancellation] CancellationToken cancellationToken = default)
            {
                foreach (BackupFileInfo file in Files.Values
                    .Where(x => string.IsNullOrEmpty(afterPath) ||
                        string.CompareOrdinal(x.Path, afterPath) > 0)
                    .OrderBy(x => x.Path, StringComparer.Ordinal))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    yield return file;
                    await Task.Yield();
                }
            }

            public Task<bool?> ExistsAsync(string path, CancellationToken cancellationToken = default) =>
                Task.FromResult<bool?>(Files.ContainsKey(path));

            public async Task<bool?> DeleteAsync(string path, CancellationToken cancellationToken = default)
            {
                if (DeleteOverride is not null)
                {
                    return await DeleteOverride(path, cancellationToken);
                }

                DeletedPaths.Add(path);
                return Files.Remove(path);
            }

            public Task UploadAsync(string path, Stream data, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;
        }

        private class GeneratedInventoryStorage(int objectCount) :
            IBackupStorage,
            IBackupStorageInventory
        {
            private const string MetadataPrefix = "metadata/";

            public string Id => nameof(TestStorage);
            public string Name => nameof(GeneratedInventoryStorage);
            public char PathSeparator => '/';
            public IEnumerable<string> RequiredParameters => [];
            public int EnumeratedCount { get; private set; }

            public void SetParameters(IReadOnlyDictionary<string, string> parameters)
            {
            }

            public void SetIgnoredPaths(ICollection<string>? ignoredPaths)
            {
            }

            public Task<BackupFileInfo?> GetFileInfoAsync(
                string path,
                CancellationToken cancellationToken) =>
                Task.FromResult<BackupFileInfo?>(null);

            public Task<Stream> GetFileStreamAsync(
                BackupFileInfo file,
                CancellationToken cancellationToken = default) =>
                Task.FromResult<Stream>(Stream.Null);

            public IEnumerable<string> GetDirectories(
                bool recursive = false,
                CancellationToken cancellationToken = default) => [];

            public IEnumerable<BackupFileInfo> GetFiles(
                bool recursive = false,
                CancellationToken cancellationToken = default)
            {
                for (int index = 0; index < objectCount; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    yield return CreateFile(index);
                }
            }

            public IAsyncEnumerable<BackupFileInfo> GetFilesAsync(
                bool recursive = false,
                CancellationToken cancellationToken = default) =>
                GetFilesAfterAsync(null, recursive, cancellationToken);

            public async IAsyncEnumerable<BackupFileInfo> GetFilesAfterAsync(
                string? afterPath,
                bool recursive = false,
                [EnumeratorCancellation] CancellationToken cancellationToken = default)
            {
                int startIndex = afterPath is null
                    ? 0
                    : int.Parse(afterPath.AsSpan(MetadataPrefix.Length)) + 1;
                for (int index = startIndex; index < objectCount; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (index > startIndex && index % 10_000 == 0)
                    {
                        await Task.Yield();
                    }

                    EnumeratedCount++;
                    yield return CreateFile(index);
                }
            }

            public Task<bool?> ExistsAsync(
                string path,
                CancellationToken cancellationToken = default) =>
                Task.FromResult<bool?>(false);

            public Task<bool?> DeleteAsync(
                string path,
                CancellationToken cancellationToken = default) =>
                Task.FromResult<bool?>(false);

            public Task UploadAsync(
                string path,
                Stream data,
                CancellationToken cancellationToken = default) =>
                Task.CompletedTask;

            private static BackupFileInfo CreateFile(int index)
            {
                string path = MetadataPrefix + index.ToString("D7");
                return new BackupFileInfo
                {
                    Path = path,
                    Name = Path.GetFileName(path),
                    Size = 1
                };
            }
        }

        private class TestCipher : IStreamCipher
        {
            public async Task EncryptAsync(
                Stream input,
                Stream output,
                int chunkSize,
                bool leaveInputOpen,
                bool leaveOutputOpen,
                CancellationToken ct)
            {
                await input.CopyToAsync(output, ct);
            }

            public async Task DecryptAsync(
                Stream input,
                Stream output,
                bool leaveInputOpen,
                bool leaveOutputOpen,
                CancellationToken ct)
            {
                await input.CopyToAsync(output, ct);
            }

            public Task<Stream> EncryptAsync(Stream input, int chunkSize, bool leaveOpen, CancellationToken ct) =>
                Task.FromResult(input);

            public Task<Stream> DecryptAsync(Stream input, bool leaveOpen, CancellationToken ct) =>
                Task.FromResult(input);
        }

        private class ImmediateStorageOperationLease : IStorageOperationLease
        {
            public ImmediateStorageOperationLease(Guid storageId)
            {
                StorageId = storageId;
            }

            public Guid OperationId { get; } = Guid.NewGuid();
            public Guid StorageId { get; }
            public CancellationToken LeaseLostToken => CancellationToken.None;
            public bool Disposed { get; private set; }

            public Task EnsureOwnedAsync(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            }

            public ValueTask DisposeAsync()
            {
                Disposed = true;
                return ValueTask.CompletedTask;
            }
        }

        private class ImmediateStorageOperationCoordinator : IStorageOperationCoordinator
        {
            public Guid? RequestedStorageId { get; private set; }
            public StorageOperationKind? RequestedKind { get; private set; }
            public ImmediateStorageOperationLease? Lease { get; private set; }
            public bool RejectAcquisition { get; init; }

            public Task<IStorageOperationLease?> TryAcquireAsync(
                Guid storageId,
                StorageOperationKind kind,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                RequestedStorageId = storageId;
                RequestedKind = kind;
                if (RejectAcquisition)
                {
                    return Task.FromResult<IStorageOperationLease?>(null);
                }

                Lease = new ImmediateStorageOperationLease(storageId);
                return Task.FromResult<IStorageOperationLease?>(Lease);
            }
        }
    }
}
