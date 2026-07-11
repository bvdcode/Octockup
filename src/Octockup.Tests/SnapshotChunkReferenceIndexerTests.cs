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
    public class SnapshotChunkReferenceIndexerTests
    {
        private SqliteConnection _connection = null!;
        private SqliteDbContext _dbContext = null!;
        private Guid _completedFileId;
        private Guid _incompleteFileId;
        private Guid _storageId;

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
            User user = new()
            {
                Username = "reference-index-user",
                PasswordPhc = "password"
            };
            Module source = CreateModule(user, "reference-source", ModuleDestination.Source);
            Module storage = CreateModule(user, "reference-storage", ModuleDestination.Target);
            await _dbContext.AddRangeAsync(user, source, storage);
            await _dbContext.SaveChangesAsync();
            Backup backup = new()
            {
                UserId = user.Id,
                SourceId = source.Id,
                StorageId = storage.Id,
                Tag = "reference-backup"
            };
            await _dbContext.Backups.AddAsync(backup);
            await _dbContext.SaveChangesAsync();
            Snapshot completed = new()
            {
                BackupId = backup.Id,
                CompletedAt = DateTime.UtcNow,
                FilesCount = 1,
                TotalSize = 10
            };
            Snapshot incomplete = new()
            {
                BackupId = backup.Id,
                FilesCount = 1,
                TotalSize = 10
            };
            await _dbContext.Snapshots.AddRangeAsync(completed, incomplete);
            await _dbContext.SaveChangesAsync();
            SnapshotFile completedFile = CreateFile(completed.Id, "completed", ["a", "a", "b"]);
            SnapshotFile incompleteFile = CreateFile(incomplete.Id, "incomplete", ["c"]);
            await _dbContext.SnapshotFiles.AddRangeAsync(completedFile, incompleteFile);
            await _dbContext.SaveChangesAsync();
            _dbContext.ChangeTracker.Clear();

            _completedFileId = completedFile.Id;
            _incompleteFileId = incompleteFile.Id;
            _storageId = storage.Id;
        }

        [TearDown]
        public async Task TearDown()
        {
            await _dbContext.DisposeAsync();
            await _connection.DisposeAsync();
        }

        [Test]
        public async Task IndexStorageAsync_PreservesRepeatedReferencesAndSkipsIncompleteSnapshots()
        {
            List<(long Files, long References)> progress = [];
            SnapshotChunkReferenceIndexer indexer = CreateIndexer();

            await indexer.IndexStorageAsync(
                _storageId,
                (files, references, _) =>
                {
                    progress.Add((files, references));
                    return Task.CompletedTask;
                },
                CancellationToken.None);
            await indexer.IndexStorageAsync(_storageId, null, CancellationToken.None);
            List<SnapshotChunkReference> references = await _dbContext.SnapshotChunkReferences
                .AsNoTracking()
                .OrderBy(x => x.Ordinal)
                .ToListAsync();
            SnapshotFile completedFile = await LoadFileAsync(_completedFileId);
            SnapshotFile incompleteFile = await LoadFileAsync(_incompleteFileId);

            Assert.Multiple(() =>
            {
                Assert.That(references.Select(x => x.ChunkHash),
                    Is.EqualTo(new[] { "a", "a", "b" }));
                Assert.That(references.Select(x => x.Ordinal),
                    Is.EqualTo(new[] { 0, 1, 2 }));
                Assert.That(completedFile.ChunkReferencesIndexed, Is.True);
                Assert.That(incompleteFile.ChunkReferencesIndexed, Is.False);
                Assert.That(progress.Last(), Is.EqualTo((1L, 3L)));
            });
        }

        [Test]
        public async Task IndexStorageAsync_WhenPreviousRunWasPartial_ResumesIdempotently()
        {
            SnapshotFile file = await LoadFileAsync(_completedFileId);
            await _dbContext.SnapshotChunkReferences.AddAsync(new SnapshotChunkReference
            {
                StorageId = _storageId,
                SnapshotId = file.SnapshotId,
                SnapshotFileId = file.Id,
                Ordinal = 0,
                ChunkHash = "a"
            });
            await _dbContext.SaveChangesAsync();
            _dbContext.ChangeTracker.Clear();
            SnapshotChunkReferenceIndexer indexer = CreateIndexer();
            List<(long Files, long References)> progress = [];

            await indexer.IndexStorageAsync(
                _storageId,
                (files, references, _) =>
                {
                    progress.Add((files, references));
                    return Task.CompletedTask;
                },
                CancellationToken.None);
            List<SnapshotChunkReference> references = await _dbContext.SnapshotChunkReferences
                .AsNoTracking()
                .OrderBy(x => x.Ordinal)
                .ToListAsync();

            Assert.Multiple(() =>
            {
                Assert.That(references, Has.Count.EqualTo(3));
                Assert.That(references.Select(x => x.Ordinal),
                    Is.EqualTo(new[] { 0, 1, 2 }));
                Assert.That(references.Select(x => x.ChunkHash),
                    Is.EqualTo(new[] { "a", "a", "b" }));
                Assert.That(progress.First(), Is.EqualTo((0L, 1L)));
                Assert.That(progress.Last(), Is.EqualTo((1L, 3L)));
            });
        }

        private SnapshotChunkReferenceIndexer CreateIndexer()
        {
            SnapshotChunkReferenceWriter writer = new(
                _dbContext,
                NullLogger<SnapshotChunkReferenceWriter>.Instance);
            return new SnapshotChunkReferenceIndexer(_dbContext, writer);
        }

        private Task<SnapshotFile> LoadFileAsync(Guid fileId)
        {
            return _dbContext.SnapshotFiles
                .AsNoTracking()
                .SingleAsync(x => x.Id == fileId);
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

        private static SnapshotFile CreateFile(
            Guid snapshotId,
            string path,
            ICollection<string> hashes)
        {
            return new SnapshotFile
            {
                SnapshotId = snapshotId,
                Path = path,
                Name = path,
                Size = 10,
                Hashsum = path + "-hash",
                ChunkHashes = hashes,
                ChunkReferencesIndexed = false
            };
        }
    }
}
