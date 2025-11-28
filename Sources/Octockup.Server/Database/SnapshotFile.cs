// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using EasyExtensions.EntityFrameworkCore.Abstractions;

namespace Octockup.Server.Database
{
    public class SnapshotFile : BaseEntity<Guid>
    {
        public Guid SnapshotId { get; set; }
        public long Size { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Hashsum { get; set; } = string.Empty;
        public ICollection<string> ChunkHashes { get; set; } = [];

        public virtual Snapshot Snapshot { get; set; } = null!;
    }
}
