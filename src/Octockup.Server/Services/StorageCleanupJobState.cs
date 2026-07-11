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
            string storageTag,
            DateTime startedAt)
            : this(new StorageCleanupJobDto
            {
                JobId = jobId,
                UserId = userId,
                StorageId = storageId,
                StorageTag = storageTag,
                Status = StorageCleanupStatus.Pending,
                Phase = StorageCleanupPhase.Preparing,
                StartedAt = startedAt
            })
        {
        }

        public StorageCleanupJobState(StorageCleanupJobDto initialValue)
        {
            _value = Clone(initialValue);
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
                return Clone(_value);
            }
        }

        private static StorageCleanupJobDto Clone(StorageCleanupJobDto value)
        {
            return new StorageCleanupJobDto
            {
                JobId = value.JobId,
                UserId = value.UserId,
                StorageId = value.StorageId,
                StorageTag = value.StorageTag,
                Status = value.Status,
                Phase = value.Phase,
                StartedAt = value.StartedAt,
                FinishedAt = value.FinishedAt,
                ErrorMessage = value.ErrorMessage,
                SnapshotFilesScanned = value.SnapshotFilesScanned,
                ReferenceCount = value.ReferenceCount,
                ReferencedChunks = value.ReferencedChunks,
                StorageObjectsScanned = value.StorageObjectsScanned,
                StorageBytesScanned = value.StorageBytesScanned,
                ChunkObjectsScanned = value.ChunkObjectsScanned,
                ReferencedObjects = value.ReferencedObjects,
                ReferencedBytes = value.ReferencedBytes,
                OrphanObjects = value.OrphanObjects,
                OrphanBytes = value.OrphanBytes,
                DeletedObjects = value.DeletedObjects,
                FreedBytes = value.FreedBytes,
                MissingObjects = value.MissingObjects,
                FailedDeletes = value.FailedDeletes,
                SkippedObjects = value.SkippedObjects,
                UploadedHashRowsDeleted = value.UploadedHashRowsDeleted,
                CurrentPath = value.CurrentPath
            };
        }
    }
}
