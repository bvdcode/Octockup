// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

namespace Octockup.Server.Abstractions
{
    public interface IBackupStorage : IBackupSource, IBackupProvider
    {
        Task<bool?> ExistsAsync(string path, CancellationToken cancellationToken = default);
        Task<bool?> DeleteAsync(string path, CancellationToken cancellationToken = default);
        Task UploadAsync(string path, Stream data, CancellationToken cancellationToken = default);
    }
}
