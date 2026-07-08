// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore;
using Octockup.Server.Database;

namespace Octockup.Server.Services
{
    public class ChunkReferenceCollector(AppDbContext _dbContext)
    {
        private const int SnapshotFileProgressInterval = 1000;

        public async Task<HashSet<string>> CollectForStorageAsync(
            Guid storageId,
            CancellationToken cancellationToken)
        {
            (HashSet<string> references, _) = await CollectWithReferenceCountForStorageAsync(
                storageId,
                cancellationToken);

            return references;
        }

        public async Task<(HashSet<string> References, long ReferenceCount)> CollectWithReferenceCountForStorageAsync(
            Guid storageId,
            CancellationToken cancellationToken,
            Func<long, long, long, CancellationToken, Task>? reportProgressAsync = null)
        {
            HashSet<string> references = new(StringComparer.Ordinal);
            long referenceCount = 0;
            long snapshotFilesScanned = 0;

            IQueryable<ICollection<string>> chunkHashQuery =
                from snapshotFile in _dbContext.SnapshotFiles.AsNoTracking()
                join snapshot in _dbContext.Snapshots.AsNoTracking()
                    on snapshotFile.SnapshotId equals snapshot.Id
                join backup in _dbContext.Backups.AsNoTracking()
                    on snapshot.BackupId equals backup.Id
                where backup.StorageId == storageId
                select snapshotFile.ChunkHashes;

            await foreach (ICollection<string> chunkHashes in chunkHashQuery
                .AsAsyncEnumerable()
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
            {
                snapshotFilesScanned++;
                foreach (string chunkHash in chunkHashes)
                {
                    referenceCount++;
                    references.Add(chunkHash);
                }

                if (reportProgressAsync is not null &&
                    snapshotFilesScanned % SnapshotFileProgressInterval == 0)
                {
                    await reportProgressAsync(
                        snapshotFilesScanned,
                        referenceCount,
                        references.Count,
                        cancellationToken).ConfigureAwait(false);
                }
            }

            if (reportProgressAsync is not null &&
                snapshotFilesScanned % SnapshotFileProgressInterval != 0)
            {
                await reportProgressAsync(
                    snapshotFilesScanned,
                    referenceCount,
                    references.Count,
                    cancellationToken).ConfigureAwait(false);
            }

            return (references, referenceCount);
        }
    }
}
