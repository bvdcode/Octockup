// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.EntityFrameworkCore.Abstractions;
using Microsoft.EntityFrameworkCore;
using Octockup.Server.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace Octockup.Server.Database
{
    [Table("storage_cleanups")]
    [Index(nameof(ModuleId), IsUnique = true)]
    public class StorageCleanup : BaseEntity<Guid>
    {
        [Column("module_id")]
        public Guid ModuleId { get; set; }

        [Column("status")]
        public StorageCleanupStatus Status { get; set; }

        [Column("cursor_hash")]
        public string? CursorHash { get; set; }

        [Column("scan_upper_bound_hash")]
        public string? ScanUpperBoundHash { get; set; }

        [Column("scanned_chunks")]
        public long ScannedChunks { get; set; }

        [Column("total_deleted_chunks")]
        public long TotalDeletedChunks { get; set; }

        [Column("total_reclaimed_bytes")]
        public long TotalReclaimedBytes { get; set; }

        [Column("last_started_at")]
        public DateTime? LastStartedAt { get; set; }

        [Column("last_completed_at")]
        public DateTime? LastCompletedAt { get; set; }

        [Column("error_message")]
        public string? ErrorMessage { get; set; }

        [Column("last_run_id")]
        public Guid? LastRunId { get; set; }

        [DeleteBehavior(DeleteBehavior.Restrict)]
        public virtual Module Module { get; set; } = null!;

        [ForeignKey(nameof(LastRunId))]
        [DeleteBehavior(DeleteBehavior.Restrict)]
        public virtual StorageCleanupRun? LastRun { get; set; }
    }
}
