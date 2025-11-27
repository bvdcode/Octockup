// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using Quartz;
using Octockup.Server.Hubs;
using Octockup.Server.Database;
using Microsoft.AspNetCore.SignalR;
using Octockup.Server.Abstractions;
using Microsoft.EntityFrameworkCore;
using EasyExtensions.Quartz.Attributes;

namespace Octockup.Server.Jobs
{
    [JobTrigger(minutes: 1)]
    public class ExecuteBackupJob(
        AppDbContext _dbContext,
        ILogger<ExecuteBackupJob> _logger,
        IHubContext<EventHub> _hubContext,
        IEnumerable<IBackupProvider> _providers) : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            Schedule? next = await GetNextScheduleAsync();
            if (next == null)
            {
                return;
            }
            Guid userId = next.Backup.Source.UserId;
            ScheduleReport report = new(userId, next.Id, 10000, _hubContext);
            for (int i = 0; i < 10000; i++)
            {
                int randomSleepTime = Random.Shared.Next(1, 1000);
                await report.SendAsync(i + 1, $"Processing file: {i}.txt...");
                await Task.Delay(randomSleepTime);
                await _hubContext.Clients.User(userId.ToString()).SendAsync("ScheduleReport", report);
                _logger.LogInformation("Schedule {ScheduleId}: {Message} ({Processed}/{Total})", next.Id, report.Message, report.Processed, report.Total);
            }

        }

        private async Task<Schedule?> GetNextScheduleAsync()
        {
            DateTime now = DateTime.UtcNow;

            var schedules = await _dbContext.Schedules
                .AsNoTracking()
                .Include(x => x.Backup)
                .ThenInclude(b => b.Source)
                .ToListAsync();

            Schedule? best = null;
            DateTime? bestTime = null;

            foreach (var sch in schedules)
            {
                DateTime? nextRun = CalculateNextRun(sch, now);
                if (nextRun == null)
                    continue;

                if (bestTime == null || nextRun < bestTime)
                {
                    best = sch;
                    bestTime = nextRun;
                }
            }

            return best;
        }

        private static DateTime? CalculateNextRun(Schedule s, DateTime now)
        {
            // One-time job (Interval = null)
            if (s.Interval is null)
            {
                // Not started yet → next start
                if (s.FinishedAt is null)
                    return s.StartAt > now ? s.StartAt : now;

                // already executed → no more runs
                return null;
            }

            // Periodic job
            TimeSpan interval = s.Interval.Value;

            // First run never happened → scheduled at StartAt
            if (s.FinishedAt is null)
            {
                return s.StartAt > now ? s.StartAt : now;
            }

            // If StartAt is in the future
            if (s.StartAt > now)
                return s.StartAt;

            // Calculate next interval tick
            var elapsed = now - s.StartAt;
            if (elapsed.TotalMilliseconds < 0)
                elapsed = TimeSpan.Zero;

            long k = (long)(elapsed.Ticks / interval.Ticks);
            DateTime next = s.StartAt.AddTicks(interval.Ticks * (k + 1));

            return next;
        }
    }
}
