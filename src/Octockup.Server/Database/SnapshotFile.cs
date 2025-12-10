// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using EasyExtensions.EntityFrameworkCore.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace Octockup.Server.Database
{
    [Table("snapshot_files1")]
    public class SnapshotFile : BaseEntity<Guid>
    {
        [Column("size")]
        public long Size { get; set; }

        [Column("snapshot_id")]
        public Guid SnapshotId { get; set; }

        [Column("last_modified")]
        public DateTime? LastModified { get; set; }

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("path")]
        public string Path { get; set; } = string.Empty;

        [Column("hashsum")]
        public string Hashsum { get; set; } = string.Empty;

        [Column("chunk_hashes")]
        public ICollection<string> ChunkHashes { get; set; } = [];

        public virtual Snapshot Snapshot { get; set; } = null!;
    }
}
