// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Octockup.Server.Models.Dto;
using Octockup.Server.Models.Enums;

namespace Octockup.Server.Services
{
    public class StorageCleanupJobState
    {
        private readonly object _sync = new();
        private readonly StorageCleanupJobDto _value;

        public StorageCleanupJobState(
            Guid jobId,
            Guid userId,
            Guid storageId,
            string storageTag)
        {
            _value = new StorageCleanupJobDto
            {
                JobId = jobId,
                UserId = userId,
                StorageId = storageId,
                StorageTag = storageTag,
                Status = StorageCleanupStatus.Pending,
                StartedAt = DateTime.UtcNow
            };
        }

        public Guid JobId => _value.JobId;
        public Guid UserId => _value.UserId;
        public Guid StorageId => _value.StorageId;

        public bool IsActive
        {
            get
            {
                lock (_sync)
                {
                    return _value.Status is StorageCleanupStatus.Pending or StorageCleanupStatus.Running;
                }
            }
        }

        public void Update(Action<StorageCleanupJobDto> update)
        {
            lock (_sync)
            {
                update(_value);
            }
        }

        public StorageCleanupJobDto Snapshot()
        {
            lock (_sync)
            {
                return new StorageCleanupJobDto
                {
                    JobId = _value.JobId,
                    UserId = _value.UserId,
                    StorageId = _value.StorageId,
                    StorageTag = _value.StorageTag,
                    Status = _value.Status,
                    StartedAt = _value.StartedAt,
                    FinishedAt = _value.FinishedAt,
                    ErrorMessage = _value.ErrorMessage,
                    ReferenceCount = _value.ReferenceCount,
                    ReferencedChunks = _value.ReferencedChunks,
                    StorageObjectsScanned = _value.StorageObjectsScanned,
                    StorageBytesScanned = _value.StorageBytesScanned,
                    ChunkObjectsScanned = _value.ChunkObjectsScanned,
                    ReferencedObjects = _value.ReferencedObjects,
                    ReferencedBytes = _value.ReferencedBytes,
                    OrphanObjects = _value.OrphanObjects,
                    OrphanBytes = _value.OrphanBytes,
                    DeletedObjects = _value.DeletedObjects,
                    FreedBytes = _value.FreedBytes,
                    MissingObjects = _value.MissingObjects,
                    FailedDeletes = _value.FailedDeletes,
                    SkippedObjects = _value.SkippedObjects,
                    UploadedHashRowsDeleted = _value.UploadedHashRowsDeleted,
                    CurrentPath = _value.CurrentPath
                };
            }
        }
    }
}
