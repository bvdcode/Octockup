// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Octockup.Server.Abstractions
{
    public interface IBackupStorage : IBackupSource, IBackupProvider
    {
        Task<bool?> ExistsAsync(string path, CancellationToken cancellationToken = default);
        Task<bool?> DeleteAsync(string path, CancellationToken cancellationToken = default);
        Task UploadAsync(string path, Stream data, CancellationToken cancellationToken = default);
    }
}
