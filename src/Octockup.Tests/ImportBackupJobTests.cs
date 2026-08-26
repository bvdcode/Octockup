// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Octockup.Server.Database;
using Octockup.Server.Jobs;
using System.Text.Json;

namespace Octockup.Tests
{
    public class ImportBackupJobTests
    {
        [TestCase(typeof(Module))]
        [TestCase(typeof(Backup))]
        [TestCase(typeof(Schedule))]
        [TestCase(typeof(Snapshot))]
        [TestCase(typeof(SnapshotFile))]
        public void CreateOptions_RestoresImportedEntityId(Type entityType)
        {
            Guid expectedId = Guid.NewGuid();
            string json = $$"""{"Id":"{{expectedId}}"}""";

            object? entity = JsonSerializer.Deserialize(
                json,
                entityType,
                ImportBackupJob.CreateOptions());

            Guid actualId = entity switch
            {
                Module module => module.Id,
                Backup backup => backup.Id,
                Schedule schedule => schedule.Id,
                Snapshot snapshot => snapshot.Id,
                SnapshotFile snapshotFile => snapshotFile.Id,
                _ => throw new ArgumentOutOfRangeException(nameof(entityType)),
            };
            Assert.That(actualId, Is.EqualTo(expectedId));
        }
    }
}
