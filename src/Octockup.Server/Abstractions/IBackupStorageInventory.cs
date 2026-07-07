// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Octockup.Server.Models;

namespace Octockup.Server.Abstractions
{
    public interface IBackupStorageInventory
    {
        IAsyncEnumerable<BackupFileInfo> GetFilesAsync(
            bool recursive = false,
            CancellationToken cancellationToken = default);
    }
}
