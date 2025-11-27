// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using Octockup.Server.Models;

namespace Octockup.Server.Abstractions
{
    public interface IBackupSource : IBackupProvider
    {
        Task<Stream> GetFileStreamAsync(BackupFileInfo file);
        IEnumerable<string> GetDirectories(bool recursive = false);
        IEnumerable<BackupFileInfo> GetFiles(bool recursive = false);
    }
}
