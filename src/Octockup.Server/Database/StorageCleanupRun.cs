// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.EntityFrameworkCore.Abstractions;
using Microsoft.EntityFrameworkCore;
using Octockup.Server.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace Octockup.Server.Database
{
    [Table("storage_cleanup_runs")]
    [Index(nameof(ModuleId), nameof(StartedAt))]
    public class StorageCleanupRun : BaseEntity<Guid>
    {
        [Column("module_id")]
        public Guid ModuleId { get; set; }

        [Column("status")]
        public StorageCleanupStatus Status { get; set; }

        [Column("started_at")]
        public DateTime StartedAt { get; set; }

        [Column("completed_at")]
        public DateTime? CompletedAt { get; set; }

        [Column("scanned_chunks")]
        public long ScannedChunks { get; set; }

        [Column("deleted_chunks")]
        public long DeletedChunks { get; set; }

        [Column("reclaimed_bytes")]
        public long ReclaimedBytes { get; set; }

        [Column("error_message")]
        public string? ErrorMessage { get; set; }

        [DeleteBehavior(DeleteBehavior.Restrict)]
        public virtual Module Module { get; set; } = null!;
    }
}
