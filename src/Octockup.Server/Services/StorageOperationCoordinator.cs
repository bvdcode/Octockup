// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using System.Collections.Concurrent;

namespace Octockup.Server.Services
{
    public class StorageOperationCoordinator
    {
        private readonly ConcurrentDictionary<Guid, StorageOperationLock> _locks = new();

        public Task<StorageOperationLease> AcquireBackupAsync(
            Guid storageId,
            CancellationToken cancellationToken)
        {
            return GetLock(storageId).AcquireBackupAsync(cancellationToken);
        }

        public StorageOperationLease? TryAcquireCleanup(Guid storageId)
        {
            return GetLock(storageId).TryAcquireCleanup();
        }

        private StorageOperationLock GetLock(Guid storageId)
        {
            return _locks.GetOrAdd(storageId, static _ => new StorageOperationLock());
        }
    }
}
