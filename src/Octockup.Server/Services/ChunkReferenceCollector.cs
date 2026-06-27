// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore;
using Octockup.Server.Database;

namespace Octockup.Server.Services
{
    public class ChunkReferenceCollector(AppDbContext _dbContext)
    {
        private const int SnapshotFileBatchSize = 1000;

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
            CancellationToken cancellationToken)
        {
            HashSet<string> references = new(StringComparer.Ordinal);
            long referenceCount = 0;
            int skip = 0;

            while (true)
            {
                List<ICollection<string>> chunkHashBatches = await _dbContext.SnapshotFiles
                    .AsNoTracking()
                    .Where(x => _dbContext.Snapshots
                        .Where(snapshot => _dbContext.Backups
                            .Where(backup => backup.StorageId == storageId)
                            .Select(backup => backup.Id)
                            .Contains(snapshot.BackupId))
                        .Select(snapshot => snapshot.Id)
                        .Contains(x.SnapshotId))
                    .OrderBy(x => x.Id)
                    .Skip(skip)
                    .Take(SnapshotFileBatchSize)
                    .Select(x => x.ChunkHashes)
                    .ToListAsync(cancellationToken);

                if (chunkHashBatches.Count == 0)
                {
                    break;
                }

                foreach (ICollection<string> chunkHashes in chunkHashBatches)
                {
                    foreach (string chunkHash in chunkHashes)
                    {
                        referenceCount++;
                        references.Add(chunkHash);
                    }
                }

                skip += chunkHashBatches.Count;
            }

            return (references, referenceCount);
        }
    }
}
