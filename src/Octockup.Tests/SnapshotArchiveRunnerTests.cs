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
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace Octockup.Tests
{
    public class SnapshotArchiveRunnerTests
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
        public async Task WriteAsync_StreamsMultipleFileBatchesAndPersistsProgress()
        {
            TestStorage storage = new();
            User user = new()
            {
                Username = "archive-runner-user",
                PasswordPhc = "password"
            };
            Module source = CreateModule(
                user,
                "archive-runner-source",
                ModuleDestination.Source,
                "source-provider");
            Module storageModule = CreateModule(
                user,
                "archive-runner-storage",
                ModuleDestination.Target,
                storage.Id);
            Backup backup = new()
            {
                UserId = user.Id,
                Source = source,
                Storage = storageModule,
                Tag = "archive-runner-backup"
            };
            Snapshot snapshot = new()
            {
                Backup = backup,
                CompletedAt = DateTime.UtcNow
            };
            await _dbContext.AddRangeAsync(
                user,
                source,
                storageModule,
                backup,
                snapshot);
            await _dbContext.SaveChangesAsync();

            const int fileCount = 55;
            long totalBytes = 0;
            Dictionary<string, string> expectedContents = new(StringComparer.Ordinal);
            for (int index = 0; index < fileCount; index++)
            {
                string contentText = $"archive-content-{index:D3}";
                byte[] content = Encoding.UTF8.GetBytes(contentText);
                string contentHash = Convert
                    .ToHexString(SHA256.HashData(content))
                    .ToLowerInvariant();
                string chunkKey = ChunkStorageHelpers.CreateKey(
                    contentHash,
                    CompressionAlgorithm.None,
                    false);
                string storagePath = ChunkStorageHelpers.GetStoragePath(
                    chunkKey,
                    storage.PathSeparator);
                storage.Files[storagePath] = new BackupFileInfo
                {
                    Path = storagePath,
                    Name = chunkKey,
                    Size = content.Length
                };
                storage.Contents[storagePath] = content;

                string path = $"folder/{index:D3}.txt";
                SnapshotFile file = new()
                {
                    SnapshotId = snapshot.Id,
                    Path = path,
                    Name = $"{index:D3}.txt",
                    Size = content.Length,
                    Hashsum = contentHash,
                    ChunkHashes = [chunkKey],
                    ChunkReferencesIndexed = true
                };
                await _dbContext.SnapshotFiles.AddAsync(file);
                await _dbContext.UploadedHashes.AddAsync(new UploadedHash
                {
                    ModuleId = storageModule.Id,
                    Hash = chunkKey,
                    StoredSize = content.Length,
                    OriginalSize = content.Length,
                    CompressionAlgorithm = CompressionAlgorithm.None
                });
                await _dbContext.SnapshotChunkReferences.AddAsync(
                    new SnapshotChunkReference
                    {
                        StorageId = storageModule.Id,
                        SnapshotId = snapshot.Id,
                        SnapshotFileId = file.Id,
                        Ordinal = 0,
                        ChunkHash = chunkKey
                    });
                expectedContents[path] = contentText;
                totalBytes += content.Length;
            }

            snapshot.FilesCount = fileCount;
            snapshot.TotalSize = totalBytes;
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
                TotalBytes = totalBytes
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
            SnapshotChunkReferenceIndexer referenceIndexer = new(
                _dbContext,
                referenceWriter);
            SnapshotArchiveRunner runner = new(
                new TestCipher(),
                _dbContext,
                referenceIndexer,
                NullLogger<SnapshotArchiveRunner>.Instance,
                new IBackupProvider[] { storage });
            SnapshotArchiveProgressTracker progress = new(
                job,
                runId,
                jobs,
                TimeProvider.System);
            using MemoryStream archive = new();

            await runner.WriteAsync(
                job,
                progress,
                archive,
                CancellationToken.None);

            archive.Position = 0;
            using ZipArchive zip = new(archive, ZipArchiveMode.Read, leaveOpen: true);
            string firstContent = await ReadEntryAsync(zip, "folder/000.txt");
            string lastContent = await ReadEntryAsync(zip, "folder/054.txt");
            SnapshotArchiveJob persisted = await _dbContext.SnapshotArchiveJobs
                .AsNoTracking()
                .SingleAsync(x => x.Id == job.Id);

            Assert.Multiple(() =>
            {
                Assert.That(zip.Entries, Has.Count.EqualTo(fileCount));
                Assert.That(firstContent, Is.EqualTo(expectedContents["folder/000.txt"]));
                Assert.That(lastContent, Is.EqualTo(expectedContents["folder/054.txt"]));
                Assert.That(persisted.Phase, Is.EqualTo(SnapshotArchivePhase.Streaming));
                Assert.That(persisted.ProcessedFiles, Is.EqualTo(fileCount));
                Assert.That(persisted.ProcessedBytes, Is.EqualTo(totalBytes));
                Assert.That(persisted.PreparedChunkReferences, Is.EqualTo(fileCount));
                Assert.That(publisher.Updates.Any(x =>
                    x.Phase == SnapshotArchivePhase.Preparing), Is.True);
                Assert.That(publisher.Updates.Any(x =>
                    x.Phase == SnapshotArchivePhase.Streaming &&
                    x.ProcessedFiles == fileCount), Is.True);
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

        private static async Task<string> ReadEntryAsync(ZipArchive archive, string name)
        {
            ZipArchiveEntry? entry = archive.GetEntry(name);
            Assert.That(entry, Is.Not.Null);
            using StreamReader reader = new(entry!.Open(), Encoding.UTF8);
            return await reader.ReadToEndAsync();
        }
    }
}
