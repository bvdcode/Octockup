// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Octockup.Server.Database;
using Octockup.Server.Helpers;
using Octockup.Server.Models.Enums;
using Octockup.Server.Services;
using System.Text.Json;

namespace Octockup.Tests
{
    public class ServerBackupExportServiceTests
    {
        private SqliteConnection _connection = null!;
        private SqliteDbContext _dbContext = null!;
        private TestCipher _cipher = null!;
        private Guid _userId;

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
            _cipher = new TestCipher();
            _userId = await SeedUserAsync("export-owner", 205);
            await SeedUserAsync("different-owner", 3);
            _dbContext.ChangeTracker.Clear();
        }

        [TearDown]
        public async Task TearDown()
        {
            await _dbContext.DisposeAsync();
            await _connection.DisposeAsync();
        }

        [TestCase(true, 205)]
        [TestCase(false, 0)]
        public async Task WriteAsync_StreamsTenantScopedTransferShape(
            bool includeFiles,
            int expectedFileCount)
        {
            ServerBackupExportService service = new(
                _dbContext,
                _cipher,
                NullLogger<ServerBackupExportService>.Instance);
            using MemoryStream output = new();

            await service.WriteAsync(
                _userId,
                includeFiles,
                output,
                CancellationToken.None);

            output.Position = 0;
            await using Stream decompressed =
                CompressionHelpers.CreateDecompressionStream(output);
            using JsonDocument document = await JsonDocument.ParseAsync(decompressed);
            JsonElement root = document.RootElement;
            JsonElement[] modules = root.GetProperty("Modules").EnumerateArray().ToArray();
            JsonElement[] backups = root.GetProperty("Backups").EnumerateArray().ToArray();
            JsonElement[] schedules = root.GetProperty("Schedules").EnumerateArray().ToArray();
            JsonElement[] snapshots = root.GetProperty("Snapshots").EnumerateArray().ToArray();
            JsonElement[] files = root.GetProperty("SnapshotFiles").EnumerateArray().ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(modules, Has.Length.EqualTo(2));
                Assert.That(backups, Has.Length.EqualTo(1));
                Assert.That(schedules, Has.Length.EqualTo(1));
                Assert.That(snapshots, Has.Length.EqualTo(1));
                Assert.That(files, Has.Length.EqualTo(expectedFileCount));
                Assert.That(modules.All(x =>
                    x.GetProperty("UserId").GetGuid() == _userId), Is.True);
                Assert.That(backups.All(x =>
                    x.GetProperty("UserId").GetGuid() == _userId), Is.True);
                Assert.That(
                    modules.Single(x => x.GetProperty("Tag").GetString() == "export-owner-source")
                        .GetProperty("Parameters")
                        .GetProperty("endpoint")
                        .GetString(),
                    Is.EqualTo("source.example"));
                Assert.That(_dbContext.ChangeTracker.Entries(), Is.Empty);
            });
        }

        private async Task<Guid> SeedUserAsync(string prefix, int fileCount)
        {
            User user = new()
            {
                Username = prefix,
                PasswordPhc = "password"
            };
            Module source = CreateModule(user, prefix + "-source", ModuleDestination.Source);
            Module storage = CreateModule(user, prefix + "-storage", ModuleDestination.Target);
            await _dbContext.AddRangeAsync(user, source, storage);
            await _dbContext.SaveChangesAsync();
            source.Params(_cipher)["endpoint"] = "source.example";
            storage.Params(_cipher)["endpoint"] = "storage.example";

            Backup backup = new()
            {
                UserId = user.Id,
                SourceId = source.Id,
                StorageId = storage.Id,
                Tag = prefix + "-backup"
            };
            await _dbContext.Backups.AddAsync(backup);
            await _dbContext.SaveChangesAsync();
            Schedule schedule = new()
            {
                BackupId = backup.Id,
                StartAt = DateTime.UtcNow,
                Status = ScheduleStatus.Completed,
                FinishedAt = DateTime.UtcNow
            };
            Snapshot snapshot = new()
            {
                BackupId = backup.Id,
                CompletedAt = DateTime.UtcNow,
                FilesCount = fileCount,
                TotalSize = fileCount
            };
            await _dbContext.AddRangeAsync(schedule, snapshot);
            await _dbContext.SaveChangesAsync();

            List<SnapshotFile> files = new(fileCount);
            for (int index = 0; index < fileCount; index++)
            {
                files.Add(new SnapshotFile
                {
                    SnapshotId = snapshot.Id,
                    Path = $"files/{index:D6}.bin",
                    Name = $"{index:D6}.bin",
                    Size = 1,
                    Hashsum = index.ToString("x64"),
                    ChunkHashes = [index.ToString("x64")]
                });
            }

            await _dbContext.SnapshotFiles.AddRangeAsync(files);
            await _dbContext.SaveChangesAsync();
            return user.Id;
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
    }
}
