// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.EntityFrameworkCore.Abstractions;
using Microsoft.EntityFrameworkCore;
using Octockup.Server.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace Octockup.Server.Database
{
    [Table("snapshot_archive_jobs")]
    [Index(nameof(ActiveSnapshotId), IsUnique = true)]
    [Index(nameof(UserId), nameof(StartedAt))]
    public class SnapshotArchiveJob : BaseEntity<Guid>
    {
        [Column("user_id")]
        public Guid UserId { get; set; }

        [Column("snapshot_id")]
        public Guid SnapshotId { get; set; }

        [Column("active_snapshot_id")]
        public Guid? ActiveSnapshotId { get; set; }

        [Column("run_id")]
        public Guid? RunId { get; set; }

        [Column("status")]
        public SnapshotArchiveStatus Status { get; set; }

        [Column("phase")]
        public SnapshotArchivePhase Phase { get; set; }

        [Column("started_at")]
        public DateTime StartedAt { get; set; }

        [Column("finished_at")]
        public DateTime? FinishedAt { get; set; }

        [Column("cancellation_requested")]
        public bool CancellationRequested { get; set; }

        [Column("error_message")]
        public string? ErrorMessage { get; set; }

        [Column("total_files")]
        public long TotalFiles { get; set; }

        [Column("processed_files")]
        public long ProcessedFiles { get; set; }

        [Column("total_bytes")]
        public long TotalBytes { get; set; }

        [Column("processed_bytes")]
        public long ProcessedBytes { get; set; }

        [Column("prepared_chunk_references")]
        public long PreparedChunkReferences { get; set; }

        [Column("current_path")]
        public string? CurrentPath { get; set; }
    }
}
