// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.EntityFrameworkCore.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Octockup.Server.Database
{
    [Table("snapshots")]
    [Index(nameof(BackupId), nameof(CompletedAt), nameof(Id))]
    public class Snapshot : BaseEntity<Guid>
    {
        [Column("backup_id")]
        public Guid BackupId { get; set; }

        [Column("completed_at")]
        public DateTime? CompletedAt { get; set; }

        [Column("total_size")]
        public long TotalSize { get; set; }

        [Column("files_count")]
        public int FilesCount { get; set; }

        [DeleteBehavior(DeleteBehavior.Restrict)]
        public virtual Backup Backup { get; set; } = null!;

        public virtual ICollection<SnapshotFile> Files { get; set; } = [];
        public virtual ICollection<SnapshotChunkReference> ChunkReferences { get; set; } = [];
    }
}
