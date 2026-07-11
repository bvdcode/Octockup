// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore;
using Octockup.Server.Abstractions;
using Octockup.Server.Database;
using Octockup.Server.Models.Dto;
using Octockup.Server.Models.Enums;

namespace Octockup.Server.Services
{
    public class StorageCleanupJobManager(
        IServiceScopeFactory _scopeFactory,
        IStorageCleanupJobScheduler _scheduler,
        StorageCleanupCancellationRegistry _cancellationRegistry,
        ILogger<StorageCleanupJobManager> _logger)
    {
        public async Task<StorageCleanupJobDto> StartAsync(
            Guid userId,
            Guid storageId,
            CancellationToken cancellationToken)
        {
            await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
            AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            Module? storage = await dbContext.Modules
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.Id == storageId &&
                        x.UserId == userId &&
                        x.Destination == ModuleDestination.Target,
                    cancellationToken)
                .ConfigureAwait(false);

            if (storage is null)
            {
                throw new InvalidOperationException("Storage not found: " + storageId);
            }

            StorageCleanupJob? existingJob = await FindActiveJobAsync(
                    dbContext,
                    userId,
                    storageId,
                    cancellationToken)
                .ConfigureAwait(false);

            if (existingJob is not null)
            {
                await _scheduler.TriggerAsync();
                return existingJob.ToDto();
            }

            DateTime startedAt = DateTime.UtcNow;
            StorageCleanupJob job = new()
            {
                UserId = userId,
                StorageId = storageId,
                ActiveStorageId = storageId,
                StorageTag = storage.Tag,
                Status = StorageCleanupStatus.Pending,
                Phase = StorageCleanupPhase.Preparing,
                StartedAt = startedAt
            };

            await dbContext.StorageCleanupJobs
                .AddAsync(job, cancellationToken)
                .ConfigureAwait(false);

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateException ex)
            {
                _logger.LogInformation(
                    ex,
                    "A concurrent cleanup request already created an active job for storage {StorageId}.",
                    storageId);
                dbContext.Entry(job).State = EntityState.Detached;

                existingJob = await FindActiveJobAsync(
                        dbContext,
                        userId,
                        storageId,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (existingJob is null)
                {
                    _logger.LogError(
                        ex,
                        "Failed to create cleanup job for storage {StorageId} and no active job exists.",
                        storageId);
                    throw;
                }

                job = existingJob;
            }

            await _scheduler.TriggerAsync();
            return job.ToDto();
        }

        public async Task<IReadOnlyList<StorageCleanupJobDto>> GetJobsAsync(
            Guid userId,
            CancellationToken cancellationToken)
        {
            await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
            AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            List<StorageCleanupJob> jobs = await dbContext.StorageCleanupJobs
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .GroupBy(x => x.StorageId)
                .Select(group => group
                    .OrderByDescending(x => x.ActiveStorageId != null)
                    .ThenByDescending(x => x.StartedAt)
                    .First())
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return jobs
                .OrderByDescending(x => x.StartedAt)
                .Select(x => x.ToDto())
                .ToList();
        }

        public async Task<bool> CancelAsync(
            Guid userId,
            Guid jobId,
            CancellationToken cancellationToken)
        {
            await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
            AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            int updated = await dbContext.StorageCleanupJobs
                .Where(x =>
                    x.Id == jobId &&
                    x.UserId == userId &&
                    x.ActiveStorageId != null &&
                    (x.Status == StorageCleanupStatus.Pending ||
                        x.Status == StorageCleanupStatus.Running))
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(x => x.CancellationRequested, true),
                    cancellationToken)
                .ConfigureAwait(false);

            if (updated != 1)
            {
                return false;
            }

            _cancellationRegistry.Cancel(jobId);
            await _scheduler.TriggerAsync();
            return true;
        }

        private static Task<StorageCleanupJob?> FindActiveJobAsync(
            AppDbContext dbContext,
            Guid userId,
            Guid storageId,
            CancellationToken cancellationToken)
        {
            return dbContext.StorageCleanupJobs
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.UserId == userId && x.ActiveStorageId == storageId,
                    cancellationToken);
        }
    }
}
