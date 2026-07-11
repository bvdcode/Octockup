// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Octockup.Server.Models.Transfer
{
    public class ServerBackupSnapshotFileRecord
    {
        public Guid Id { get; set; }
        public long Size { get; set; }
        public Guid SnapshotId { get; set; }
        public DateTime? LastModified { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Hashsum { get; set; } = string.Empty;
        public List<string> ChunkHashes { get; set; } = [];
    }
}
