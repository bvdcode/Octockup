// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Octockup.Server.Models.Enums;

namespace Octockup.Server.Abstractions
{
    public interface IStorageOperationCoordinator
    {
        Task<IStorageOperationLease?> TryAcquireAsync(
            Guid storageId,
            StorageOperationKind kind,
            CancellationToken cancellationToken);
    }
}
