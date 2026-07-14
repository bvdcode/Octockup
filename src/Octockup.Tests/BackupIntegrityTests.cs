// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Octockup.Server.Database;
using Octockup.Server.Models.Enums;
using System.Security.Cryptography;
using System.Text;

namespace Octockup.Tests
{
    [Category("Integration")]
    public class BackupIntegrityTests
    {
        private PostgresTestDatabase _database = null!;

        [OneTimeSetUp]
        public async Task CreateDatabaseAsync()
        {
            _database = await PostgresTestDatabase.CreateAsync();
        }

        [OneTimeTearDown]
        public async Task DropDatabaseAsync()
        {
            await _database.DisposeAsync();
        }

        [Test]
        public async Task Backup_WhenFilesAreStored_RestoresExactContent()
        {
            await using BackupIntegrationScenario scenario = await BackupIntegrationScenario.CreateAsync(
                _database.ConnectionString);
            string textPath = Path.Combine("nested", "привет.txt");
            byte[] text = Encoding.UTF8.GetBytes("Octockup integrity round-trip ✓");
            byte[] binary = GC.AllocateUninitializedArray<byte>(64 * 1024);
            new Random(42).NextBytes(binary);
            await scenario.WriteSourceFileAsync(textPath, text);
            await scenario.WriteSourceFileAsync("binary.dat", binary);

            Schedule schedule = await scenario.RunBackupAsync();
            Assert.That(schedule.Status, Is.EqualTo(ScheduleStatus.Completed), schedule.ErrorMessage);
            Snapshot snapshot = await scenario.GetLatestSnapshotAsync();
            byte[] restoredText = await scenario.RestoreFileAsync(snapshot.Id, textPath);
            byte[] restoredBinary = await scenario.RestoreFileAsync(snapshot.Id, "binary.dat");

            Assert.Multiple(() =>
            {
                Assert.That(snapshot.CompletedAt, Is.Not.Null);
                Assert.That(Hash(restoredText), Is.EqualTo(Hash(text)));
                Assert.That(Hash(restoredBinary), Is.EqualTo(Hash(binary)));
            });
        }

        [Test]
        public async Task Backup_WhenContentChangesWithoutMetadataChange_RestoresLatestContent()
        {
            await using BackupIntegrationScenario scenario = await BackupIntegrationScenario.CreateAsync(
                _database.ConnectionString);
            const string path = "stable-metadata.txt";
            byte[] original = Encoding.UTF8.GetBytes("version-one");
            byte[] updated = Encoding.UTF8.GetBytes("version-two");
            DateTime fixedTimestamp = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
            await scenario.WriteSourceFileAsync(path, original, fixedTimestamp);
            Schedule firstSchedule = await scenario.RunBackupAsync();
            Assert.That(firstSchedule.Status, Is.EqualTo(ScheduleStatus.Completed), firstSchedule.ErrorMessage);

            await scenario.WriteSourceFileAsync(path, updated, fixedTimestamp);
            Assert.Multiple(() =>
            {
                Assert.That(updated.Length, Is.EqualTo(original.Length));
                Assert.That(scenario.GetSourceLastWriteTimeUtc(path), Is.EqualTo(fixedTimestamp));
            });

            Schedule secondSchedule = await scenario.RunBackupAsync();
            Assert.That(secondSchedule.Status, Is.EqualTo(ScheduleStatus.Completed), secondSchedule.ErrorMessage);
            Snapshot latestSnapshot = await scenario.GetLatestSnapshotAsync();
            byte[] restored = await scenario.RestoreFileAsync(latestSnapshot.Id, path);

            Assert.Multiple(() =>
            {
                Assert.That(Hash(restored), Is.EqualTo(Hash(updated)),
                    "A completed snapshot must contain the latest bytes even when size and mtime are unchanged.");
            });
        }

        [Test]
        public async Task Backup_WhenDeduplicatedChunkIsMissing_ReuploadsIt()
        {
            await using BackupIntegrationScenario scenario = await BackupIntegrationScenario.CreateAsync(
                _database.ConnectionString);
            byte[] content = Encoding.UTF8.GetBytes("same content, new source path");
            await scenario.WriteSourceFileAsync("first.bin", content);
            Schedule firstSchedule = await scenario.RunBackupAsync();
            Assert.That(firstSchedule.Status, Is.EqualTo(ScheduleStatus.Completed), firstSchedule.ErrorMessage);
            Snapshot firstSnapshot = await scenario.GetLatestSnapshotAsync();
            SnapshotFile firstFile = await scenario.GetSnapshotFileAsync(firstSnapshot.Id, "first.bin");
            string chunkKey = firstFile.ChunkHashes.Single();
            string storageObjectPath = scenario.GetStorageObjectPath(chunkKey);
            Assert.That(File.Exists(storageObjectPath), Is.True, "The first backup must upload its chunk.");
            File.Delete(storageObjectPath);
            Assert.That(File.Exists(storageObjectPath), Is.False, "The test must remove the physical chunk.");
            scenario.DeleteSourceFile("first.bin");
            await scenario.WriteSourceFileAsync("second.bin", content);

            Schedule secondSchedule = await scenario.RunBackupAsync();
            Assert.That(secondSchedule.Status, Is.EqualTo(ScheduleStatus.Completed), secondSchedule.ErrorMessage);

            Assert.That(File.Exists(storageObjectPath), Is.True,
                "A database deduplication record must not substitute for the physical chunk.");
            Snapshot secondSnapshot = await scenario.GetLatestSnapshotAsync();
            byte[] restored = await scenario.RestoreFileAsync(secondSnapshot.Id, "second.bin");
            Assert.That(Hash(restored), Is.EqualTo(Hash(content)));
        }

        private static string Hash(byte[] content)
        {
            return Convert.ToHexString(SHA256.HashData(content));
        }
    }
}
