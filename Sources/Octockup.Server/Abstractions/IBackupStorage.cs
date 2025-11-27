// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

namespace Octockup.Server.Abstractions
{
    public interface IBackupStorage : IBackupSource, IBackupProvider
    {
        bool? Exists(string path);
        Task<bool?> DeleteAsync(string path);
        Task UploadAsync(string path, Stream data);
        Task<Stream> DownloadAsync(string path);
    }
}
