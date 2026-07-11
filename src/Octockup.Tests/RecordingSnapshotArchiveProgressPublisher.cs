// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Octockup.Server.Abstractions;
using Octockup.Server.Models.Dto;

namespace Octockup.Tests
{
    internal class RecordingSnapshotArchiveProgressPublisher :
        ISnapshotArchiveProgressPublisher
    {
        public List<SnapshotArchiveJobDto> Updates { get; } = [];

        public Task PublishAsync(
            SnapshotArchiveJobDto progress,
            CancellationToken cancellationToken)
        {
            Updates.Add(new SnapshotArchiveJobDto
            {
                JobId = progress.JobId,
                UserId = progress.UserId,
                SnapshotId = progress.SnapshotId,
                Status = progress.Status,
                Phase = progress.Phase,
                CancellationRequested = progress.CancellationRequested,
                StartedAt = progress.StartedAt,
                FinishedAt = progress.FinishedAt,
                ErrorMessage = progress.ErrorMessage,
                TotalFiles = progress.TotalFiles,
                ProcessedFiles = progress.ProcessedFiles,
                TotalBytes = progress.TotalBytes,
                ProcessedBytes = progress.ProcessedBytes,
                PreparedChunkReferences = progress.PreparedChunkReferences,
                CurrentPath = progress.CurrentPath
            });
            return Task.CompletedTask;
        }
    }
}
