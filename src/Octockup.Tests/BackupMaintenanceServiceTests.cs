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
using Octockup.Server.Models.Enums;
using Octockup.Server.Services;

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

            (Guid userId, Guid backupId, _, _) = await SeedBackupAsync(dbContext);
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
                Assert.That(dbContext.UploadedHashes.Count(), Is.EqualTo(1));
            });
        }

        [Test]
        public async Task DeleteAsync_WhenBackupIsRunning_KeepsBackupRows()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using SqliteDbContext dbContext = await CreateDbContextAsync(connection);

            (Guid userId, Guid backupId, Guid scheduleId, _) = await SeedBackupAsync(dbContext);
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
        public async Task CollectAsync_RemovesOnlyUnreferencedUploadedHashes()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using SqliteDbContext dbContext = await CreateDbContextAsync(connection);

            (Guid userId, _, _, Guid storageId) = await SeedBackupAsync(dbContext);
            await dbContext.UploadedHashes.AddAsync(new UploadedHash
            {
                ModuleId = storageId,
                Hash = OrphanHash,
                OriginalSize = 20,
                StoredSize = 10,
                CompressionAlgorithm = CompressionHelpers.Algorithm
            });
            await dbContext.SaveChangesAsync();

            TestStorage storage = new();
            StorageGarbageCollectionService service = new(
                new TestCipher(),
                dbContext,
                NullLogger<StorageGarbageCollectionService>.Instance,
                [storage],
                new ChunkReferenceCollector(dbContext));

            var result = await service.CollectAsync(userId, storageId, CancellationToken.None);

            dbContext.ChangeTracker.Clear();

            Assert.Multiple(() =>
            {
                Assert.That(result.UploadedHashesScanned, Is.EqualTo(2));
                Assert.That(result.ReferencedChunks, Is.EqualTo(1));
                Assert.That(result.OrphanChunks, Is.EqualTo(1));
                Assert.That(result.DeletedObjects, Is.EqualTo(1));
                Assert.That(result.FreedStoredSize, Is.EqualTo(10));
                Assert.That(storage.DeletedPaths, Is.EqualTo(new[]
                {
                    ChunkStorageHelpers.GetStoragePath(OrphanHash, storage.PathSeparator)
                }));
                Assert.That(dbContext.UploadedHashes.Select(x => x.Hash), Is.EqualTo(new[]
                {
                    ReferencedHash
                }));
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

        private static async Task<(Guid UserId, Guid BackupId, Guid ScheduleId, Guid StorageId)> SeedBackupAsync(
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
            await dbContext.Backups.AddAsync(backup);
            await dbContext.Schedules.AddAsync(schedule);
            await dbContext.Snapshots.AddAsync(snapshot);
            await dbContext.SnapshotFiles.AddAsync(snapshotFile);
            await dbContext.UploadedHashes.AddAsync(uploadedHash);
            await dbContext.SaveChangesAsync();

            return (user.Id, backup.Id, schedule.Id, storage.Id);
        }

        private class TestSource
        {
        }

        private class TestStorage : IBackupStorage
        {
            public string Id => nameof(TestStorage);
            public string Name => nameof(TestStorage);
            public char PathSeparator => '/';
            public IEnumerable<string> RequiredParameters => [];
            public List<string> DeletedPaths { get; } = [];

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

            public IEnumerable<BackupFileInfo> GetFiles(bool recursive = false, CancellationToken cancellationToken = default) => [];

            public Task<bool?> ExistsAsync(string path, CancellationToken cancellationToken = default) =>
                Task.FromResult<bool?>(false);

            public Task<bool?> DeleteAsync(string path, CancellationToken cancellationToken = default)
            {
                DeletedPaths.Add(path);
                return Task.FromResult<bool?>(true);
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
    }
}
