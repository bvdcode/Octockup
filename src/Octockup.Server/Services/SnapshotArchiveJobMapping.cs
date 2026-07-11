// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Octockup.Server.Database;
using Octockup.Server.Models.Dto;

namespace Octockup.Server.Services
{
    public static class SnapshotArchiveJobMapping
    {
        public static SnapshotArchiveJobDto ToDto(this SnapshotArchiveJob job)
        {
            return new SnapshotArchiveJobDto
            {
                JobId = job.Id,
                UserId = job.UserId,
                SnapshotId = job.SnapshotId,
                Status = job.Status,
                Phase = job.Phase,
                CancellationRequested = job.CancellationRequested,
                StartedAt = job.StartedAt,
                FinishedAt = job.FinishedAt,
                ErrorMessage = job.ErrorMessage,
                TotalFiles = job.TotalFiles,
                ProcessedFiles = job.ProcessedFiles,
                TotalBytes = job.TotalBytes,
                ProcessedBytes = job.ProcessedBytes,
                PreparedChunkReferences = job.PreparedChunkReferences,
                CurrentPath = job.CurrentPath
            };
        }
    }
}
