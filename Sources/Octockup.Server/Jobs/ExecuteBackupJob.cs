// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using Quartz;
using Octockup.Server.Hubs;
using Octockup.Server.Models;
using Octockup.Server.Helpers;
using Octockup.Server.Database;
using Octockup.Server.Models.Enums;
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
            Schedule? next = await ScheduleHelpers.GetNextScheduleAsync(_dbContext.Schedules);
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

        private async Task BackupAsync(Schedule next, IBackupSource foundSourceProvider, IBackupStorage foundStorageProvider, List<BackupFileInfo> filesToBackup, ScheduleReport report)
        {
            long processedBytes = 0;
            for (int i = 0; i < filesToBackup.Count; i++)
            {
                var file = filesToBackup[i];
                await report.SendAsync(i, $"Processing: {file.Name}", processedBytes: processedBytes);
                
                
                
                await Task.Delay(Random.Shared.Next(1, 1000));



                processedBytes += filesToBackup[i].Size ?? 0;
                _logger.LogInformation("Schedule {ScheduleId}: {Message} ({Processed}/{Total})", next.Id, report.Message, report.Processed, report.Total);
            }
        }
    }
}
