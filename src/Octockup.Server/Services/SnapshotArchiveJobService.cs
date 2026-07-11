// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore;
using Octockup.Server.Abstractions;
using Octockup.Server.Database;
using Octockup.Server.Models.Dto;
using Octockup.Server.Models.Enums;

namespace Octockup.Server.Services
{
    public class SnapshotArchiveJobService(
        AppDbContext _dbContext,
        TimeProvider _timeProvider,
        SnapshotArchiveCancellationRegistry _cancellationRegistry,
        ISnapshotArchiveProgressPublisher _progressPublisher,
        ILogger<SnapshotArchiveJobService> _logger)
    {
        public async Task<SnapshotArchiveJobDto?> StartAsync(
            Guid userId,
            Guid snapshotId,
            CancellationToken cancellationToken)
        {
            var snapshot = await _dbContext.Snapshots
                .AsNoTracking()
                .Where(x =>
                    x.Id == snapshotId &&
                    x.CompletedAt != null &&
                    x.Backup.Source.UserId == userId)
                .Select(x => new
                {
                    x.Id,
                    x.FilesCount,
                    x.TotalSize
                })
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            if (snapshot is null)
            {
                return null;
            }

            SnapshotArchiveJob? existing = await FindActiveAsync(
                userId,
                snapshotId,
                cancellationToken).ConfigureAwait(false);
            if (existing is not null)
            {
                return existing.ToDto();
            }

            SnapshotArchiveJob job = new()
            {
                UserId = userId,
                SnapshotId = snapshot.Id,
                ActiveSnapshotId = snapshot.Id,
                Status = SnapshotArchiveStatus.Pending,
                Phase = SnapshotArchivePhase.Waiting,
                StartedAt = _timeProvider.GetUtcNow().UtcDateTime,
                TotalFiles = snapshot.FilesCount,
                TotalBytes = snapshot.TotalSize
            };
            await _dbContext.SnapshotArchiveJobs.AddAsync(job, cancellationToken);
            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogInformation(
                    ex,
                    "A concurrent archive request already exists for snapshot {SnapshotId}.",
                    snapshotId);
                _dbContext.Entry(job).State = EntityState.Detached;
                existing = await FindActiveAsync(
                    userId,
                    snapshotId,
                    cancellationToken).ConfigureAwait(false);
                if (existing is null)
                {
                    throw;
                }

                job = existing;
            }

            return job.ToDto();
        }

        public async Task<IReadOnlyList<SnapshotArchiveJobDto>?> GetForBackupAsync(
            Guid userId,
            Guid backupId,
            CancellationToken cancellationToken)
        {
            bool backupExists = await _dbContext.Backups
                .AsNoTracking()
                .AnyAsync(
                    x => x.Id == backupId && x.Source.UserId == userId,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!backupExists)
            {
                return null;
            }

            List<SnapshotArchiveJob> jobs = await _dbContext.SnapshotArchiveJobs
                .AsNoTracking()
                .Where(x =>
                    x.UserId == userId &&
                    _dbContext.Snapshots.Any(snapshot =>
                        snapshot.Id == x.SnapshotId &&
                        snapshot.BackupId == backupId))
                .GroupBy(x => x.SnapshotId)
                .Select(group => group
                    .OrderByDescending(x => x.ActiveSnapshotId != null)
                    .ThenByDescending(x => x.StartedAt)
                    .First())
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            return jobs.Select(x => x.ToDto()).ToList();
        }

        public async Task<SnapshotArchiveJob?> ClaimAsync(
            Guid userId,
            Guid jobId,
            Guid runId,
            CancellationToken cancellationToken)
        {
            int updated = await _dbContext.SnapshotArchiveJobs
                .Where(x =>
                    x.Id == jobId &&
                    x.UserId == userId &&
                    x.ActiveSnapshotId != null &&
                    x.Status == SnapshotArchiveStatus.Pending &&
                    !x.CancellationRequested)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(x => x.Status, SnapshotArchiveStatus.Running)
                        .SetProperty(x => x.Phase, SnapshotArchivePhase.Preparing)
                        .SetProperty(x => x.RunId, runId)
                        .SetProperty(x => x.ProcessedFiles, 0)
                        .SetProperty(x => x.ProcessedBytes, 0)
                        .SetProperty(x => x.PreparedChunkReferences, 0)
                        .SetProperty(x => x.CurrentPath, (string?)null)
                        .SetProperty(x => x.ErrorMessage, (string?)null),
                    cancellationToken)
                .ConfigureAwait(false);
            if (updated != 1)
            {
                return null;
            }

            return await _dbContext.SnapshotArchiveJobs
                .AsNoTracking()
                .SingleAsync(x => x.Id == jobId, cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task<bool> UpdateProgressAsync(
            SnapshotArchiveJobDto progress,
            Guid runId,
            CancellationToken cancellationToken)
        {
            int updated = await _dbContext.SnapshotArchiveJobs
                .Where(x =>
                    x.Id == progress.JobId &&
                    x.RunId == runId &&
                    !x.CancellationRequested)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(x => x.ProcessedFiles, progress.ProcessedFiles)
                        .SetProperty(x => x.ProcessedBytes, progress.ProcessedBytes)
                        .SetProperty(
                            x => x.PreparedChunkReferences,
                            progress.PreparedChunkReferences)
                        .SetProperty(x => x.Phase, progress.Phase)
                        .SetProperty(x => x.CurrentPath, progress.CurrentPath),
                    cancellationToken)
                .ConfigureAwait(false);
            if (updated != 1)
            {
                return false;
            }

            await _progressPublisher.PublishAsync(progress, cancellationToken).ConfigureAwait(false);
            return true;
        }

        public async Task<SnapshotArchiveJobDto?> FinalizeAsync(
            SnapshotArchiveJobDto progress,
            Guid runId,
            SnapshotArchiveStatus status,
            string? errorMessage)
        {
            DateTime finishedAt = _timeProvider.GetUtcNow().UtcDateTime;
            int updated = await _dbContext.SnapshotArchiveJobs
                .Where(x => x.Id == progress.JobId && x.RunId == runId)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(x => x.Status, status)
                        .SetProperty(x => x.ActiveSnapshotId, (Guid?)null)
                        .SetProperty(x => x.RunId, (Guid?)null)
                        .SetProperty(x => x.FinishedAt, finishedAt)
                        .SetProperty(x => x.ErrorMessage, errorMessage)
                        .SetProperty(x => x.ProcessedFiles, progress.ProcessedFiles)
                        .SetProperty(x => x.ProcessedBytes, progress.ProcessedBytes)
                        .SetProperty(x => x.Phase, progress.Phase)
                        .SetProperty(
                            x => x.PreparedChunkReferences,
                            progress.PreparedChunkReferences)
                        .SetProperty(x => x.CurrentPath, (string?)null),
                    CancellationToken.None)
                .ConfigureAwait(false);
            if (updated != 1)
            {
                return null;
            }

            SnapshotArchiveJob job = await _dbContext.SnapshotArchiveJobs
                .AsNoTracking()
                .SingleAsync(x => x.Id == progress.JobId, CancellationToken.None)
                .ConfigureAwait(false);
            SnapshotArchiveJobDto result = job.ToDto();
            await _progressPublisher.PublishAsync(result, CancellationToken.None).ConfigureAwait(false);
            return result;
        }

        public async Task<bool> CancelAsync(
            Guid userId,
            Guid jobId,
            CancellationToken cancellationToken)
        {
            DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
            int pendingCanceled = await _dbContext.SnapshotArchiveJobs
                .Where(x =>
                    x.Id == jobId &&
                    x.UserId == userId &&
                    x.ActiveSnapshotId != null &&
                    x.Status == SnapshotArchiveStatus.Pending)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(x => x.Status, SnapshotArchiveStatus.Canceled)
                        .SetProperty(x => x.ActiveSnapshotId, (Guid?)null)
                        .SetProperty(x => x.FinishedAt, now)
                        .SetProperty(x => x.CancellationRequested, true),
                    cancellationToken)
                .ConfigureAwait(false);
            if (pendingCanceled == 1)
            {
                await PublishCurrentAsync(jobId, cancellationToken).ConfigureAwait(false);
                return true;
            }

            int runningCanceled = await _dbContext.SnapshotArchiveJobs
                .Where(x =>
                    x.Id == jobId &&
                    x.UserId == userId &&
                    x.ActiveSnapshotId != null &&
                    x.Status == SnapshotArchiveStatus.Running)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(x => x.CancellationRequested, true),
                    cancellationToken)
                .ConfigureAwait(false);
            if (runningCanceled == 1)
            {
                _cancellationRegistry.Cancel(jobId);
                await PublishCurrentAsync(jobId, cancellationToken).ConfigureAwait(false);
                return true;
            }

            return false;
        }

        public async Task<bool> IsCancellationRequestedAsync(Guid jobId)
        {
            return await _dbContext.SnapshotArchiveJobs
                .AsNoTracking()
                .Where(x => x.Id == jobId)
                .Select(x => x.CancellationRequested)
                .SingleOrDefaultAsync(CancellationToken.None)
                .ConfigureAwait(false);
        }

        public async Task RecoverInterruptedAsync(CancellationToken cancellationToken)
        {
            DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
            await _dbContext.SnapshotArchiveJobs
                .Where(x => x.Status == SnapshotArchiveStatus.Running)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(x => x.Status, SnapshotArchiveStatus.Failed)
                        .SetProperty(x => x.ActiveSnapshotId, (Guid?)null)
                        .SetProperty(x => x.RunId, (Guid?)null)
                        .SetProperty(x => x.FinishedAt, now)
                        .SetProperty(
                            x => x.ErrorMessage,
                            "Archive stream was interrupted by a server restart."),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        private Task<SnapshotArchiveJob?> FindActiveAsync(
            Guid userId,
            Guid snapshotId,
            CancellationToken cancellationToken)
        {
            return _dbContext.SnapshotArchiveJobs
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.UserId == userId && x.ActiveSnapshotId == snapshotId,
                    cancellationToken);
        }

        private async Task PublishCurrentAsync(
            Guid jobId,
            CancellationToken cancellationToken)
        {
            SnapshotArchiveJob job = await _dbContext.SnapshotArchiveJobs
                .AsNoTracking()
                .SingleAsync(x => x.Id == jobId, cancellationToken)
                .ConfigureAwait(false);
            await _progressPublisher
                .PublishAsync(job.ToDto(), cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
