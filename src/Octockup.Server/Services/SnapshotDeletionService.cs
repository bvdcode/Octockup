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
    public class SnapshotDeletionService(AppDbContext _dbContext)
    {
        public async Task<SnapshotDeletionResult> DeleteAsync(
            Guid userId,
            Guid snapshotId,
            CancellationToken cancellationToken)
        {
            Snapshot? snapshot = await _dbContext.Snapshots
                .AsNoTracking()
                .Include(x => x.Backup)
                    .ThenInclude(x => x.Source)
                .FirstOrDefaultAsync(
                    x => x.Id == snapshotId && x.Backup.Source.UserId == userId,
                    cancellationToken);

            if (snapshot is null)
            {
                return new SnapshotDeletionResult
                {
                    ErrorMessage = "Snapshot not found: " + snapshotId
                };
            }

            List<Schedule> schedules = await _dbContext.Schedules
                .Where(x => x.BackupId == snapshot.BackupId)
                .ToListAsync(cancellationToken);

            bool hasRunningSchedule = schedules.Any(x =>
                x.Status == ScheduleStatus.Running ||
                ExecuteBackupJob.IsScheduleRunning(x.Id));

            if (hasRunningSchedule)
            {
                return new SnapshotDeletionResult
                {
                    BackupId = snapshot.BackupId,
                    ErrorMessage = "Backup has a running schedule and snapshots cannot be deleted until it stops."
                };
            }

            await using IDbContextTransaction transaction = await _dbContext.Database
                .BeginTransactionAsync(cancellationToken);

            long deletedSnapshotFileBytes = await _dbContext.SnapshotFiles
                .Where(x => x.SnapshotId == snapshotId)
                .SumAsync(x => (long?)x.Size, cancellationToken) ?? 0;

            int deletedSnapshotFiles = await _dbContext.SnapshotFiles
                .Where(x => x.SnapshotId == snapshotId)
                .ExecuteDeleteAsync(cancellationToken);

            int deletedSnapshots = await _dbContext.Snapshots
                .Where(x => x.Id == snapshotId)
                .ExecuteDeleteAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            return new SnapshotDeletionResult
            {
                Deleted = deletedSnapshots > 0,
                BackupId = snapshot.BackupId,
                DeletedSnapshotFiles = deletedSnapshotFiles,
                DeletedSnapshotFileBytes = deletedSnapshotFileBytes
            };
        }
    }
}
