// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Octockup.Server.Database;
using Octockup.Server.Models.Dto;
using Octockup.Server.Models.Enums;

namespace Octockup.Server.Services
{
    public class SnapshotArchiveProgressTracker(
        SnapshotArchiveJob job,
        Guid _runId,
        SnapshotArchiveJobService _jobs,
        TimeProvider _timeProvider)
    {
        private static readonly TimeSpan PublishInterval = TimeSpan.FromSeconds(1);

        private DateTimeOffset _lastPublishedAt = DateTimeOffset.MinValue;

        public SnapshotArchiveJobDto Progress { get; } = job.ToDto();

        public Task ReportPreparationAsync(
            long filesPrepared,
            long chunkReferencesPrepared,
            CancellationToken cancellationToken)
        {
            Progress.Phase = SnapshotArchivePhase.Preparing;
            Progress.ProcessedFiles = filesPrepared;
            Progress.ProcessedBytes = 0;
            Progress.PreparedChunkReferences = chunkReferencesPrepared;
            Progress.CurrentPath = null;
            return PublishIfDueAsync(false, cancellationToken);
        }

        public async Task BeginStreamingAsync(CancellationToken cancellationToken)
        {
            Progress.Phase = SnapshotArchivePhase.Streaming;
            Progress.ProcessedFiles = 0;
            Progress.ProcessedBytes = 0;
            Progress.CurrentPath = null;
            await PublishIfDueAsync(true, cancellationToken).ConfigureAwait(false);
        }

        public Task SetCurrentPathAsync(
            string currentPath,
            CancellationToken cancellationToken)
        {
            Progress.CurrentPath = currentPath;
            return PublishIfDueAsync(false, cancellationToken);
        }

        public Task ReportStreamingAsync(
            long filesProcessed,
            long bytesProcessed,
            CancellationToken cancellationToken)
        {
            Progress.ProcessedFiles = filesProcessed;
            Progress.ProcessedBytes = bytesProcessed;
            return PublishIfDueAsync(false, cancellationToken);
        }

        public Task FlushAsync(CancellationToken cancellationToken)
        {
            return PublishIfDueAsync(true, cancellationToken);
        }

        private async Task PublishIfDueAsync(
            bool force,
            CancellationToken cancellationToken)
        {
            DateTimeOffset now = _timeProvider.GetUtcNow();
            if (!force && now - _lastPublishedAt < PublishInterval)
            {
                return;
            }

            bool updated = await _jobs.UpdateProgressAsync(
                Progress,
                _runId,
                cancellationToken).ConfigureAwait(false);
            if (!updated)
            {
                throw new OperationCanceledException(
                    "Snapshot archive job no longer owns the active run.",
                    cancellationToken);
            }

            _lastPublishedAt = now;
        }
    }
}
