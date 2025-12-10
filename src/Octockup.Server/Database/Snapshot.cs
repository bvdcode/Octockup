// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using EasyExtensions.EntityFrameworkCore.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace Octockup.Server.Database
{
    [Table("snapshots1")]
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

        public virtual Backup Backup { get; set; } = null!;
        public ICollection<SnapshotFile> Files { get; set; } = [];
    }
}
