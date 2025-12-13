// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using EasyExtensions.Abstractions;
using EasyExtensions.Quartz.Attributes;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Octockup.Server.Abstractions;
using Octockup.Server.Database;
using Octockup.Server.Helpers;
using Octockup.Server.Hubs;
using Octockup.Server.Models;
using Quartz;
using System.Collections.Concurrent;

namespace Octockup.Server.Jobs
{
    [JobTrigger(minutes: 1)]
    public class ExecuteBackupJob(
        IStreamCipher _crypto,
        AppDbContext _dbContext,
        IServiceProvider _serviceProvider,
        ILogger<ExecuteBackupJob> _logger,
        IHubContext<EventHub> _hubContext,
        IEnumerable<IBackupProvider> _providers) : IJob
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
            using CancellationTokenSource merged = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
            CancellationToken cancellationToken = merged.Token;

            Schedule? next = await ScheduleHelpers.GetNextScheduleAsync(_dbContext.Schedules, cancellationToken);
            if (next == null)
            {
                return;
            }

            _stoppingSchedules[next.Id] = merged;

            try
            {
                var runner = new BackupRunner(
                    _crypto,
                    _dbContext,
                    _serviceProvider,
                    _serviceProvider.GetRequiredService<ILogger<BackupRunner>>(),
                    _hubContext,
                    _providers);

                await runner.RunAsync(next, cancellationToken);
            }
            finally
            {
                _stoppingSchedules.TryRemove(next.Id, out _);
            }
        }
    }
}
