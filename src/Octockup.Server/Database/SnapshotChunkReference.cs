// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.EntityFrameworkCore.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Octockup.Server.Database
{
    [Table("snapshot_chunk_references")]
    [Index(nameof(StorageId), nameof(ChunkHash))]
    [Index(nameof(SnapshotId))]
    [Index(nameof(SnapshotFileId), nameof(Ordinal), IsUnique = true)]
    public class SnapshotChunkReference : BaseEntity<Guid>
    {
        [Column("storage_id")]
        public Guid StorageId { get; set; }

        [Column("snapshot_id")]
        public Guid SnapshotId { get; set; }

        [Column("snapshot_file_id")]
        public Guid SnapshotFileId { get; set; }

        [Column("ordinal")]
        public int Ordinal { get; set; }

        [Column("chunk_hash")]
        public string ChunkHash { get; set; } = string.Empty;

        [DeleteBehavior(DeleteBehavior.Restrict)]
        public virtual Snapshot Snapshot { get; set; } = null!;

        [DeleteBehavior(DeleteBehavior.Restrict)]
        public virtual SnapshotFile SnapshotFile { get; set; } = null!;
    }
}
