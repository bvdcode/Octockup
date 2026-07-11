// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

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
using Octockup.Server.Streams;
using System.Security.Cryptography;

namespace Octockup.Tests
{
    public class StorageCleanupRestoreIntegrityTests
    {
        private SqliteConnection _connection = null!;
        private SqliteDbContext _dbContext = null!;

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
        }

        [TearDown]
        public async Task TearDown()
        {
            await _dbContext.DisposeAsync();
            await _connection.DisposeAsync();
        }

        [Test]
        public async Task CleanupAfterSnapshotDeletion_RestoresEveryRetainedFileExactly()
        {
            TestCipher cipher = new();
            TestStorage storageProvider = new();
            User user = new()
            {
                Username = "cleanup-restore-user",
                PasswordPhc = "password"
            };
            Module source = CreateModule(
                user,
                "cleanup-restore-source",
                ModuleDestination.Source,
                "source-provider");
            Module storage = CreateModule(
                user,
                "cleanup-restore-storage",
                ModuleDestination.Target,
                storageProvider.Id);
            await _dbContext.AddRangeAsync(user, source, storage);
            await _dbContext.SaveChangesAsync();
            Backup backup = new()
            {
                UserId = user.Id,
                Source = source,
                Storage = storage,
                Tag = "cleanup-restore-backup"
            };
            Snapshot retainedSnapshot = new()
            {
                Backup = backup,
                CompletedAt = DateTime.UtcNow.AddMinutes(-1)
            };
            Snapshot deletedSnapshot = new()
            {
                Backup = backup,
                CompletedAt = DateTime.UtcNow
            };
            await _dbContext.AddRangeAsync(
                backup,
                retainedSnapshot,
                deletedSnapshot);

            (string sharedKey, _, byte[] sharedContent) = await AddChunkAsync(
                storage,
                storageProvider,
                "shared:"u8.ToArray());
            (string alphaKey, _, byte[] alphaContent) = await AddChunkAsync(
                storage,
                storageProvider,
                "alpha"u8.ToArray());
            (string betaKey, _, byte[] betaContent) = await AddChunkAsync(
                storage,
                storageProvider,
                "beta"u8.ToArray());
            (string orphanKey, string orphanPath, byte[] orphanContent) = await AddChunkAsync(
                storage,
                storageProvider,
                "deleted-only"u8.ToArray());

            SnapshotFile alphaFile = AddSnapshotFile(
                storage,
                retainedSnapshot,
                "retained/alpha.txt",
                [sharedKey, alphaKey],
                sharedContent.Length + alphaContent.Length);
            SnapshotFile betaFile = AddSnapshotFile(
                storage,
                retainedSnapshot,
                "retained/beta.txt",
                [sharedKey, betaKey],
                sharedContent.Length + betaContent.Length);
            AddSnapshotFile(
                storage,
                deletedSnapshot,
                "deleted/orphan.txt",
                [orphanKey],
                orphanContent.Length);
            retainedSnapshot.FilesCount = 2;
            retainedSnapshot.TotalSize = alphaFile.Size + betaFile.Size;
            deletedSnapshot.FilesCount = 1;
            deletedSnapshot.TotalSize = orphanContent.Length;
            await _dbContext.SaveChangesAsync();
            _dbContext.ChangeTracker.Clear();

            SnapshotDeletionService deletion = new(
                _dbContext,
                new ImmediateOperationCoordinator());
            SnapshotDeletionResult deletionResult = await deletion.DeleteAsync(
                user.Id,
                deletedSnapshot.Id,
                CancellationToken.None);
            Assert.That(
                deletionResult.Deleted,
                Is.True,
                deletionResult.ErrorMessage);

            StorageCleanupRunner runner = new(
                cipher,
                _dbContext,
                NullLogger<StorageCleanupRunner>.Instance,
                new IBackupProvider[] { storageProvider },
                CreateReferenceIndexer());
            StorageCleanupJobState state = new(
                Guid.NewGuid(),
                user.Id,
                storage.Id,
                storage.Tag,
                DateTime.UtcNow);
            long lastScanned = 0;
            bool progressRegressed = false;
            int progressEvents = 0;
            await using ImmediateLease cleanupLease = new(storage.Id);

            await runner.RunAsync(
                state,
                (progress, _) =>
                {
                    RecordProgress(progress);
                    return Task.CompletedTask;
                },
                (progress, _) =>
                {
                    RecordProgress(progress);
                    return Task.CompletedTask;
                },
                cleanupLease,
                CancellationToken.None);

            StorageCleanupJobDto cleanup = state.Snapshot();
            List<string> indexedHashes = await _dbContext.UploadedHashes
                .AsNoTracking()
                .OrderBy(x => x.Hash)
                .Select(x => x.Hash)
                .ToListAsync();
            Dictionary<string, byte[]> expectedFiles = new(StringComparer.Ordinal)
            {
                [alphaFile.Path] = [.. sharedContent, .. alphaContent],
                [betaFile.Path] = [.. sharedContent, .. betaContent]
            };
            List<SnapshotFile> retainedFiles = await _dbContext.SnapshotFiles
                .AsNoTracking()
                .Where(x => x.SnapshotId == retainedSnapshot.Id)
                .OrderBy(x => x.Path)
                .ToListAsync();

            foreach (SnapshotFile file in retainedFiles)
            {
                SnapshotChunkDescriptorReader chunks = new(
                    _dbContext,
                    storage.Id,
                    file.Id,
                    NullLogger.Instance);
                await using SnapshotConcatStream restored = new(
                    NullLogger.Instance,
                    storageProvider,
                    chunks.ReadNextAsync,
                    file,
                    cipher,
                    file.Size,
                    CancellationToken.None);
                using MemoryStream output = new();
                await restored.CopyToAsync(output);
                Assert.That(output.ToArray(), Is.EqualTo(expectedFiles[file.Path]));
            }

            Assert.Multiple(() =>
            {
                Assert.That(progressRegressed, Is.False);
                Assert.That(progressEvents, Is.GreaterThan(2));
                Assert.That(cleanup.StorageObjectsScanned, Is.EqualTo(4));
                Assert.That(cleanup.ChunkObjectsScanned, Is.EqualTo(4));
                Assert.That(cleanup.ReferencedChunks, Is.EqualTo(3));
                Assert.That(cleanup.ReferencedObjects, Is.EqualTo(3));
                Assert.That(cleanup.OrphanObjects, Is.EqualTo(1));
                Assert.That(cleanup.DeletedObjects, Is.EqualTo(1));
                Assert.That(cleanup.FreedBytes, Is.EqualTo(orphanContent.Length));
                Assert.That(cleanup.FailedDeletes, Is.Zero);
                Assert.That(cleanup.CurrentPath, Is.Null);
                Assert.That(storageProvider.Files.ContainsKey(orphanPath), Is.False);
                Assert.That(storageProvider.Contents.ContainsKey(orphanPath), Is.False);
                Assert.That(indexedHashes, Is.EquivalentTo(new[]
                {
                    sharedKey,
                    alphaKey,
                    betaKey
                }));
                Assert.That(_dbContext.Snapshots.Count(), Is.EqualTo(1));
                Assert.That(retainedFiles, Has.Count.EqualTo(2));
                Assert.That(cleanupLease.EnsureOwnedCount, Is.GreaterThan(0));
            });

            void RecordProgress(StorageCleanupJobDto progress)
            {
                if (progress.StorageObjectsScanned < lastScanned)
                {
                    progressRegressed = true;
                }

                lastScanned = progress.StorageObjectsScanned;
                progressEvents++;
            }
        }

        private async Task<(string Key, string Path, byte[] Content)> AddChunkAsync(
            Module storage,
            TestStorage storageProvider,
            byte[] content)
        {
            string hash = Convert
                .ToHexString(SHA256.HashData(content))
                .ToLowerInvariant();
            string key = ChunkStorageHelpers.CreateKey(
                hash,
                CompressionAlgorithm.None,
                false);
            string path = ChunkStorageHelpers.GetStoragePath(
                key,
                storageProvider.PathSeparator);
            storageProvider.Files[path] = new BackupFileInfo
            {
                Path = path,
                Name = key,
                Size = content.Length
            };
            storageProvider.Contents[path] = content;
            await _dbContext.UploadedHashes.AddAsync(new UploadedHash
            {
                Module = storage,
                Hash = key,
                StoredSize = content.Length,
                OriginalSize = content.Length,
                CompressionAlgorithm = CompressionAlgorithm.None
            });
            return (key, path, content);
        }

        private SnapshotFile AddSnapshotFile(
            Module storage,
            Snapshot snapshot,
            string path,
            IReadOnlyList<string> chunkKeys,
            long size)
        {
            SnapshotFile file = new()
            {
                Snapshot = snapshot,
                Path = path,
                Name = Path.GetFileName(path),
                Size = size,
                Hashsum = string.Empty,
                ChunkHashes = chunkKeys.ToList(),
                ChunkReferencesIndexed = true
            };
            _dbContext.SnapshotFiles.Add(file);
            for (int ordinal = 0; ordinal < chunkKeys.Count; ordinal++)
            {
                _dbContext.SnapshotChunkReferences.Add(new SnapshotChunkReference
                {
                    StorageId = storage.Id,
                    SnapshotId = snapshot.Id,
                    SnapshotFileId = file.Id,
                    Ordinal = ordinal,
                    ChunkHash = chunkKeys[ordinal]
                });
            }

            return file;
        }

        private SnapshotChunkReferenceIndexer CreateReferenceIndexer()
        {
            SnapshotChunkReferenceWriter writer = new(
                _dbContext,
                NullLogger<SnapshotChunkReferenceWriter>.Instance);
            return new SnapshotChunkReferenceIndexer(_dbContext, writer);
        }

        private static Module CreateModule(
            User user,
            string tag,
            ModuleDestination destination,
            string providerId)
        {
            return new Module
            {
                User = user,
                Tag = tag,
                BackupModuleId = providerId,
                Destination = destination
            };
        }

        private class ImmediateOperationCoordinator : IStorageOperationCoordinator
        {
            public Task<IStorageOperationLease?> TryAcquireAsync(
                Guid storageId,
                StorageOperationKind kind,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult<IStorageOperationLease?>(new ImmediateLease(storageId));
            }
        }

        private class ImmediateLease(Guid storageId) : IStorageOperationLease
        {
            public Guid OperationId { get; } = Guid.NewGuid();
            public Guid StorageId { get; } = storageId;
            public CancellationToken LeaseLostToken => CancellationToken.None;
            public int EnsureOwnedCount { get; private set; }

            public Task EnsureOwnedAsync(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                EnsureOwnedCount++;
                return Task.CompletedTask;
            }

            public ValueTask DisposeAsync()
            {
                return ValueTask.CompletedTask;
            }
        }
    }
}
