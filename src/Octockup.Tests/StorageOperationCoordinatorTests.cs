// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Octockup.Server.Services;

namespace Octockup.Tests
{
    public class StorageOperationCoordinatorTests
    {
        [Test]
        public async Task Cleanup_WaitsUntilAllBackupsForStorageHaveFinished()
        {
            StorageOperationCoordinator coordinator = new();
            Guid storageId = Guid.NewGuid();
            await using StorageOperationLease firstBackup = await coordinator.AcquireBackupAsync(
                storageId,
                CancellationToken.None);
            await using StorageOperationLease secondBackup = await coordinator.AcquireBackupAsync(
                storageId,
                CancellationToken.None);

            Assert.That(coordinator.TryAcquireCleanup(storageId), Is.Null);
            await firstBackup.DisposeAsync();
            Assert.That(coordinator.TryAcquireCleanup(storageId), Is.Null);
            await secondBackup.DisposeAsync();

            await using StorageOperationLease? cleanup = coordinator.TryAcquireCleanup(storageId);
            Assert.That(cleanup, Is.Not.Null);
        }

        [Test]
        public async Task Backup_WaitsWhileCleanupOwnsStorage()
        {
            StorageOperationCoordinator coordinator = new();
            Guid storageId = Guid.NewGuid();
            StorageOperationLease cleanup = coordinator.TryAcquireCleanup(storageId)!;

            Task<StorageOperationLease> backupTask = coordinator.AcquireBackupAsync(
                storageId,
                CancellationToken.None);
            await Task.Delay(50);
            Assert.That(backupTask.IsCompleted, Is.False);

            await cleanup.DisposeAsync();
            await using StorageOperationLease backup = await backupTask.WaitAsync(TimeSpan.FromSeconds(1));
        }

        [Test]
        public async Task Operations_OnDifferentStoragesDoNotBlockEachOther()
        {
            StorageOperationCoordinator coordinator = new();
            await using StorageOperationLease backup = await coordinator.AcquireBackupAsync(
                Guid.NewGuid(),
                CancellationToken.None);

            await using StorageOperationLease? cleanup = coordinator.TryAcquireCleanup(Guid.NewGuid());

            Assert.That(cleanup, Is.Not.Null);
        }
    }
}
