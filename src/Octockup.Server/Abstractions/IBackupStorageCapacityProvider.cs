// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Octockup.Server.Models;

namespace Octockup.Server.Abstractions
{
    public interface IBackupStorageCapacityProvider
    {
        Task<StorageCapacityInfo?> GetCapacityAsync(CancellationToken cancellationToken = default);
    }
}
