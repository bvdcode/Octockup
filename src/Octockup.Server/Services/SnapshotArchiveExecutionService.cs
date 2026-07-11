// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore;
using Octockup.Server.Archives;
using Octockup.Server.Database;
using Octockup.Server.Models.Enums;

namespace Octockup.Server.Services
{
    public class SnapshotArchiveExecutionService(
        AppDbContext _dbContext,
        SnapshotArchiveJobService _jobs,
        SnapshotArchiveRunner _runner,
        SnapshotArchiveCancellationRegistry _cancellations,
        TimeProvider _timeProvider,
        ILogger<SnapshotArchiveExecutionService> _logger)
    {
        public async Task<SnapshotArchiveRunContext?> BeginAsync(
            Guid userId,
            Guid jobId,
            CancellationToken cancellationToken)
        {
            Guid runId = Guid.NewGuid();
            SnapshotArchiveJob? job = await _jobs.ClaimAsync(
                userId,
                jobId,
                runId,
                cancellationToken).ConfigureAwait(false);
            if (job is null)
            {
                return null;
            }

            var snapshot = await _dbContext.Snapshots
                .AsNoTracking()
                .Where(x =>
                    x.Id == job.SnapshotId &&
                    x.CompletedAt != null &&
                    x.Backup.Source.UserId == userId)
                .Select(x => new
                {
                    x.Id,
                    x.CreatedAt,
                    CompletedAt = x.CompletedAt!.Value,
                    x.Backup.Tag
                })
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            if (snapshot is null)
            {
                SnapshotArchiveProgressTracker missingSnapshotProgress = new(
                    job,
                    runId,
                    _jobs,
                    _timeProvider);
                await _jobs.FinalizeAsync(
                    missingSnapshotProgress.Progress,
                    runId,
                    SnapshotArchiveStatus.Failed,
                    "The completed snapshot no longer exists.").ConfigureAwait(false);
                return null;
            }

            string fileName = SnapshotArchiveFileName.Create(
                snapshot.Tag,
                snapshot.CreatedAt,
                snapshot.CompletedAt,
                snapshot.Id);
            return new SnapshotArchiveRunContext(job, runId, fileName);
        }

        public async Task ExecuteAsync(
            SnapshotArchiveRunContext context,
            Stream output,
            CancellationToken requestCancellationToken)
        {
            SnapshotArchiveProgressTracker progress = new(
                context.Job,
                context.RunId,
                _jobs,
                _timeProvider);
            using CancellationTokenSource executionCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(requestCancellationToken);
            if (!_cancellations.TryRegister(context.Job.Id, executionCancellation))
            {
                await _jobs.FinalizeAsync(
                    progress.Progress,
                    context.RunId,
                    SnapshotArchiveStatus.Failed,
                    "Archive execution could not be registered.").ConfigureAwait(false);
                throw new InvalidOperationException(
                    "Snapshot archive execution is already registered.");
            }

            try
            {
                await _runner.WriteAsync(
                    context.Job,
                    progress,
                    output,
                    executionCancellation.Token).ConfigureAwait(false);
                await _jobs.FinalizeAsync(
                    progress.Progress,
                    context.RunId,
                    SnapshotArchiveStatus.Completed,
                    null).ConfigureAwait(false);
            }
            catch (OperationCanceledException ex)
            {
                bool cancellationRequested = await _jobs
                    .IsCancellationRequestedAsync(context.Job.Id)
                    .ConfigureAwait(false);
                SnapshotArchiveStatus status = cancellationRequested
                    ? SnapshotArchiveStatus.Canceled
                    : SnapshotArchiveStatus.Failed;
                string? errorMessage = cancellationRequested
                    ? null
                    : "Archive stream was interrupted before completion.";
                await _jobs.FinalizeAsync(
                    progress.Progress,
                    context.RunId,
                    status,
                    errorMessage).ConfigureAwait(false);

                if (cancellationRequested)
                {
                    _logger.LogInformation(
                        "Snapshot archive job {JobId} was canceled.",
                        context.Job.Id);
                }
                else
                {
                    _logger.LogWarning(
                        ex,
                        "Snapshot archive job {JobId} was interrupted.",
                        context.Job.Id);
                }

                throw;
            }
            catch (Exception ex)
            {
                await _jobs.FinalizeAsync(
                    progress.Progress,
                    context.RunId,
                    SnapshotArchiveStatus.Failed,
                    "Archive generation failed. Check the server logs.").ConfigureAwait(false);
                _logger.LogError(
                    ex,
                    "Snapshot archive job {JobId} failed.",
                    context.Job.Id);
                throw;
            }
            finally
            {
                _cancellations.Unregister(context.Job.Id, executionCancellation);
            }
        }
    }
}
