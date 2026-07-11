// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Octockup.Server.Models.Transfer
{
    public class ServerBackupBackupRecord
    {
        public Guid Id { get; set; }
        public Guid SourceId { get; set; }
        public Guid StorageId { get; set; }
        public string Tag { get; set; } = string.Empty;
        public List<string> IgnoredPaths { get; set; } = [];
        public bool DisableCompression { get; set; }
        public bool DisableEncryption { get; set; }
    }
}
