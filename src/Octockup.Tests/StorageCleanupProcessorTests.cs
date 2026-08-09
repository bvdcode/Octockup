// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Octockup.Server.Database;
using Octockup.Server.Jobs;
using Octockup.Server.Models.Enums;
using System.Security.Cryptography;
using System.Text;

namespace Octockup.Tests
{
    [Category("Integration")]
    public class StorageCleanupProcessorTests
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
        public async Task ProcessAsync_DeletesOnlyUnreferencedChunksAndPersistsStatistics()
        {
            await using StorageCleanupTestScenario scenario = await StorageCleanupTestScenario.CreateAsync(
                _database.ConnectionString);
            string referencedHash = CreateHash("referenced");
            string orphanHash = CreateHash("orphan");
            await scenario.AddUploadedChunkAsync(referencedHash, [1, 2, 3]);
            await scenario.AddUploadedChunkAsync(orphanHash, [4, 5, 6, 7]);
            await scenario.AddSnapshotFileAsync([referencedHash]);

            await scenario.ProcessAsync();
            await scenario.ProcessAsync();

            StorageCleanup cleanup = await scenario.GetCleanupAsync();
            StorageCleanupRun run = await scenario.GetRunAsync();
            Assert.Multiple(() =>
            {
                Assert.That(cleanup.Status, Is.EqualTo(StorageCleanupStatus.Completed));
                Assert.That(cleanup.ScannedChunks, Is.EqualTo(2));
                Assert.That(cleanup.TotalDeletedChunks, Is.EqualTo(1));
                Assert.That(cleanup.TotalReclaimedBytes, Is.EqualTo(4));
                Assert.That(cleanup.LastRunId, Is.EqualTo(run.Id));
                Assert.That(scenario.ChunkExists(referencedHash), Is.True);
                Assert.That(scenario.ChunkExists(orphanHash), Is.False);
                Assert.That(run.Status, Is.EqualTo(StorageCleanupStatus.Completed));
                Assert.That(run.ScannedChunks, Is.EqualTo(2));
                Assert.That(run.DeletedChunks, Is.EqualTo(1));
                Assert.That(run.ReclaimedBytes, Is.EqualTo(4));
                Assert.That(run.CompletedAt, Is.Not.Null);
            });
            Assert.That(await scenario.UploadedHashesAsync(), Is.EqualTo([referencedHash]));
            Assert.That(await scenario.QueuedChunksAsync(), Is.Empty);
        }

        [Test]
        public async Task ProcessAsync_WhenQueuedChunkBecameReferenced_RestoresRegistryWithoutDeletingData()
        {
            await using StorageCleanupTestScenario scenario = await StorageCleanupTestScenario.CreateAsync(
                _database.ConnectionString);
            string hash = CreateHash("reused-after-restart");
            await scenario.AddQueuedChunkAsync(hash, [1, 2, 3, 4, 5]);
            await scenario.AddSnapshotFileAsync([hash]);

            await scenario.ProcessAsync();
            await scenario.ProcessAsync();

            StorageCleanup cleanup = await scenario.GetCleanupAsync();
            StorageCleanupRun run = await scenario.GetRunAsync();
            Assert.Multiple(() =>
            {
                Assert.That(cleanup.Status, Is.EqualTo(StorageCleanupStatus.Completed));
                Assert.That(cleanup.TotalDeletedChunks, Is.Zero);
                Assert.That(cleanup.TotalReclaimedBytes, Is.Zero);
                Assert.That(scenario.ChunkExists(hash), Is.True);
                Assert.That(run.Status, Is.EqualTo(StorageCleanupStatus.Completed));
                Assert.That(run.DeletedChunks, Is.Zero);
            });
            Assert.That(await scenario.UploadedHashesAsync(), Is.EqualTo([hash]));
            Assert.That(await scenario.QueuedChunksAsync(), Is.Empty);
        }

        [Test]
        public async Task ProcessAsync_WhenQueuedChunkWasAlreadyDeleted_CompletesItExactlyOnce()
        {
            await using StorageCleanupTestScenario scenario = await StorageCleanupTestScenario.CreateAsync(
                _database.ConnectionString);
            string hash = CreateHash("deleted-before-restart");
            await scenario.AddQueuedChunkAsync(hash, storedSize: 7);

            await scenario.ProcessAsync();

            StorageCleanup cleanup = await scenario.GetCleanupAsync();
            StorageCleanupRun run = await scenario.GetRunAsync();
            Assert.Multiple(() =>
            {
                Assert.That(cleanup.Status, Is.EqualTo(StorageCleanupStatus.Completed));
                Assert.That(cleanup.TotalDeletedChunks, Is.EqualTo(1));
                Assert.That(cleanup.TotalReclaimedBytes, Is.EqualTo(7));
                Assert.That(run.DeletedChunks, Is.EqualTo(1));
                Assert.That(run.ReclaimedBytes, Is.EqualTo(7));
            });
            Assert.That(await scenario.QueuedChunksAsync(), Is.Empty);
        }

        [Test]
        public async Task ProcessAsync_ScansOnlyOneBoundedBatchPerExecution()
        {
            await using StorageCleanupTestScenario scenario = await StorageCleanupTestScenario.CreateAsync(
                _database.ConnectionString);
            int chunkCount = StorageCleanupProcessor.ScanBatchSize + 1;
            List<string> hashes = Enumerable.Range(0, chunkCount)
                .Select(index => CreateHash($"bounded-{index}"))
                .ToList();
            await scenario.AddUploadedHashesAsync(hashes);
            await scenario.AddSnapshotFileAsync(hashes);

            await scenario.ProcessAsync();

            StorageCleanup cleanup = await scenario.GetCleanupAsync();
            StorageCleanupRun run = await scenario.GetRunAsync();
            Assert.Multiple(() =>
            {
                Assert.That(cleanup.Status, Is.EqualTo(StorageCleanupStatus.Running));
                Assert.That(cleanup.ScannedChunks, Is.EqualTo(StorageCleanupProcessor.ScanBatchSize));
                Assert.That(cleanup.TotalDeletedChunks, Is.Zero);
                Assert.That(run.Status, Is.EqualTo(StorageCleanupStatus.Running));
                Assert.That(run.ScannedChunks, Is.EqualTo(StorageCleanupProcessor.ScanBatchSize));
            });
            Assert.That(await scenario.UploadedHashesAsync(), Has.Count.EqualTo(chunkCount));
            Assert.That(await scenario.QueuedChunksAsync(), Is.Empty);
        }

        private static string CreateHash(string value)
        {
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
        }
    }
}
