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

        private static string Hash(byte[] content)
        {
            return Convert.ToHexString(SHA256.HashData(content));
        }
    }
}
