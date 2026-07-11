// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore;
using Octockup.Server.Database;
using Octockup.Server.Services;

namespace Octockup.Server.Jobs
{
    public class SnapshotBatchWriter(
        AppDbContext dbContext,
        SnapshotChunkReferenceWriter chunkReferenceWriter)
    {
        private const int MaxInlineChunkReferences = 500;

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
            Schedule schedule,
            Guid storageId,
            SnapshotFile snapshotFile,
            CancellationToken cancellationToken)
        {
            if (snapshotFile.Snapshot is not null)
            {
                throw new ArgumentException(
                    "Snapshot navigation must remain unset while writing a backup batch.",
                    nameof(snapshotFile));
            }

            snapshot.TotalSize += snapshotFile.Size;
            snapshot.FilesCount++;
            snapshotFile.SnapshotId = snapshot.Id;
            snapshotFile.ChunkReferencesIndexed =
                snapshotFile.ChunkHashes.Count <= MaxInlineChunkReferences;
            await dbContext.SnapshotFiles.AddAsync(snapshotFile, cancellationToken);

            if (snapshotFile.ChunkReferencesIndexed)
            {
                List<SnapshotChunkReference> references = CreateReferences(
                    snapshot,
                    snapshotFile,
                    storageId,
                    0,
                    snapshotFile.ChunkHashes);
                await dbContext.SnapshotChunkReferences
                    .AddRangeAsync(references, cancellationToken);
                return;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            ResetTracking(schedule);
            int ordinal = 0;
            foreach (string[] chunkBatch in snapshotFile.ChunkHashes
                .Chunk(SnapshotChunkReferenceWriter.MaxBatchSize))
            {
                List<SnapshotChunkReference> references = CreateReferences(
                    snapshot,
                    snapshotFile,
                    storageId,
                    ordinal,
                    chunkBatch);
                await chunkReferenceWriter.FlushAsync(references, cancellationToken);
                ordinal += chunkBatch.Length;
            }

            int updatedFiles = await dbContext.SnapshotFiles
                .Where(x => x.Id == snapshotFile.Id)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(x => x.ChunkReferencesIndexed, true),
                    cancellationToken);
            if (updatedFiles != 1)
            {
                throw new InvalidOperationException(
                    $"Expected to mark snapshot file {snapshotFile.Id} indexed, but updated {updatedFiles} rows.");
            }
        }

        private static List<SnapshotChunkReference> CreateReferences(
            Snapshot snapshot,
            SnapshotFile snapshotFile,
            Guid storageId,
            int startingOrdinal,
            IEnumerable<string> chunkHashes)
        {
            return chunkHashes
                .Select((chunkHash, index) => new SnapshotChunkReference
                {
                    StorageId = storageId,
                    SnapshotId = snapshot.Id,
                    SnapshotFileId = snapshotFile.Id,
                    Ordinal = startingOrdinal + index,
                    ChunkHash = chunkHash
                })
                .ToList();
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
