// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using EasyExtensions.EntityFrameworkCore.Abstractions;

namespace Octockup.Server.Database
{
    public class Snapshot : BaseEntity<Guid>
    {
        public Guid BackupId { get; set; }
        public DateTime? CompletedAt { get; set; }
        public virtual Backup Backup { get; set; } = null!;

        public ICollection<SnapshotFile> Files { get; set; } = [];
    }
}
