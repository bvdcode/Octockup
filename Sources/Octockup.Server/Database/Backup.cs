// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using Microsoft.EntityFrameworkCore;
using EasyExtensions.EntityFrameworkCore.Abstractions;

namespace Octockup.Server.Database
{
    [Index(nameof(Tag), IsUnique = true)]
    public class Backup : BaseEntity<Guid>
    {
        public Guid SourceId { get; set; }
        public Guid StorageId { get; set; }
        public string Tag { get; set; } = string.Empty;
        public ICollection<string> IgnoredPaths { get; set; } = [];

        public virtual Module Source { get; set; } = null!;
        public virtual Module Storage { get; set; } = null!;
        public virtual ICollection<Snapshot> Snapshots { get; set; } = [];
        public virtual ICollection<Schedule> Schedules { get; set; } = [];
    }
}
