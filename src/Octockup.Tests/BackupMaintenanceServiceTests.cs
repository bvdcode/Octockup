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

            (Guid userId, Guid backupId, _, _, _) = await SeedBackupAsync(dbContext);
            BackupDeletionService service = new(dbContext);

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
                Assert.That(dbContext.UploadedHashes.Count(), Is.EqualTo(1));
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

            BackupDeletionService service = new(dbContext);

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
        public async Task DeleteSnapshotAsync_WhenSnapshotHasFiles_RemovesSnapshotMetadataOnly()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using SqliteDbContext dbContext = await CreateDbContextAsync(connection);

            (Guid userId, Guid backupId, _, _, Guid snapshotId) = await SeedBackupAsync(dbContext);
            SnapshotDeletionService service = new(dbContext);

            var result = await service.DeleteAsync(userId, snapshotId, CancellationToken.None);

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
                Assert.That(dbContext.UploadedHashes.Count(), Is.EqualTo(1));
            });
        }

        [Test]
        public async Task DeleteSnapshotAsync_WhenUserDoesNotOwnSnapshot_KeepsSnapshotRows()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using SqliteDbContext dbContext = await CreateDbContextAsync(connection);

            (_, _, _, _, Guid snapshotId) = await SeedBackupAsync(dbContext);
            SnapshotDeletionService service = new(dbContext);

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
            SnapshotDeletionService service = new(dbContext);

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

        private static SnapshotChunkReferenceIndexer CreateReferenceIndexer(AppDbContext dbContext)
        {
            SnapshotChunkReferenceWriter writer = new(
                dbContext,
                NullLogger<SnapshotChunkReferenceWriter>.Instance);
            return new SnapshotChunkReferenceIndexer(dbContext, writer);
        }

        [Test]
        public async Task CollectWithReferenceCountForStorageAsync_WhenChunkHashesRepeat_ReturnsReferencesAndTotalCount()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using SqliteDbContext dbContext = await CreateDbContextAsync(connection);

            (Guid userId, Guid backupId, Guid scheduleId, Guid storageId, Guid snapshotId) seed =
                await SeedBackupAsync(dbContext);
            Snapshot snapshot = await dbContext.Snapshots.SingleAsync(x => x.BackupId == seed.backupId);
            SnapshotFile extraSnapshotFile = new()
            {
                SnapshotId = snapshot.Id,
                Path = "file2.txt",
                Name = "file2.txt",
                Size = 40,
                Hashsum = OrphanHash,
                ChunkHashes = [ReferencedHash, OrphanHash]
            };
            await dbContext.SnapshotFiles.AddAsync(extraSnapshotFile);
            await dbContext.SaveChangesAsync();

            List<(long SnapshotFilesScanned, long ReferenceCount, long ReferencedChunks)> progress = [];
            ChunkReferenceCollector collector = new(dbContext);

            (HashSet<string> references, long referenceCount) = await collector
                .CollectWithReferenceCountForStorageAsync(
                    seed.storageId,
                    CancellationToken.None,
                    (snapshotFilesScanned, currentReferenceCount, referencedChunkCount, _) =>
                    {
                        progress.Add((
                            snapshotFilesScanned,
                            currentReferenceCount,
                            referencedChunkCount));
                        return Task.CompletedTask;
                    });

            Assert.Multiple(() =>
            {
                Assert.That(referenceCount, Is.EqualTo(3));
                Assert.That(references, Is.EquivalentTo(new[]
                {
                    ReferencedHash,
                    OrphanHash
                }));
                Assert.That(progress, Has.Count.EqualTo(1));
                Assert.That(progress[0].SnapshotFilesScanned, Is.EqualTo(2));
                Assert.That(progress[0].ReferenceCount, Is.EqualTo(3));
                Assert.That(progress[0].ReferencedChunks, Is.EqualTo(2));
            });
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
                foreach (BackupFileInfo file in Files.Values)
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

            public Task EnsureOwnedAsync(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            }

            public ValueTask DisposeAsync()
            {
                return ValueTask.CompletedTask;
            }
        }
    }
}
