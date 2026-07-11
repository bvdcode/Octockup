// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.Abstractions;
using Mapster;
using Microsoft.EntityFrameworkCore;
using Octockup.Server.Abstractions;
using Octockup.Server.Database;
using Octockup.Server.Models;
using Octockup.Server.Models.Dto;
using Octockup.Server.Models.Enums;

namespace Octockup.Server.Services
{
    public class StorageMaintenanceService(
        IStreamCipher _crypto,
        AppDbContext _dbContext,
        ILogger<StorageMaintenanceService> _logger,
        IEnumerable<IBackupProvider> _providers,
        StorageCleanupJobManager _jobManager)
    {
        public async Task<IReadOnlyList<StorageMaintenanceSummaryDto>> GetSummariesAsync(
            Guid userId,
            CancellationToken cancellationToken)
        {
            List<Module> storages = await _dbContext.Modules
                .AsNoTracking()
                .Where(x => x.UserId == userId && x.Destination == ModuleDestination.Target)
                .OrderBy(x => x.Tag)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            IReadOnlyList<StorageCleanupJobDto> jobs = await _jobManager
                .GetJobsAsync(userId, cancellationToken)
                .ConfigureAwait(false);
            Dictionary<Guid, StorageCleanupJobDto> jobsByStorage = jobs
                .ToDictionary(x => x.StorageId);

            List<StorageMaintenanceSummaryDto> summaries = [];
            foreach (Module storage in storages)
            {
                StorageMaintenanceSummaryDto summary = storage.Adapt<StorageMaintenanceSummaryDto>();
                if (jobsByStorage.TryGetValue(storage.Id, out StorageCleanupJobDto? job))
                {
                    AssignJob(summary, job);
                }
                summaries.Add(summary);
            }

            return summaries;
        }

        public async Task<StorageMaintenanceSummaryDto> GetStorageStatsAsync(
            Guid userId,
            Guid storageId,
            CancellationToken cancellationToken)
        {
            Module? storage = await _dbContext.Modules
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

            StorageMaintenanceSummaryDto summary = storage.Adapt<StorageMaintenanceSummaryDto>();
            IReadOnlyList<StorageCleanupJobDto> jobs = await _jobManager
                .GetJobsAsync(userId, cancellationToken)
                .ConfigureAwait(false);
            StorageCleanupJobDto? job = jobs.FirstOrDefault(x => x.StorageId == storage.Id);
            await FillDatabaseStatsAsync(summary, storage.Id, job, cancellationToken).ConfigureAwait(false);
            await FillCapacityAsync(summary, storage, cancellationToken).ConfigureAwait(false);
            if (job is not null)
            {
                AssignJob(summary, job);
            }
            return summary;
        }

        public Task<IReadOnlyList<StorageCleanupJobDto>> GetJobsAsync(
            Guid userId,
            CancellationToken cancellationToken)
        {
            return _jobManager.GetJobsAsync(userId, cancellationToken);
        }

        public Task<StorageCleanupJobDto> StartCleanupAsync(
            Guid userId,
            Guid storageId,
            CancellationToken cancellationToken)
        {
            return _jobManager.StartAsync(userId, storageId, cancellationToken);
        }

        public Task<bool> CancelCleanupAsync(
            Guid userId,
            Guid jobId,
            CancellationToken cancellationToken)
        {
            return _jobManager.CancelAsync(userId, jobId, cancellationToken);
        }

        private async Task FillDatabaseStatsAsync(
            StorageMaintenanceSummaryDto summary,
            Guid storageId,
            StorageCleanupJobDto? job,
            CancellationToken cancellationToken)
        {
            summary.TotalBackups = await _dbContext.Backups
                .Where(x => x.StorageId == storageId)
                .CountAsync(cancellationToken)
                .ConfigureAwait(false);

            var chunkSizes = await _dbContext.UploadedHashes
                .AsNoTracking()
                .Where(x => x.ModuleId == storageId)
                .GroupBy(x => x.ModuleId)
                .Select(x => new
                {
                    IndexedObjects = x.Count(),
                    IndexedOriginalSize = x.Sum(c => (long?)c.OriginalSize) ?? 0,
                    IndexedStoredSize = x.Sum(c => (long?)c.StoredSize) ?? 0
                })
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            summary.IndexedObjects = chunkSizes?.IndexedObjects ?? 0;
            summary.IndexedOriginalSize = chunkSizes?.IndexedOriginalSize ?? 0;
            summary.IndexedStoredSize = chunkSizes?.IndexedStoredSize ?? 0;

            if (job is null)
            {
                return;
            }

            summary.ReferenceCount = job.ReferenceCount;
            summary.ReferencedChunks = job.ReferencedChunks;
            summary.DeduplicatedChunks = Math.Max(0, job.ReferenceCount - job.ReferencedChunks);
        }

        private static void AssignJob(
            StorageMaintenanceSummaryDto summary,
            StorageCleanupJobDto job)
        {
            if (job.Status is StorageCleanupStatus.Pending or StorageCleanupStatus.Running)
            {
                summary.ActiveJob = job;
                return;
            }

            summary.LastJob = job;
        }

        private async Task FillCapacityAsync(
            StorageMaintenanceSummaryDto summary,
            Module storage,
            CancellationToken cancellationToken)
        {
            IBackupProvider? provider = _providers.FirstOrDefault(x => x.Id == storage.BackupModuleId);
            if (provider is not IBackupStorageCapacityProvider capacityProvider)
            {
                return;
            }

            try
            {
                provider.SetParameters(storage.Params(_crypto).Snapshot());
                StorageCapacityInfo? capacity = await capacityProvider
                    .GetCapacityAsync(cancellationToken)
                    .ConfigureAwait(false);

                summary.TotalCapacityBytes = capacity?.TotalBytes;
                summary.AvailableBytes = capacity?.AvailableBytes;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to read capacity for storage {StorageId}.",
                    storage.Id);
            }
        }
    }
}
