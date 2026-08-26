// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.EntityFrameworkCore.Abstractions;
using Microsoft.EntityFrameworkCore;
using Octockup.Server.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace Octockup.Server.Database
{
    [Table("schedules")]
    public class Schedule : BaseEntity<Guid>, IImportableEntity
    {
        [Column("backup_id")]
        public Guid BackupId { get; set; }

        [Column("finished_at")]
        public DateTime? FinishedAt { get; set; }

        [Column("status")]
        public ScheduleStatus Status { get; set; }

        [Column("start_at")]
        public DateTime StartAt { get; set; }

        [Column("interval")]
        public TimeSpan? Interval { get; set; }

        [Column("error_message")]
        public string? ErrorMessage { get; set; }

        [DeleteBehavior(DeleteBehavior.Restrict)]
        public virtual Backup Backup { get; set; } = null!;

        public virtual ICollection<Snapshot> Snapshots { get; set; } = [];

        void IImportableEntity.RestoreId(Guid id)
        {
            Id = id;
        }
    }
}
