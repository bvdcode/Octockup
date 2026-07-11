// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore;
using Octockup.Server.Database;

namespace Octockup.Server.Jobs
{
    public class SnapshotBatchWriter(AppDbContext dbContext)
    {
        public async Task<Snapshot> CreateAsync(
            Guid backupId,
            Schedule schedule,
            CancellationToken cancellationToken)
        {
            Snapshot snapshot = new()
            {
                BackupId = backupId
            };

            await dbContext.Snapshots.AddAsync(snapshot, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            ResetTracking(schedule);
            return snapshot;
        }

        public async ValueTask AddFileAsync(
            Snapshot snapshot,
            SnapshotFile snapshotFile,
            CancellationToken cancellationToken)
        {
            if (snapshotFile.Snapshot is not null)
            {
                throw new ArgumentException(
                    "Snapshot navigation must remain unset while writing a backup batch.",
                    nameof(snapshotFile));
            }

            snapshotFile.SnapshotId = snapshot.Id;
            await dbContext.SnapshotFiles.AddAsync(snapshotFile, cancellationToken);
            snapshot.TotalSize += snapshotFile.Size;
            snapshot.FilesCount++;
        }

        public async Task FlushAsync(
            Snapshot snapshot,
            Schedule schedule,
            CancellationToken cancellationToken)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await UpdateProgressAsync(snapshot, cancellationToken);
            ResetTracking(schedule);
        }

        public async Task CompleteAsync(
            Snapshot snapshot,
            Schedule schedule,
            CancellationToken cancellationToken)
        {
            await dbContext.SaveChangesAsync(cancellationToken);

            var stats = await dbContext.SnapshotFiles
                .AsNoTracking()
                .Where(x => x.SnapshotId == snapshot.Id)
                .GroupBy(x => x.SnapshotId)
                .Select(x => new
                {
                    FilesCount = x.Count(),
                    TotalSize = x.Sum(file => (long?)file.Size) ?? 0L
                })
                .SingleOrDefaultAsync(cancellationToken);

            DateTime completedAt = DateTime.UtcNow;
            int filesCount = stats?.FilesCount ?? 0;
            long totalSize = stats?.TotalSize ?? 0L;
            int updatedSnapshots = await dbContext.Snapshots
                .Where(x => x.Id == snapshot.Id)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(x => x.CompletedAt, completedAt)
                        .SetProperty(x => x.FilesCount, filesCount)
                        .SetProperty(x => x.TotalSize, totalSize)
                        .SetProperty(x => x.UpdatedAt, completedAt),
                    cancellationToken);

            EnsureSnapshotUpdated(snapshot.Id, updatedSnapshots);
            snapshot.CompletedAt = completedAt;
            snapshot.FilesCount = filesCount;
            snapshot.TotalSize = totalSize;
            ResetTracking(schedule);
        }

        private async Task UpdateProgressAsync(
            Snapshot snapshot,
            CancellationToken cancellationToken)
        {
            DateTime updatedAt = DateTime.UtcNow;
            int updatedSnapshots = await dbContext.Snapshots
                .Where(x => x.Id == snapshot.Id)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(x => x.FilesCount, snapshot.FilesCount)
                        .SetProperty(x => x.TotalSize, snapshot.TotalSize)
                        .SetProperty(x => x.UpdatedAt, updatedAt),
                    cancellationToken);

            EnsureSnapshotUpdated(snapshot.Id, updatedSnapshots);
        }

        private void ResetTracking(Schedule schedule)
        {
            dbContext.ChangeTracker.Clear();
            dbContext.Entry(schedule).State = EntityState.Unchanged;
        }

        private static void EnsureSnapshotUpdated(Guid snapshotId, int updatedSnapshots)
        {
            if (updatedSnapshots != 1)
            {
                throw new InvalidOperationException(
                    $"Expected to update snapshot {snapshotId}, but updated {updatedSnapshots} rows.");
            }
        }
    }
}
