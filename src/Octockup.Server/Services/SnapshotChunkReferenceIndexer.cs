// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore;
using Octockup.Server.Database;

namespace Octockup.Server.Services
{
    public class SnapshotChunkReferenceIndexer(
        AppDbContext _dbContext,
        SnapshotChunkReferenceWriter _writer)
    {
        private const int FileBatchSize = 50;

        public async Task IndexStorageAsync(
            Guid storageId,
            Func<long, long, CancellationToken, Task>? reportProgressAsync,
            CancellationToken cancellationToken)
        {
            long filesIndexed = await _dbContext.SnapshotFiles
                .AsNoTracking()
                .LongCountAsync(
                    x => x.ChunkReferencesIndexed &&
                        x.Snapshot.CompletedAt != null &&
                        x.Snapshot.Backup.StorageId == storageId,
                    cancellationToken);
            long referencesProcessed = await _dbContext.SnapshotChunkReferences
                .AsNoTracking()
                .LongCountAsync(
                    x => x.StorageId == storageId && x.Snapshot.CompletedAt != null,
                    cancellationToken);
            if (reportProgressAsync is not null)
            {
                await reportProgressAsync(
                    filesIndexed,
                    referencesProcessed,
                    cancellationToken);
            }

            while (true)
            {
                List<SnapshotFile> files = await _dbContext.SnapshotFiles
                    .Where(x =>
                        !x.ChunkReferencesIndexed &&
                        x.Snapshot.CompletedAt != null &&
                        x.Snapshot.Backup.StorageId == storageId)
                    .OrderBy(x => x.Id)
                    .Take(FileBatchSize)
                    .ToListAsync(cancellationToken);
                if (files.Count == 0)
                {
                    break;
                }

                List<SnapshotChunkReference> pendingReferences =
                    new(SnapshotChunkReferenceWriter.MaxBatchSize);
                foreach (SnapshotFile file in files)
                {
                    int ordinal = 0;
                    foreach (string chunkHash in file.ChunkHashes)
                    {
                        pendingReferences.Add(new SnapshotChunkReference
                        {
                            StorageId = storageId,
                            SnapshotId = file.SnapshotId,
                            SnapshotFileId = file.Id,
                            Ordinal = ordinal,
                            ChunkHash = chunkHash
                        });
                        ordinal++;
                        if (pendingReferences.Count == SnapshotChunkReferenceWriter.MaxBatchSize)
                        {
                            referencesProcessed += await _writer
                                .FlushAsync(pendingReferences, cancellationToken);
                            pendingReferences.Clear();
                        }
                    }

                    file.ChunkReferencesIndexed = true;
                    filesIndexed++;
                }

                referencesProcessed += await _writer
                    .FlushAsync(pendingReferences, cancellationToken);
                _dbContext.ChangeTracker.Clear();
                if (reportProgressAsync is not null)
                {
                    await reportProgressAsync(
                        filesIndexed,
                        referencesProcessed,
                        cancellationToken);
                }
            }
        }
    }
}
