// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Octockup.Server.Database;
using Octockup.Server.Jobs;
using Octockup.Server.Models.Enums;
using Octockup.Server.Models.Results;

namespace Octockup.Server.Services
{
    public class BackupDeletionService(AppDbContext _dbContext)
    {
        public async Task<BackupDeletionResult> DeleteAsync(
            Guid userId,
            Guid backupId,
            CancellationToken cancellationToken)
        {
            Backup? backup = await _dbContext.Backups
                .AsNoTracking()
                .Include(x => x.Source)
                .FirstOrDefaultAsync(
                    x => x.Id == backupId && x.Source.UserId == userId,
                    cancellationToken);

            if (backup is null)
            {
                return new BackupDeletionResult
                {
                    ErrorMessage = "Backup not found: " + backupId
                };
            }

            List<Schedule> schedules = await _dbContext.Schedules
                .Where(x => x.BackupId == backupId)
                .ToListAsync(cancellationToken);

            bool hasRunningSchedule = schedules.Any(x =>
                x.Status == ScheduleStatus.Running ||
                ExecuteBackupJob.IsScheduleRunning(x.Id));

            if (hasRunningSchedule)
            {
                return new BackupDeletionResult
                {
                    ErrorMessage = "Backup has a running schedule and cannot be deleted until it stops."
                };
            }

            await using IDbContextTransaction transaction = await _dbContext.Database
                .BeginTransactionAsync(cancellationToken);

            IQueryable<Guid> snapshotIds = _dbContext.Snapshots
                .Where(x => x.BackupId == backupId)
                .Select(x => x.Id);

            int deletedSnapshotFiles = await _dbContext.SnapshotFiles
                .Where(x => snapshotIds.Contains(x.SnapshotId))
                .ExecuteDeleteAsync(cancellationToken);

            int deletedSnapshots = await _dbContext.Snapshots
                .Where(x => x.BackupId == backupId)
                .ExecuteDeleteAsync(cancellationToken);

            int deletedSchedules = await _dbContext.Schedules
                .Where(x => x.BackupId == backupId)
                .ExecuteDeleteAsync(cancellationToken);

            await _dbContext.Backups
                .Where(x => x.Id == backupId)
                .ExecuteDeleteAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return new BackupDeletionResult
            {
                Deleted = true,
                DeletedSchedules = deletedSchedules,
                DeletedSnapshots = deletedSnapshots,
                DeletedSnapshotFiles = deletedSnapshotFiles
            };
        }
    }
}
