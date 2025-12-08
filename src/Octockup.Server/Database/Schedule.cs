// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using Octockup.Server.Models.Enums;
using EasyExtensions.EntityFrameworkCore.Abstractions;

namespace Octockup.Server.Database
{
    public class Schedule : BaseEntity<Guid>
    {
        public Guid BackupId { get; set; }
        public DateTime? FinishedAt { get; set; }
        public ScheduleStatus Status { get; set; }
        public DateTime StartAt { get; set; }
        public TimeSpan? Interval { get; set; }
        public string? ErrorMessage { get; set; }

        public virtual Backup Backup { get; set; } = null!;
        public virtual ICollection<Snapshot> Snapshots { get; set; } = [];
    }
}
