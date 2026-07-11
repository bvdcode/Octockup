// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore;
using Octockup.Server.Database;

namespace Octockup.Server.Services
{
    public class SnapshotChunkReferenceWriter(
        AppDbContext _dbContext,
        ILogger<SnapshotChunkReferenceWriter> _logger)
    {
        public const int MaxBatchSize = 500;

        public async Task<int> FlushAsync(
            IReadOnlyCollection<SnapshotChunkReference> references,
            CancellationToken cancellationToken)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(references.Count, MaxBatchSize);
            List<SnapshotChunkReference> candidates = references
                .GroupBy(x => (x.SnapshotFileId, x.Ordinal))
                .Select(group => group.First())
                .ToList();

            try
            {
                await SaveAndDetachAsync(candidates, cancellationToken);
                return candidates.Count;
            }
            catch (DbUpdateException ex)
            {
                HashSet<(Guid SnapshotFileId, int Ordinal)> persisted =
                    await LoadExistingKeysAsync(candidates, cancellationToken);
                if (persisted.Count == 0)
                {
                    _logger.LogError(ex, "Failed to persist normalized snapshot chunk references.");
                    throw;
                }

                List<SnapshotChunkReference> retry = candidates
                    .Where(x => !persisted.Contains((x.SnapshotFileId, x.Ordinal)))
                    .ToList();
                _logger.LogInformation(
                    ex,
                    "Recovered {ReferenceCount} already indexed snapshot chunk references.",
                    candidates.Count - retry.Count);
                await SaveAndDetachAsync(retry, cancellationToken);
                return retry.Count;
            }
        }

        private async Task SaveAndDetachAsync(
            IReadOnlyCollection<SnapshotChunkReference> references,
            CancellationToken cancellationToken)
        {
            if (references.Count > 0)
            {
                await _dbContext.SnapshotChunkReferences
                    .AddRangeAsync(references, cancellationToken);
            }

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            finally
            {
                foreach (SnapshotChunkReference reference in references)
                {
                    _dbContext.Entry(reference).State = EntityState.Detached;
                }
            }
        }

        private async Task<HashSet<(Guid SnapshotFileId, int Ordinal)>> LoadExistingKeysAsync(
            IReadOnlyCollection<SnapshotChunkReference> references,
            CancellationToken cancellationToken)
        {
            HashSet<(Guid SnapshotFileId, int Ordinal)> existing = [];
            foreach (IGrouping<Guid, SnapshotChunkReference> fileReferences in references
                .GroupBy(x => x.SnapshotFileId))
            {
                int[] ordinals = fileReferences.Select(x => x.Ordinal).ToArray();
                List<int> found = await _dbContext.SnapshotChunkReferences
                    .AsNoTracking()
                    .Where(x =>
                        x.SnapshotFileId == fileReferences.Key &&
                        ordinals.Contains(x.Ordinal))
                    .Select(x => x.Ordinal)
                    .ToListAsync(cancellationToken);
                foreach (int ordinal in found)
                {
                    existing.Add((fileReferences.Key, ordinal));
                }
            }

            return existing;
        }
    }
}
