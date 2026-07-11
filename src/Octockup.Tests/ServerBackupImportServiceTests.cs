// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Octockup.Server.Database;
using Octockup.Server.Helpers;
using Octockup.Server.Models.Enums;
using Octockup.Server.Services;
using System.Text;

namespace Octockup.Tests
{
    public class ServerBackupImportServiceTests
    {
        [Test]
        public async Task ImportAsync_RoundTripsMultipleBatchesWithoutChangingOwnership()
        {
            const int fileCount = 1205;
            TestCipher cipher = new();
            await using SqliteConnection sourceConnection = new("Data Source=:memory:");
            await sourceConnection.OpenAsync();
            await using SqliteDbContext source = await CreateDbContextAsync(sourceConnection);
            (Guid userId, Guid sourceModuleId, Guid backupId, Guid snapshotId) =
                await SeedSourceAsync(source, cipher, fileCount);
            ServerBackupExportService exportService = new(
                source,
                cipher,
                NullLogger<ServerBackupExportService>.Instance);
            using MemoryStream transfer = new();
            await exportService.WriteAsync(
                userId,
                true,
                transfer,
                CancellationToken.None);
            string transferPath = Path.Combine(
                Path.GetTempPath(),
                "octockup-import-test-" + Guid.NewGuid().ToString("N") + ".ctn");

            try
            {
                await File.WriteAllBytesAsync(transferPath, transfer.ToArray());
                await using SqliteConnection targetConnection = new("Data Source=:memory:");
                await targetConnection.OpenAsync();
                await using SqliteDbContext target = await CreateDbContextAsync(targetConnection);
                await AddTargetUserAsync(target, userId);
                ServerBackupImportService importService = new(
                    target,
                    cipher,
                    new ServerBackupJsonStreamReader(),
                    NullLogger<ServerBackupImportService>.Instance);

                await importService.ImportAsync(
                    userId,
                    transferPath,
                    CancellationToken.None);

                Module importedSource = await target.Modules
                    .AsNoTracking()
                    .SingleAsync(x => x.Id == sourceModuleId);
                SnapshotFile firstFile = await target.SnapshotFiles
                    .AsNoTracking()
                    .OrderBy(x => x.Path)
                    .FirstAsync();
                Assert.Multiple(() =>
                {
                    Assert.That(target.Modules.Count(), Is.EqualTo(2));
                    Assert.That(target.Backups.Count(), Is.EqualTo(1));
                    Assert.That(target.Schedules.Count(), Is.EqualTo(1));
                    Assert.That(target.Snapshots.Count(), Is.EqualTo(1));
                    Assert.That(target.SnapshotFiles.Count(), Is.EqualTo(fileCount));
                    Assert.That(
                        target.Backups.AsNoTracking().Single().Id,
                        Is.EqualTo(backupId));
                    Assert.That(
                        target.Snapshots.AsNoTracking().Single().Id,
                        Is.EqualTo(snapshotId));
                    Assert.That(target.Modules.All(x => x.UserId == userId), Is.True);
                    Assert.That(target.Backups.All(x => x.UserId == userId), Is.True);
                    Assert.That(
                        importedSource.Params(cipher)["endpoint"],
                        Is.EqualTo("source.example"));
                    Assert.That(firstFile.ChunkReferencesIndexed, Is.False);
                    Assert.That(firstFile.ChunkHashes, Has.Count.EqualTo(1));
                    Assert.That(target.ChangeTracker.Entries(), Is.Empty);
                });
            }
            finally
            {
                File.Delete(transferPath);
            }
        }

        [Test]
        public async Task ImportAsync_WhenTargetUserIsMissing_DoesNotWriteRows()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using SqliteDbContext target = await CreateDbContextAsync(connection);
            string transferPath = Path.GetTempFileName();
            try
            {
                ServerBackupImportService service = new(
                    target,
                    new TestCipher(),
                    new ServerBackupJsonStreamReader(),
                    NullLogger<ServerBackupImportService>.Instance);

                Assert.ThrowsAsync<InvalidOperationException>(async () =>
                    await service.ImportAsync(
                        Guid.NewGuid(),
                        transferPath,
                        CancellationToken.None));
                Assert.That(target.Modules, Is.Empty);
            }
            finally
            {
                File.Delete(transferPath);
            }
        }

        [Test]
        public async Task ImportAsync_WhenJsonFailsAfterFlushedSection_RollsBackEverything()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            await using SqliteDbContext target = await CreateDbContextAsync(connection);
            Guid userId = Guid.NewGuid();
            await AddTargetUserAsync(target, userId);
            string transferPath = Path.Combine(
                Path.GetTempPath(),
                "octockup-invalid-import-" + Guid.NewGuid().ToString("N") + ".ctn");
            const string malformedJson = """
                {"Modules":[{"Id":"11111111-1111-1111-1111-111111111111","Tag":"module","Destination":0,"BackupModuleId":"provider","Parameters":{}}],"Backups":[
                """;
            try
            {
                await WriteTransferAsync(transferPath, malformedJson);
                ServerBackupImportService service = new(
                    target,
                    new TestCipher(),
                    new ServerBackupJsonStreamReader(),
                    NullLogger<ServerBackupImportService>.Instance);

                Assert.CatchAsync<System.Text.Json.JsonException>(async () =>
                    await service.ImportAsync(
                        userId,
                        transferPath,
                        CancellationToken.None));

                Assert.Multiple(() =>
                {
                    Assert.That(target.Modules, Is.Empty);
                    Assert.That(target.Backups, Is.Empty);
                });
            }
            finally
            {
                File.Delete(transferPath);
            }
        }

        [Test]
        [NonParallelizable]
        public async Task ExportAndImport_WithTwentyFiveThousandFiles_KeepsMemoryBounded()
        {
            const int fileCount = 25_000;
            const long maximumMemoryGrowth = 96L * 1024 * 1024;
            string transferPath = Path.Combine(
                Path.GetTempPath(),
                "octockup-scale-transfer-" + Guid.NewGuid().ToString("N") + ".ctn");
            TestCipher cipher = new();

            try
            {
                await using SqliteConnection sourceConnection = new("Data Source=:memory:");
                await sourceConnection.OpenAsync();
                await using SqliteDbContext source = await CreateDbContextAsync(sourceConnection);
                (Guid userId, _, _, _) = await SeedSourceAsync(
                    source,
                    cipher,
                    fileCount);
                source.ChangeTracker.Clear();
                ServerBackupExportService exportService = new(
                    source,
                    cipher,
                    NullLogger<ServerBackupExportService>.Instance);
                using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(60));
                await using ManagedMemorySampler exportMemory = new();

                await using (FileStream transfer = new(
                    transferPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    64 * 1024,
                    FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    await exportService.WriteAsync(
                        userId,
                        true,
                        transfer,
                        timeout.Token);
                }

                await exportMemory.StopAsync();
                await using SqliteConnection targetConnection = new("Data Source=:memory:");
                await targetConnection.OpenAsync();
                await using SqliteDbContext target = await CreateDbContextAsync(targetConnection);
                await AddTargetUserAsync(target, userId);
                ServerBackupImportService importService = new(
                    target,
                    cipher,
                    new ServerBackupJsonStreamReader(),
                    NullLogger<ServerBackupImportService>.Instance);
                await using ManagedMemorySampler importMemory = new();

                await importService.ImportAsync(
                    userId,
                    transferPath,
                    timeout.Token);
                await importMemory.StopAsync();

                Assert.Multiple(() =>
                {
                    Assert.That(new FileInfo(transferPath).Length, Is.GreaterThan(0));
                    Assert.That(target.SnapshotFiles.Count(), Is.EqualTo(fileCount));
                    Assert.That(target.ChangeTracker.Entries(), Is.Empty);
                    Assert.That(
                        exportMemory.MaximumGrowthBytes,
                        Is.LessThan(maximumMemoryGrowth));
                    Assert.That(
                        exportMemory.RetainedGrowthBytes,
                        Is.LessThan(maximumMemoryGrowth));
                    Assert.That(
                        importMemory.MaximumGrowthBytes,
                        Is.LessThan(maximumMemoryGrowth));
                    Assert.That(
                        importMemory.RetainedGrowthBytes,
                        Is.LessThan(maximumMemoryGrowth));
                });
            }
            finally
            {
                File.Delete(transferPath);
            }
        }

        private static async Task<SqliteDbContext> CreateDbContextAsync(
            SqliteConnection connection)
        {
            DbContextOptions<SqliteDbContext> options =
                new DbContextOptionsBuilder<SqliteDbContext>()
                    .UseSqlite(connection)
                    .Options;
            SqliteDbContext dbContext = new(options);
            await dbContext.Database.EnsureCreatedAsync();
            return dbContext;
        }

        private static async Task<(Guid UserId, Guid SourceId, Guid BackupId, Guid SnapshotId)>
            SeedSourceAsync(
                AppDbContext dbContext,
                TestCipher cipher,
                int fileCount)
        {
            User user = new()
            {
                Username = "import-source-user",
                PasswordPhc = "password"
            };
            Module source = CreateModule(
                user,
                "import-source",
                ModuleDestination.Source);
            Module storage = CreateModule(
                user,
                "import-storage",
                ModuleDestination.Target);
            await dbContext.AddRangeAsync(user, source, storage);
            await dbContext.SaveChangesAsync();
            source.Params(cipher)["endpoint"] = "source.example";
            storage.Params(cipher)["endpoint"] = "storage.example";
            Backup backup = new()
            {
                UserId = user.Id,
                SourceId = source.Id,
                StorageId = storage.Id,
                Tag = "import-backup"
            };
            await dbContext.Backups.AddAsync(backup);
            await dbContext.SaveChangesAsync();
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
            await dbContext.AddRangeAsync(schedule, snapshot);
            await dbContext.SaveChangesAsync();

            for (int start = 0; start < fileCount; start += 500)
            {
                int count = Math.Min(500, fileCount - start);
                List<SnapshotFile> files = new(count);
                for (int offset = 0; offset < count; offset++)
                {
                    int index = start + offset;
                    string hash = index.ToString("x64");
                    files.Add(new SnapshotFile
                    {
                        SnapshotId = snapshot.Id,
                        Path = $"files/{index:D6}.bin",
                        Name = $"{index:D6}.bin",
                        Size = 1,
                        Hashsum = hash,
                        ChunkHashes = [hash],
                        ChunkReferencesIndexed = true
                    });
                }

                await dbContext.SnapshotFiles.AddRangeAsync(files);
                await dbContext.SaveChangesAsync();
                dbContext.ChangeTracker.Clear();
            }

            return (user.Id, source.Id, backup.Id, snapshot.Id);
        }

        private static async Task AddTargetUserAsync(
            AppDbContext dbContext,
            Guid userId)
        {
            User user = new()
            {
                Username = "import-target-user",
                PasswordPhc = "password"
            };
            dbContext.Users.Add(user);
            dbContext.Entry(user).Property("Id").CurrentValue = userId;
            await dbContext.SaveChangesAsync();
            dbContext.ChangeTracker.Clear();
        }

        private static async Task WriteTransferAsync(string path, string json)
        {
            await using FileStream file = new(
                path,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                64 * 1024,
                FileOptions.Asynchronous);
            await using Stream compressed =
                CompressionHelpers.CreateCompressionStream(file);
            await compressed.WriteAsync(Encoding.UTF8.GetBytes(json));
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
