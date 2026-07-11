// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.Abstractions;
using EasyExtensions.Quartz.Attributes;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Octockup.Server.Abstractions;
using Octockup.Server.Database;
using Octockup.Server.Helpers;
using Octockup.Server.Hubs;
using Octockup.Server.Models.Enums;
using Octockup.Server.Models.Options;
using Octockup.Server.Services;
using Quartz;
using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace Octockup.Server.Jobs
{
    [JobTrigger(minutes: 1, disallowConcurrentExecution: false)]
    public class ExecuteBackupJob(
        IServiceProvider _serviceProvider,
        IOptions<BackupExecutionOptions> _options,
        ILogger<ExecuteBackupJob> _logger) : IJob
    {
        private static readonly ConcurrentDictionary<Guid, CancellationTokenSource> _runningSchedules = new();

        public static bool IsScheduleRunning(Guid scheduleId)
        {
            return _runningSchedules.ContainsKey(scheduleId);
        }

        public static void StopRunningBackup(Guid scheduleId)
        {
            if (!_runningSchedules.TryGetValue(scheduleId, out var cts))
            {
                return;
            }

            try
            {
                cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Ignore
            }
        }

        public async Task Execute(IJobExecutionContext context)
        {
            int capacity = GetAvailableCapacity();
            if (capacity <= 0)
            {
                _logger.LogDebug("Backup execution capacity is full at {Time}.", DateTimeOffset.UtcNow);
                return;
            }

            IReadOnlyList<Guid> scheduleIds = await GetReadyScheduleIdsAsync(
                capacity,
                context.CancellationToken);

            if (scheduleIds.Count == 0)
            {
                _logger.LogDebug("No schedules ready for execution at {Time}", DateTimeOffset.UtcNow);
                return;
            }

            List<Task> tasks = [];
            foreach (Guid scheduleId in scheduleIds)
            {
                CancellationTokenSource scheduleCts = CancellationTokenSource
                    .CreateLinkedTokenSource(context.CancellationToken);

                if (!_runningSchedules.TryAdd(scheduleId, scheduleCts))
                {
                    _logger.LogInformation("Schedule {ScheduleId} is already running, skipping duplicate run attempt", scheduleId);
                    scheduleCts.Dispose();
                    continue;
                }

                tasks.Add(RunScheduleAsync(scheduleId, scheduleCts));
            }

            await Task.WhenAll(tasks);
        }

        private int GetAvailableCapacity()
        {
            return _options.Value.MaxConcurrentBackups - _runningSchedules.Count;
        }

        private async Task<IReadOnlyList<Guid>> GetReadyScheduleIdsAsync(
            int maxCount,
            CancellationToken cancellationToken)
        {
            using IServiceScope scope = _serviceProvider.CreateScope();
            AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            Guid[] runningIds = _runningSchedules.Keys.ToArray();
            IQueryable<Schedule> query = dbContext.Schedules.AsQueryable();

            if (runningIds.Length > 0)
            {
                query = query.Where(x => !runningIds.Contains(x.Id));
            }

            IReadOnlyList<Schedule> schedules = await ScheduleHelpers.GetReadySchedulesAsync(
                query,
                maxCount,
                cancellationToken);

            return schedules.Select(x => x.Id).ToList();
        }

        private async Task RunScheduleAsync(
            Guid scheduleId,
            CancellationTokenSource scheduleCts)
        {
            using IServiceScope scope = _serviceProvider.CreateScope();
            AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            try
            {
                Schedule? schedule = await dbContext.Schedules
                    .Include(x => x.Backup)
                    .ThenInclude(x => x.Source)
                    .Include(x => x.Backup)
                    .ThenInclude(x => x.Storage)
                    .FirstOrDefaultAsync(x => x.Id == scheduleId, scheduleCts.Token);

                if (schedule is null)
                {
                    _logger.LogWarning("Schedule {ScheduleId} was deleted before execution.", scheduleId);
                    return;
                }

                IStorageOperationCoordinator operationCoordinator = scope.ServiceProvider
                    .GetRequiredService<IStorageOperationCoordinator>();
                IStorageOperationLease? storageLease = await operationCoordinator
                    .TryAcquireAsync(
                        schedule.Backup.StorageId,
                        StorageOperationKind.Backup,
                        scheduleCts.Token)
                    .ConfigureAwait(false);

                if (storageLease is null)
                {
                    schedule.Status = ScheduleStatus.Created;
                    schedule.ErrorMessage = null;
                    schedule.FinishedAt = null;
                    await dbContext.SaveChangesAsync(scheduleCts.Token);
                    _logger.LogInformation(
                        "Deferred schedule {ScheduleId} because storage {StorageId} is busy.",
                        schedule.Id,
                        schedule.Backup.StorageId);
                    return;
                }

                await using (storageLease)
                using (CancellationTokenSource operationCts = CancellationTokenSource
                    .CreateLinkedTokenSource(scheduleCts.Token, storageLease.LeaseLostToken))
                {
                    _logger.LogInformation("Starting backup job for schedule {ScheduleId}", schedule.Id);
                    ILogger<BackupRunner> runnerLogger = scope.ServiceProvider.GetRequiredService<ILogger<BackupRunner>>();
                    BackupRunner runner = new(
                        scope.ServiceProvider.GetRequiredService<IStreamCipher>(),
                        dbContext,
                        scope.ServiceProvider,
                        runnerLogger,
                        scope.ServiceProvider.GetRequiredService<IHubContext<EventHub>>(),
                        scope.ServiceProvider.GetRequiredService<IEnumerable<IBackupProvider>>(),
                        scope.ServiceProvider.GetRequiredService<UploadedChunkLookup>(),
                        scope.ServiceProvider.GetRequiredService<PreviousSnapshotFileLookup>());

                    await runner.RunAsync(schedule, operationCts.Token);
                    _logger.LogInformation("Backup job for schedule {ScheduleId} completed", schedule.Id);
                }
            }
            catch (OperationCanceledException) when (scheduleCts.IsCancellationRequested)
            {
                _logger.LogInformation("Backup job wrapper canceled for schedule {ScheduleId}", scheduleId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Backup job wrapper failed for schedule {ScheduleId}", scheduleId);
            }
            finally
            {
                _runningSchedules.TryRemove(scheduleId, out _);
                scheduleCts.Dispose();
                _logger.LogInformation("Finished backup job for schedule {ScheduleId}", scheduleId);
            }
        }
    }
}
