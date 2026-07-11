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
                .FirstOrDefaultAsync(
                    x => x.Id == snapshotId && x.Backup.UserId == userId,
                    cancellationToken);

            if (snapshot is null)
            {
                return new SnapshotDeletionResult
                {
                    ErrorMessage = "Snapshot not found: " + snapshotId
                };
            }

            bool hasRunningSchedule = await _dbContext.Schedules
                .AsNoTracking()
                .AnyAsync(
                    x => x.BackupId == snapshot.BackupId &&
                        x.Status == ScheduleStatus.Running,
                    cancellationToken);
            Guid[] runningScheduleIds = ExecuteBackupJob.GetRunningScheduleIds();
            if (!hasRunningSchedule && runningScheduleIds.Length > 0)
            {
                hasRunningSchedule = await _dbContext.Schedules
                    .AsNoTracking()
                    .AnyAsync(
                        x => x.BackupId == snapshot.BackupId &&
                            runningScheduleIds.Contains(x.Id),
                        cancellationToken);
            }

            if (hasRunningSchedule)
            {
                return new SnapshotDeletionResult
                {
                    BackupId = snapshot.BackupId,
                    ErrorMessage = "Backup has a running schedule and snapshots cannot be deleted until it stops."
                };
            }

            bool hasActiveArchive = await _dbContext.SnapshotArchiveJobs
                .AsNoTracking()
                .AnyAsync(
                    x => x.ActiveSnapshotId == snapshotId,
                    cancellationToken);
            if (hasActiveArchive)
            {
                return new SnapshotDeletionResult
                {
                    BackupId = snapshot.BackupId,
                    ErrorMessage = "Snapshot archive is active and must finish or be canceled before deletion."
                };
            }

            await using IDbContextTransaction transaction = await _dbContext.Database
                .BeginTransactionAsync(cancellationToken);

            long deletedSnapshotFileBytes = await _dbContext.SnapshotFiles
                .Where(x => x.SnapshotId == snapshotId)
                .SumAsync(x => (long?)x.Size, cancellationToken) ?? 0;

            IQueryable<Guid> archiveJobIds = _dbContext.SnapshotArchiveJobs
                .Where(x => x.SnapshotId == snapshotId)
                .Select(x => x.Id);
            await _dbContext.DownloadTickets
                .Where(x =>
                    x.Kind == DownloadTicketKind.SnapshotArchiveJob &&
                    x.ResourceId.HasValue &&
                    archiveJobIds.Contains(x.ResourceId.Value))
                .ExecuteDeleteAsync(cancellationToken);
            await _dbContext.SnapshotArchiveJobs
                .Where(x => x.SnapshotId == snapshotId)
                .ExecuteDeleteAsync(cancellationToken);

            await _dbContext.SnapshotChunkReferences
                .Where(x => x.SnapshotId == snapshotId)
                .ExecuteDeleteAsync(cancellationToken);

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
