// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov | bvdcode | belov.us

using EasyExtensions.EntityFrameworkCore.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Octockup.Server.Database
{
    [Table("backups")]
    [Index(nameof(Tag), IsUnique = true)]
    public class Backup : BaseEntity<Guid>
    {
        [Column("source_id")]
        public Guid SourceId { get; set; }

        [Column("storage_id")]
        public Guid StorageId { get; set; }

        [Column("tag")]
        public string Tag { get; set; } = string.Empty;

        [Column("ignored_paths")]
        public ICollection<string> IgnoredPaths { get; set; } = [];

        public virtual Module Source { get; set; } = null!;
        public virtual Module Storage { get; set; } = null!;
        public virtual ICollection<Snapshot> Snapshots { get; set; } = [];
        public virtual ICollection<Schedule> Schedules { get; set; } = [];
    }
}
