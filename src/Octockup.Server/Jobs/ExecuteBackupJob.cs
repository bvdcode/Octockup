// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using EasyExtensions.Abstractions;
using EasyExtensions.Quartz.Attributes;
using Microsoft.AspNetCore.SignalR;
using Octockup.Server.Abstractions;
using Octockup.Server.Database;
using Octockup.Server.Helpers;
using Octockup.Server.Hubs;
using Quartz;
using System.Collections.Concurrent;

namespace Octockup.Server.Jobs
{
    [JobTrigger(minutes: 1, disallowConcurrentExecution: false)]
    public class ExecuteBackupJob(
        IServiceProvider _serviceProvider,
        ILogger<ExecuteBackupJob> _logger) : IJob
    {
        private static readonly ConcurrentDictionary<Guid, CancellationTokenSource> _runningSchedules = new();

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
            using IServiceScope scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var runningIds = _runningSchedules.Keys.ToArray();
            var query = dbContext.Schedules.AsQueryable();

            if (runningIds.Length > 0)
            {
                query = query.Where(x => !runningIds.Contains(x.Id));
            }

            Schedule? next = await ScheduleHelpers.GetNextScheduleAsync(query, context.CancellationToken);
            if (next == null)
            {
                _logger.LogDebug("No schedules ready for execution at {Time}", DateTimeOffset.UtcNow);
                return;
            }

            using CancellationTokenSource scheduleCts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);

            if (!_runningSchedules.TryAdd(next.Id, scheduleCts))
            {
                _logger.LogInformation("Schedule {ScheduleId} is already running, skipping duplicate run attempt", next.Id);
                return;
            }

            try
            {
                _logger.LogInformation("Starting backup job for schedule {ScheduleId}", next.Id);
                var runnerLogger = scope.ServiceProvider.GetRequiredService<ILogger<BackupRunner>>();
                var runner = new BackupRunner(
                    scope.ServiceProvider.GetRequiredService<IStreamCipher>(),
                    dbContext,
                    scope.ServiceProvider,
                    runnerLogger,
                    scope.ServiceProvider.GetRequiredService<IHubContext<EventHub>>(),
                    scope.ServiceProvider.GetRequiredService<IEnumerable<IBackupProvider>>());

                await runner.RunAsync(next, scheduleCts.Token);
                _logger.LogInformation("Backup job for schedule {ScheduleId} completed", next.Id);
            }
            finally
            {
                _runningSchedules.TryRemove(next.Id, out _);
                _logger.LogInformation("Finished backup job for schedule {ScheduleId}", next.Id);
            }
        }
    }
}
