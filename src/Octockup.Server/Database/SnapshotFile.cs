// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.EntityFrameworkCore.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Octockup.Server.Database
{
    [Table("snapshot_files")]
    [Index(nameof(SnapshotId), nameof(Path), IsUnique = true)]
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

        [Column("chunk_references_indexed")]
        public bool ChunkReferencesIndexed { get; set; }

        [DeleteBehavior(DeleteBehavior.Restrict)]
        public virtual Snapshot Snapshot { get; set; } = null!;
        public virtual ICollection<SnapshotChunkReference> ChunkReferences { get; set; } = [];
    }
}
