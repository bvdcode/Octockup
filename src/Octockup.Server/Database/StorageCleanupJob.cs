// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.EntityFrameworkCore.Abstractions;
using Microsoft.EntityFrameworkCore;
using Octockup.Server.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace Octockup.Server.Database
{
    [Table("storage_cleanup_jobs")]
    [Index(nameof(ActiveStorageId), IsUnique = true)]
    [Index(nameof(UserId), nameof(StartedAt))]
    public class StorageCleanupJob : BaseEntity<Guid>
    {
        [Column("user_id")]
        public Guid UserId { get; set; }

        [Column("storage_id")]
        public Guid StorageId { get; set; }

        [Column("active_storage_id")]
        public Guid? ActiveStorageId { get; set; }

        [Column("run_id")]
        public Guid? RunId { get; set; }

        [Column("storage_tag")]
        public string StorageTag { get; set; } = string.Empty;

        [Column("status")]
        public StorageCleanupStatus Status { get; set; }

        [Column("phase")]
        public StorageCleanupPhase Phase { get; set; }

        [Column("started_at")]
        public DateTime StartedAt { get; set; }

        [Column("finished_at")]
        public DateTime? FinishedAt { get; set; }

        [Column("cancellation_requested")]
        public bool CancellationRequested { get; set; }

        [Column("error_message")]
        public string? ErrorMessage { get; set; }

        [Column("snapshot_files_scanned")]
        public long SnapshotFilesScanned { get; set; }

        [Column("reference_count")]
        public long ReferenceCount { get; set; }

        [Column("referenced_chunks")]
        public long ReferencedChunks { get; set; }

        [Column("storage_objects_scanned")]
        public long StorageObjectsScanned { get; set; }

        [Column("storage_bytes_scanned")]
        public long StorageBytesScanned { get; set; }

        [Column("chunk_objects_scanned")]
        public long ChunkObjectsScanned { get; set; }

        [Column("referenced_objects")]
        public long ReferencedObjects { get; set; }

        [Column("referenced_bytes")]
        public long ReferencedBytes { get; set; }

        [Column("orphan_objects")]
        public long OrphanObjects { get; set; }

        [Column("orphan_bytes")]
        public long OrphanBytes { get; set; }

        [Column("deleted_objects")]
        public long DeletedObjects { get; set; }

        [Column("freed_bytes")]
        public long FreedBytes { get; set; }

        [Column("missing_objects")]
        public long MissingObjects { get; set; }

        [Column("missing_indexed_objects")]
        public long MissingIndexedObjects { get; set; }

        [Column("failed_deletes")]
        public long FailedDeletes { get; set; }

        [Column("skipped_objects")]
        public long SkippedObjects { get; set; }

        [Column("uploaded_hash_rows_deleted")]
        public long UploadedHashRowsDeleted { get; set; }

        [Column("current_path")]
        public string? CurrentPath { get; set; }
    }
}
