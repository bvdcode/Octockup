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
        ChunkReferenceCollector _chunkReferenceCollector,
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

            List<StorageMaintenanceSummaryDto> summaries = [];
            foreach (Module storage in storages)
            {
                StorageMaintenanceSummaryDto summary = storage.Adapt<StorageMaintenanceSummaryDto>();
                await FillDatabaseStatsAsync(summary, storage.Id, cancellationToken).ConfigureAwait(false);
                await FillCapacityAsync(summary, storage, cancellationToken).ConfigureAwait(false);
                summary.ActiveJob = _jobManager.GetActiveJob(storage.Id);
                summary.LastJob = _jobManager.GetLastJob(storage.Id);
                summaries.Add(summary);
            }

            return summaries;
        }

        public Task<IReadOnlyList<StorageCleanupJobDto>> GetJobsAsync(Guid userId)
        {
            IReadOnlyList<StorageCleanupJobDto> jobs = _jobManager.GetJobs(userId);
            return Task.FromResult(jobs);
        }

        public Task<StorageCleanupJobDto> StartCleanupAsync(
            Guid userId,
            Guid storageId,
            CancellationToken cancellationToken)
        {
            return _jobManager.StartAsync(userId, storageId, cancellationToken);
        }

        public bool CancelCleanup(Guid userId, Guid jobId)
        {
            return _jobManager.Cancel(userId, jobId);
        }

        private async Task FillDatabaseStatsAsync(
            StorageMaintenanceSummaryDto summary,
            Guid storageId,
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

            (HashSet<string> referencedChunks, long referenceCount) = await _chunkReferenceCollector
                .CollectWithReferenceCountForStorageAsync(storageId, cancellationToken)
                .ConfigureAwait(false);

            summary.ReferenceCount = referenceCount;
            summary.ReferencedChunks = referencedChunks.Count;
            summary.DeduplicatedChunks = Math.Max(0, referenceCount - referencedChunks.Count);
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
