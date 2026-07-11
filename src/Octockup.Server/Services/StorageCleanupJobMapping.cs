// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Octockup.Server.Database;
using Octockup.Server.Models.Dto;

namespace Octockup.Server.Services
{
    public static class StorageCleanupJobMapping
    {
        public static StorageCleanupJobDto ToDto(this StorageCleanupJob job)
        {
            return new StorageCleanupJobDto
            {
                JobId = job.Id,
                UserId = job.UserId,
                StorageId = job.StorageId,
                StorageTag = job.StorageTag,
                Status = job.Status,
                Phase = job.Phase,
                StartedAt = job.StartedAt,
                FinishedAt = job.FinishedAt,
                ErrorMessage = job.ErrorMessage,
                SnapshotFilesScanned = job.SnapshotFilesScanned,
                ReferenceCount = job.ReferenceCount,
                ReferencedChunks = job.ReferencedChunks,
                StorageObjectsScanned = job.StorageObjectsScanned,
                StorageBytesScanned = job.StorageBytesScanned,
                ChunkObjectsScanned = job.ChunkObjectsScanned,
                ReferencedObjects = job.ReferencedObjects,
                ReferencedBytes = job.ReferencedBytes,
                OrphanObjects = job.OrphanObjects,
                OrphanBytes = job.OrphanBytes,
                DeletedObjects = job.DeletedObjects,
                FreedBytes = job.FreedBytes,
                MissingObjects = job.MissingObjects,
                FailedDeletes = job.FailedDeletes,
                SkippedObjects = job.SkippedObjects,
                UploadedHashRowsDeleted = job.UploadedHashRowsDeleted,
                CurrentPath = job.CurrentPath
            };
        }
    }
}
