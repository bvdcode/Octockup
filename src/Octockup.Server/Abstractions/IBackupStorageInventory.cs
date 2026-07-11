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

        /// <summary>
        /// Enumerates files in stable ordinal path order, excluding the cursor path and all prior paths.
        /// </summary>
        IAsyncEnumerable<BackupFileInfo> GetFilesAfterAsync(
            string? afterPath,
            bool recursive = false,
            CancellationToken cancellationToken = default);
    }
}
