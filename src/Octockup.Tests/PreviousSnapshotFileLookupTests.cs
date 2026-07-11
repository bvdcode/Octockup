// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Octockup.Server.Database;
using Octockup.Server.Models.Enums;
using Octockup.Server.Services;

namespace Octockup.Tests
{
    public class PreviousSnapshotFileLookupTests
    {
        private SqliteConnection _connection = null!;
        private SqliteDbContext _dbContext = null!;
        private Guid _backupId;
        private Guid _latestCompletedSnapshotId;

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
                Username = "previous-user",
                PasswordPhc = "password"
            };
            Module source = CreateModule(user, "previous-source", ModuleDestination.Source);
            Module storage = CreateModule(user, "previous-storage", ModuleDestination.Target);
            await _dbContext.AddRangeAsync(user, source, storage);
            await _dbContext.SaveChangesAsync();
            Backup backup = new()
            {
                UserId = user.Id,
                SourceId = source.Id,
                StorageId = storage.Id,
                Tag = "previous-backup"
            };
            await _dbContext.Backups.AddAsync(backup);
            await _dbContext.SaveChangesAsync();

            Snapshot older = CreateSnapshot(backup.Id, DateTime.UtcNow.AddHours(-2));
            Snapshot latest = CreateSnapshot(backup.Id, DateTime.UtcNow.AddHours(-1));
            Snapshot incomplete = CreateSnapshot(backup.Id, null);
            await _dbContext.Snapshots.AddRangeAsync(older, latest, incomplete);
            await _dbContext.SaveChangesAsync();
            await _dbContext.SnapshotFiles.AddRangeAsync(
                CreateFile(older.Id, "same.txt", "older"),
                CreateFile(latest.Id, "same.txt", "latest"),
                CreateFile(incomplete.Id, "same.txt", "incomplete"));
            await _dbContext.SaveChangesAsync();
            _dbContext.ChangeTracker.Clear();

            _backupId = backup.Id;
            _latestCompletedSnapshotId = latest.Id;
        }

        [TearDown]
        public async Task TearDown()
        {
            await _dbContext.DisposeAsync();
            await _connection.DisposeAsync();
        }

        [Test]
        public async Task LoadBatchAsync_UsesLatestCompletedSnapshotOnly()
        {
            PreviousSnapshotFileLookup lookup = new(_dbContext);
            await lookup.InitializeAsync(_backupId, CancellationToken.None);

            IReadOnlyDictionary<string, SnapshotFile> files = await lookup.LoadBatchAsync(
                new[] { "same.txt", "missing.txt" },
                CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(lookup.SnapshotId, Is.EqualTo(_latestCompletedSnapshotId));
                Assert.That(lookup.PreviousFileCount, Is.EqualTo(1));
                Assert.That(files, Has.Count.EqualTo(1));
                Assert.That(files["same.txt"].Hashsum, Is.EqualTo("latest"));
            });
        }

        [Test]
        public async Task LoadBatchAsync_WhenBatchExceedsBound_RejectsRequest()
        {
            PreviousSnapshotFileLookup lookup = new(_dbContext);
            await lookup.InitializeAsync(_backupId, CancellationToken.None);
            string[] paths = Enumerable.Range(0, PreviousSnapshotFileLookup.MaxBatchSize + 1)
                .Select(index => index.ToString())
                .ToArray();

            Assert.That(
                async () => await lookup.LoadBatchAsync(paths, CancellationToken.None),
                Throws.InstanceOf<ArgumentOutOfRangeException>());
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

        private static Snapshot CreateSnapshot(Guid backupId, DateTime? completedAt)
        {
            return new Snapshot
            {
                BackupId = backupId,
                CompletedAt = completedAt,
                FilesCount = 1,
                TotalSize = 10
            };
        }

        private static SnapshotFile CreateFile(
            Guid snapshotId,
            string path,
            string hash)
        {
            return new SnapshotFile
            {
                SnapshotId = snapshotId,
                Path = path,
                Name = path,
                Size = 10,
                Hashsum = hash,
                ChunkHashes = [hash]
            };
        }
    }
}
