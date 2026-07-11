// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore;
using Octockup.Server.Database;
using Octockup.Server.Models.Dto;
using Octockup.Server.Models.Enums;

namespace Octockup.Server.Services
{
    public class StorageCleanupJobStore(IServiceScopeFactory _scopeFactory)
    {
        public async Task<IReadOnlyList<StorageCleanupJob>> GetRunnableJobsAsync(
            CancellationToken cancellationToken)
        {
            await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
            AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            return await dbContext.StorageCleanupJobs
                .AsNoTracking()
                .Where(x =>
                    x.ActiveStorageId != null &&
                    (x.Status == StorageCleanupStatus.Pending ||
                        x.Status == StorageCleanupStatus.Running))
                .OrderBy(x => x.StartedAt)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task<StorageCleanupJob?> PrepareRunAsync(
            Guid jobId,
            Guid runId,
            CancellationToken cancellationToken)
        {
            await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
            AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            StorageCleanupJob? job = await dbContext.StorageCleanupJobs
                .FirstOrDefaultAsync(
                    x => x.Id == jobId &&
                        x.ActiveStorageId != null &&
                        (x.Status == StorageCleanupStatus.Pending ||
                            x.Status == StorageCleanupStatus.Running),
                    cancellationToken)
                .ConfigureAwait(false);

            if (job is null)
            {
                return null;
            }

            job.RunId = runId;
            job.Status = StorageCleanupStatus.Running;
            job.Phase = StorageCleanupPhase.Preparing;
            job.FinishedAt = null;
            job.ErrorMessage = null;
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return job;
        }

        public async Task<bool> UpdateProgressAsync(
            StorageCleanupJobDto progress,
            Guid runId,
            CancellationToken cancellationToken)
        {
            await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
            AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            StorageCleanupJob? job = await dbContext.StorageCleanupJobs
                .FirstOrDefaultAsync(
                    x => x.Id == progress.JobId && x.RunId == runId,
                    cancellationToken)
                .ConfigureAwait(false);

            if (job is null)
            {
                return false;
            }

            ApplyProgress(job, progress);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }

        public async Task<bool> IsCancellationRequestedAsync(Guid jobId)
        {
            await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
            AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            return await dbContext.StorageCleanupJobs
                .AsNoTracking()
                .Where(x => x.Id == jobId)
                .Select(x => x.CancellationRequested)
                .SingleOrDefaultAsync(CancellationToken.None)
                .ConfigureAwait(false);
        }

        public async Task<bool> CanContinueRunAsync(
            Guid jobId,
            Guid runId,
            CancellationToken cancellationToken)
        {
            await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
            AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            return await dbContext.StorageCleanupJobs
                .AsNoTracking()
                .AnyAsync(
                    x => x.Id == jobId &&
                        x.RunId == runId &&
                        !x.CancellationRequested,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        public async Task<StorageCleanupJobDto?> FinalizePendingCancellationAsync(Guid jobId)
        {
            await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
            AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            StorageCleanupJob? job = await dbContext.StorageCleanupJobs
                .FirstOrDefaultAsync(
                    x => x.Id == jobId &&
                        x.ActiveStorageId != null &&
                        x.RunId == null &&
                        x.CancellationRequested,
                    CancellationToken.None)
                .ConfigureAwait(false);

            if (job is null)
            {
                return null;
            }

            job.Status = StorageCleanupStatus.Canceled;
            job.Phase = StorageCleanupPhase.Completed;
            job.FinishedAt = DateTime.UtcNow;
            job.ActiveStorageId = null;
            job.CurrentPath = null;
            await dbContext.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
            return job.ToDto();
        }

        public async Task<StorageCleanupJobDto?> FinalizeRunAsync(
            StorageCleanupJobDto progress,
            Guid runId,
            StorageCleanupStatus status,
            string? errorMessage)
        {
            await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
            AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            StorageCleanupJob? job = await dbContext.StorageCleanupJobs
                .FirstOrDefaultAsync(
                    x => x.Id == progress.JobId && x.RunId == runId,
                    CancellationToken.None)
                .ConfigureAwait(false);

            if (job is null)
            {
                return null;
            }

            ApplyProgress(job, progress);
            job.Status = status;
            job.Phase = status is StorageCleanupStatus.Completed or StorageCleanupStatus.Canceled
                ? StorageCleanupPhase.Completed
                : progress.Phase;
            job.FinishedAt = DateTime.UtcNow;
            job.ErrorMessage = errorMessage;
            job.ActiveStorageId = null;
            job.RunId = null;
            job.CurrentPath = null;
            await dbContext.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
            return job.ToDto();
        }

        private static void ApplyProgress(StorageCleanupJob job, StorageCleanupJobDto progress)
        {
            job.Phase = progress.Phase;
            job.SnapshotFilesScanned = progress.SnapshotFilesScanned;
            job.ReferenceCount = progress.ReferenceCount;
            job.ReferencedChunks = progress.ReferencedChunks;
            job.StorageObjectsScanned = progress.StorageObjectsScanned;
            job.StorageBytesScanned = progress.StorageBytesScanned;
            job.ChunkObjectsScanned = progress.ChunkObjectsScanned;
            job.ReferencedObjects = progress.ReferencedObjects;
            job.ReferencedBytes = progress.ReferencedBytes;
            job.OrphanObjects = progress.OrphanObjects;
            job.OrphanBytes = progress.OrphanBytes;
            job.DeletedObjects = progress.DeletedObjects;
            job.FreedBytes = progress.FreedBytes;
            job.MissingObjects = progress.MissingObjects;
            job.MissingIndexedObjects = progress.MissingIndexedObjects;
            job.FailedDeletes = progress.FailedDeletes;
            job.SkippedObjects = progress.SkippedObjects;
            job.UploadedHashRowsDeleted = progress.UploadedHashRowsDeleted;
            job.CurrentPath = progress.CurrentPath;
        }
    }
}
