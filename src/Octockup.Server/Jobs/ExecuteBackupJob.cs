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
    [JobTrigger(minutes: 1)]
    public class ExecuteBackupJob(
        IServiceProvider _serviceProvider,
        ILogger<ExecuteBackupJob> _logger) : IJob
    {
        private static readonly ConcurrentDictionary<Guid, CancellationTokenSource> _stoppingSchedules = new();

        public static void StopRunningBackup(Guid scheduleId)
        {
            if (!_stoppingSchedules.TryRemove(scheduleId, out CancellationTokenSource? cts))
            {
                return;
            }

            try
            {
                cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        public async Task Execute(IJobExecutionContext context)
        {
            List<Task> tasks = [];
            List<IServiceScope> scopes = [];
            const int concurrencyLevel = 2;
            List<Guid> runningIds = [];

            for (int i = 0; i < concurrencyLevel; i++)
            {
                var scope = _serviceProvider.CreateScope();
                scopes.Add(scope);
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var query = dbContext.Schedules.Where(x => !runningIds.Contains(x.Id));
                Schedule? next = await ScheduleHelpers.GetNextScheduleAsync(query, context.CancellationToken);
                if (next == null)
                {
                    _logger.LogDebug("No schedules ready for execution at {Time}", DateTimeOffset.UtcNow);
                    break;
                }

                _logger.LogInformation("Starting backup job for schedule {ScheduleId}", next.Id);
                var runner = new BackupRunner(
                    scope.ServiceProvider.GetRequiredService<IStreamCipher>(),
                    dbContext,
                    scope.ServiceProvider,
                    scope.ServiceProvider.GetRequiredService<ILogger<BackupRunner>>(),
                    scope.ServiceProvider.GetRequiredService<IHubContext<EventHub>>(),
                    scope.ServiceProvider.GetRequiredService<IEnumerable<IBackupProvider>>());
                Task task = RunTaskAsync(next, runner, context.CancellationToken);
                tasks.Add(task);
                runningIds.Add(next.Id);
            }

            await Task.WhenAll(tasks);
            _logger.LogInformation("All backup jobs completed at {Time}", DateTimeOffset.UtcNow);
            foreach (var scope in scopes)
            {
                scope.Dispose();
            }
        }

        private async Task RunTaskAsync(Schedule next, BackupRunner runner, CancellationToken cancellationToken)
        {
            using CancellationTokenSource merged = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _stoppingSchedules[next.Id] = merged;
            try
            {
                await runner.RunAsync(next, merged.Token);
            }
            finally
            {
                _stoppingSchedules.TryRemove(next.Id, out _);
                _logger.LogInformation("Finished backup job for schedule {ScheduleId}", next.Id);
            }
        }
    }
}
