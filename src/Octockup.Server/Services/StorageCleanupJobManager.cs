// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Octockup.Server.Database;
using Octockup.Server.Hubs;
using Octockup.Server.Models.Dto;
using Octockup.Server.Models.Enums;
using System.Collections.Concurrent;

namespace Octockup.Server.Services
{
    public class StorageCleanupJobManager(
        IServiceScopeFactory _scopeFactory,
        IHubContext<EventHub> _hubContext,
        ILogger<StorageCleanupJobManager> _logger)
    {
        private readonly ConcurrentDictionary<Guid, StorageCleanupJobState> _jobs = [];
        private readonly ConcurrentDictionary<Guid, Guid> _activeJobIdsByStorage = [];
        private readonly ConcurrentDictionary<Guid, StorageCleanupJobState> _lastJobsByStorage = [];
        private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _cancellationsByJob = [];

        public async Task<StorageCleanupJobDto> StartAsync(
            Guid userId,
            Guid storageId,
            CancellationToken cancellationToken)
        {
            if (_activeJobIdsByStorage.TryGetValue(storageId, out Guid existingJobId) &&
                _jobs.TryGetValue(existingJobId, out StorageCleanupJobState? existingJob) &&
                existingJob.UserId == userId)
            {
                return existingJob.Snapshot();
            }

            string storageTag = await GetStorageTagAsync(
                userId,
                storageId,
                cancellationToken).ConfigureAwait(false);

            Guid jobId = Guid.NewGuid();
            StorageCleanupJobState state = new(jobId, userId, storageId, storageTag);

            if (!_activeJobIdsByStorage.TryAdd(storageId, jobId))
            {
                if (_activeJobIdsByStorage.TryGetValue(storageId, out Guid activeJobId) &&
                    _jobs.TryGetValue(activeJobId, out StorageCleanupJobState? activeJob) &&
                    activeJob.UserId == userId)
                {
                    return activeJob.Snapshot();
                }

                throw new InvalidOperationException("Cleanup is already running for storage: " + storageId);
            }

            CancellationTokenSource cancellationTokenSource = new();
            _jobs[jobId] = state;
            _cancellationsByJob[jobId] = cancellationTokenSource;

            _ = Task.Run(
                () => RunJobAsync(state, cancellationTokenSource),
                CancellationToken.None);

            return state.Snapshot();
        }

        public IReadOnlyList<StorageCleanupJobDto> GetJobs(Guid userId)
        {
            List<StorageCleanupJobDto> jobs = _jobs.Values
                .Where(x => x.UserId == userId)
                .Select(x => x.Snapshot())
                .ToList();

            HashSet<Guid> activeStorageIds = jobs
                .Select(x => x.StorageId)
                .ToHashSet();

            jobs.AddRange(_lastJobsByStorage.Values
                .Where(x => x.UserId == userId && !activeStorageIds.Contains(x.StorageId))
                .Select(x => x.Snapshot()));

            return jobs
                .OrderByDescending(x => x.StartedAt)
                .ToList();
        }

        public StorageCleanupJobDto? GetActiveJob(Guid storageId)
        {
            if (!_activeJobIdsByStorage.TryGetValue(storageId, out Guid jobId))
            {
                return null;
            }

            return _jobs.TryGetValue(jobId, out StorageCleanupJobState? state)
                ? state.Snapshot()
                : null;
        }

        public StorageCleanupJobDto? GetLastJob(Guid storageId)
        {
            return _lastJobsByStorage.TryGetValue(storageId, out StorageCleanupJobState? state)
                ? state.Snapshot()
                : null;
        }

        public bool Cancel(Guid userId, Guid jobId)
        {
            if (!_jobs.TryGetValue(jobId, out StorageCleanupJobState? state) ||
                state.UserId != userId ||
                !state.IsActive)
            {
                return false;
            }

            if (!_cancellationsByJob.TryGetValue(jobId, out CancellationTokenSource? cancellationTokenSource))
            {
                return false;
            }

            cancellationTokenSource.Cancel();
            return true;
        }

        private async Task<string> GetStorageTagAsync(
            Guid userId,
            Guid storageId,
            CancellationToken cancellationToken)
        {
            using IServiceScope scope = _scopeFactory.CreateScope();
            AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            string? storageTag = await dbContext.Modules
                .AsNoTracking()
                .Where(x => x.Id == storageId &&
                    x.UserId == userId &&
                    x.Destination == ModuleDestination.Target)
                .Select(x => x.Tag)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (storageTag is null)
            {
                throw new InvalidOperationException("Storage not found: " + storageId);
            }

            return storageTag;
        }

        private async Task RunJobAsync(
            StorageCleanupJobState state,
            CancellationTokenSource cancellationTokenSource)
        {
            try
            {
                await PublishAsync(state.Snapshot(), CancellationToken.None).ConfigureAwait(false);

                using IServiceScope scope = _scopeFactory.CreateScope();
                StorageCleanupRunner runner = scope.ServiceProvider.GetRequiredService<StorageCleanupRunner>();
                await runner
                    .RunAsync(state, PublishAsync, cancellationTokenSource.Token)
                    .ConfigureAwait(false);

                state.Update(x =>
                {
                    x.Status = StorageCleanupStatus.Completed;
                    x.FinishedAt = DateTime.UtcNow;
                    x.CurrentPath = null;
                });
            }
            catch (OperationCanceledException) when (cancellationTokenSource.IsCancellationRequested)
            {
                state.Update(x =>
                {
                    x.Status = StorageCleanupStatus.Canceled;
                    x.FinishedAt = DateTime.UtcNow;
                    x.CurrentPath = null;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Storage cleanup job {JobId} failed for storage {StorageId}.",
                    state.JobId,
                    state.StorageId);
                state.Update(x =>
                {
                    x.Status = StorageCleanupStatus.Failed;
                    x.FinishedAt = DateTime.UtcNow;
                    x.ErrorMessage = ex.Message;
                    x.CurrentPath = null;
                });
            }
            finally
            {
                _activeJobIdsByStorage.TryRemove(state.StorageId, out _);
                _jobs.TryRemove(state.JobId, out _);
                _cancellationsByJob.TryRemove(state.JobId, out _);
                _lastJobsByStorage[state.StorageId] = state;
                cancellationTokenSource.Dispose();
                await PublishAsync(state.Snapshot(), CancellationToken.None).ConfigureAwait(false);
            }
        }

        private async Task PublishAsync(
            StorageCleanupJobDto progress,
            CancellationToken cancellationToken)
        {
            try
            {
                await _hubContext.Clients
                    .User(progress.UserId.ToString())
                    .SendAsync("StorageCleanupProgress", progress, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(
                    ex,
                    "Failed to publish storage cleanup progress for job {JobId}.",
                    progress.JobId);
            }
        }
    }
}
