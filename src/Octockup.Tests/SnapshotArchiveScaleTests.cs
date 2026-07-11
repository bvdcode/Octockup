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
using Octockup.Server.Models.Enums;
using Octockup.Server.Services;
using System.Security.Cryptography;

namespace Octockup.Tests
{
    public class SnapshotArchiveScaleTests
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
        [NonParallelizable]
        public async Task WriteAsync_WithTenThousandFiles_KeepsMetadataMemoryBounded()
        {
            const int fileCount = 10_000;
            const long maximumMemoryGrowth = 96L * 1024 * 1024;
            TestStorage storageProvider = new();
            User user = new()
            {
                Username = "archive-scale-user",
                PasswordPhc = "password"
            };
            Module source = CreateModule(
                user,
                "archive-scale-source",
                ModuleDestination.Source,
                "source-provider");
            Module storage = CreateModule(
                user,
                "archive-scale-storage",
                ModuleDestination.Target,
                storageProvider.Id);
            await _dbContext.AddRangeAsync(user, source, storage);
            await _dbContext.SaveChangesAsync();

            byte[] content = [0x5A];
            string contentHash = Convert
                .ToHexString(SHA256.HashData(content))
                .ToLowerInvariant();
            string chunkKey = ChunkStorageHelpers.CreateKey(
                contentHash,
                CompressionAlgorithm.None,
                false);
            string storagePath = ChunkStorageHelpers.GetStoragePath(
                chunkKey,
                storageProvider.PathSeparator);
            storageProvider.Files[storagePath] = new BackupFileInfo
            {
                Path = storagePath,
                Name = chunkKey,
                Size = content.Length
            };
            storageProvider.Contents[storagePath] = content;

            Backup backup = new()
            {
                UserId = user.Id,
                SourceId = source.Id,
                StorageId = storage.Id,
                Tag = "archive-scale-backup"
            };
            Snapshot snapshot = new()
            {
                Backup = backup,
                CompletedAt = DateTime.UtcNow,
                FilesCount = fileCount,
                TotalSize = fileCount
            };
            UploadedHash uploadedHash = new()
            {
                ModuleId = storage.Id,
                Hash = chunkKey,
                StoredSize = content.Length,
                OriginalSize = content.Length,
                CompressionAlgorithm = CompressionAlgorithm.None
            };
            await _dbContext.AddRangeAsync(backup, snapshot, uploadedHash);
            await _dbContext.SaveChangesAsync();

            const int seedBatchSize = 500;
            for (int start = 0; start < fileCount; start += seedBatchSize)
            {
                int count = Math.Min(seedBatchSize, fileCount - start);
                List<SnapshotFile> files = new(count);
                for (int offset = 0; offset < count; offset++)
                {
                    int index = start + offset;
                    SnapshotFile file = new()
                    {
                        SnapshotId = snapshot.Id,
                        Path = $"archive/{index:D7}.bin",
                        Name = $"{index:D7}.bin",
                        Size = content.Length,
                        Hashsum = contentHash,
                        ChunkHashes = [chunkKey],
                        ChunkReferencesIndexed = true
                    };
                    files.Add(file);
                }

                await _dbContext.SnapshotFiles.AddRangeAsync(files);
                await _dbContext.SaveChangesAsync();
                List<SnapshotChunkReference> references = files
                    .Select(file => new SnapshotChunkReference
                    {
                        StorageId = storage.Id,
                        SnapshotId = snapshot.Id,
                        SnapshotFileId = file.Id,
                        Ordinal = 0,
                        ChunkHash = chunkKey
                    })
                    .ToList();
                await _dbContext.SnapshotChunkReferences.AddRangeAsync(references);
                await _dbContext.SaveChangesAsync();
                _dbContext.ChangeTracker.Clear();
            }

            Guid runId = Guid.NewGuid();
            SnapshotArchiveJob job = new()
            {
                UserId = user.Id,
                SnapshotId = snapshot.Id,
                ActiveSnapshotId = snapshot.Id,
                RunId = runId,
                Status = SnapshotArchiveStatus.Running,
                Phase = SnapshotArchivePhase.Preparing,
                StartedAt = DateTime.UtcNow,
                TotalFiles = fileCount,
                TotalBytes = fileCount
            };
            await _dbContext.SnapshotArchiveJobs.AddAsync(job);
            await _dbContext.SaveChangesAsync();
            _dbContext.ChangeTracker.Clear();

            RecordingSnapshotArchiveProgressPublisher publisher = new();
            SnapshotArchiveJobService jobs = new(
                _dbContext,
                TimeProvider.System,
                new SnapshotArchiveCancellationRegistry(
                    NullLogger<SnapshotArchiveCancellationRegistry>.Instance),
                publisher,
                NullLogger<SnapshotArchiveJobService>.Instance);
            SnapshotChunkReferenceWriter referenceWriter = new(
                _dbContext,
                NullLogger<SnapshotChunkReferenceWriter>.Instance);
            SnapshotArchiveRunner runner = new(
                new TestCipher(),
                _dbContext,
                new SnapshotChunkReferenceIndexer(_dbContext, referenceWriter),
                NullLogger<SnapshotArchiveRunner>.Instance,
                new IBackupProvider[] { storageProvider });
            SnapshotArchiveProgressTracker progress = new(
                job,
                runId,
                jobs,
                TimeProvider.System);
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(60));
            await using ManagedMemorySampler memory = new();

            await runner.WriteAsync(
                job,
                progress,
                Stream.Null,
                timeout.Token);
            await memory.StopAsync();

            SnapshotArchiveJob persisted = await _dbContext.SnapshotArchiveJobs
                .AsNoTracking()
                .SingleAsync(x => x.Id == job.Id);
            Assert.Multiple(() =>
            {
                Assert.That(persisted.ProcessedFiles, Is.EqualTo(fileCount));
                Assert.That(persisted.ProcessedBytes, Is.EqualTo(fileCount));
                Assert.That(persisted.PreparedChunkReferences, Is.EqualTo(fileCount));
                Assert.That(_dbContext.ChangeTracker.Entries(), Is.Empty);
                Assert.That(memory.MaximumGrowthBytes, Is.LessThan(maximumMemoryGrowth));
                Assert.That(memory.RetainedGrowthBytes, Is.LessThan(maximumMemoryGrowth));
            });
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
    }
}
