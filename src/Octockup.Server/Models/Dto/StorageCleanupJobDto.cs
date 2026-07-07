// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Octockup.Server.Models.Enums;

namespace Octockup.Server.Models.Dto
{
    public class StorageCleanupJobDto
    {
        public Guid JobId { get; set; }
        public Guid UserId { get; set; }
        public Guid StorageId { get; set; }
        public string StorageTag { get; set; } = string.Empty;
        public StorageCleanupStatus Status { get; set; }
        public StorageCleanupPhase Phase { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? FinishedAt { get; set; }
        public string? ErrorMessage { get; set; }
        public long SnapshotFilesScanned { get; set; }
        public long ReferenceCount { get; set; }
        public long ReferencedChunks { get; set; }
        public long StorageObjectsScanned { get; set; }
        public long StorageBytesScanned { get; set; }
        public long ChunkObjectsScanned { get; set; }
        public long ReferencedObjects { get; set; }
        public long ReferencedBytes { get; set; }
        public long OrphanObjects { get; set; }
        public long OrphanBytes { get; set; }
        public long DeletedObjects { get; set; }
        public long FreedBytes { get; set; }
        public long MissingObjects { get; set; }
        public long FailedDeletes { get; set; }
        public long SkippedObjects { get; set; }
        public long UploadedHashRowsDeleted { get; set; }
        public string? CurrentPath { get; set; }
    }
}
