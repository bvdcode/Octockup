// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using Quartz;
using Octockup.Server.Hubs;
using Octockup.Server.Database;
using Microsoft.AspNetCore.SignalR;
using Octockup.Server.Abstractions;
using Microsoft.EntityFrameworkCore;
using EasyExtensions.Quartz.Attributes;
using Octockup.Server.Models.Enums;
using Octockup.Server.Models;

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
            ScheduleReport report = new(userId, next.Id, _hubContext);

            if (_providers.FirstOrDefault(x => x.Id == next.Backup.Source.BackupModuleId) is not IBackupSource foundSourceProvider)
            {
                next.ErrorMessage = $"Source provider not found: {next.Backup.Source.BackupModuleId}";
                next.Status = BackupStatus.Failed;
                next.FinishedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
                _logger.LogWarning("{msg}", next.ErrorMessage);
                await report.SendAsync(0, next.ErrorMessage);
                return;
            }
            if (_providers.FirstOrDefault(x => x.Id == next.Backup.Storage.BackupModuleId) is not IBackupStorage foundStorageProvider)
            {
                next.ErrorMessage = $"Storage provider not found: {next.Backup.Storage.BackupModuleId}";
                next.Status = BackupStatus.Failed;
                next.FinishedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
                _logger.LogWarning("{msg}", next.ErrorMessage);
                await report.SendAsync(0, next.ErrorMessage);
                return;
            }

            await report.SendAsync(0, "Listing files to backup...");
            next.Status = BackupStatus.Running;
            await _dbContext.SaveChangesAsync();

            try
            {
                var filesToBackup = foundSourceProvider.GetFiles(recursive: true).ToList();
                report.Total = filesToBackup.Count;

                await BackupAsync(next, foundSourceProvider, foundStorageProvider, filesToBackup, report);
            }
            catch (Exception ex)
            {
                next.ErrorMessage = $"Backup failed: {ex.Message}";
                next.Status = BackupStatus.Failed;
                next.FinishedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
                _logger.LogError(ex, "Schedule {ScheduleId} backup failed", next.Id);
                await report.SendAsync(report.Processed, next.ErrorMessage, status: BackupStatus.Failed);
                return;
            }
        }

        private async Task<Schedule?> GetNextScheduleAsync()
        {
            DateTime now = DateTime.UtcNow;

            var schedules = await _dbContext.Schedules
                .AsNoTracking()
                .Include(x => x.Backup)
                .ThenInclude(b => b.Source)
                .Include(x => x.Backup)
                .ThenInclude(b => b.Storage)
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
                {
                    return s.StartAt > now ? s.StartAt : now;
                }

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
            {
                return s.StartAt;
            }

            // Calculate next interval tick
            var elapsed = now - s.StartAt;
            if (elapsed.TotalMilliseconds < 0)
            {
                elapsed = TimeSpan.Zero;
            }

            long k = elapsed.Ticks / interval.Ticks;
            DateTime next = s.StartAt.AddTicks(interval.Ticks * (k + 1));

            return next;
        }

        private async Task BackupAsync(Schedule next, IBackupSource foundSourceProvider, IBackupStorage foundStorageProvider, List<BackupFileInfo> filesToBackup, ScheduleReport report)
        {
            long processedBytes = 0;
            for (int i = 0; i < filesToBackup.Count; i++)
            {
                var file = filesToBackup[i];
                await report.SendAsync(i, $"Processing file: {file.Name}", processedBytes: processedBytes);
                await Task.Delay(Random.Shared.Next(1, 1000));
                _logger.LogInformation("Schedule {ScheduleId}: {Message} ({Processed}/{Total})", next.Id, report.Message, report.Processed, report.Total);
                processedBytes += filesToBackup[i].Size ?? 0;
            }
        }
    }
}
