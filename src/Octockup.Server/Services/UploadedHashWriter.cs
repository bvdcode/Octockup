// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore;
using Octockup.Server.Database;

namespace Octockup.Server.Services
{
    public class UploadedHashWriter(
        AppDbContext _dbContext,
        ILogger<UploadedHashWriter> _logger)
    {
        public async Task<int> FlushAsync(
            IReadOnlyCollection<UploadedHash> hashes,
            CancellationToken cancellationToken)
        {
            if (hashes.Count == 0)
            {
                return 0;
            }

            Guid moduleId = hashes.First().ModuleId;
            if (hashes.Any(x => x.ModuleId != moduleId))
            {
                throw new ArgumentException(
                    "An uploaded hash batch must belong to one storage module.",
                    nameof(hashes));
            }

            List<UploadedHash> candidates = hashes
                .GroupBy(x => x.Hash, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToList();
            HashSet<string> existingHashes = await LoadExistingHashesAsync(
                moduleId,
                candidates,
                cancellationToken);
            List<UploadedHash> missingHashes = candidates
                .Where(x => !existingHashes.Contains(x.Hash))
                .ToList();

            try
            {
                await SaveAndDetachAsync(missingHashes, cancellationToken);
                return missingHashes.Count;
            }
            catch (DbUpdateException ex)
            {
                HashSet<string> persistedAfterConflict = await LoadExistingHashesAsync(
                    moduleId,
                    missingHashes,
                    cancellationToken);
                if (persistedAfterConflict.Count == 0)
                {
                    _logger.LogError(
                        ex,
                        "Failed to persist uploaded chunk metadata for storage {StorageId}.",
                        moduleId);
                    throw;
                }

                List<UploadedHash> retryHashes = missingHashes
                    .Where(x => !persistedAfterConflict.Contains(x.Hash))
                    .ToList();
                _logger.LogInformation(
                    ex,
                    "Resolved {DuplicateCount} concurrently registered chunks for storage {StorageId}.",
                    missingHashes.Count - retryHashes.Count,
                    moduleId);
                await SaveAndDetachAsync(retryHashes, cancellationToken);
                return retryHashes.Count;
            }
        }

        private async Task SaveAndDetachAsync(
            IReadOnlyCollection<UploadedHash> hashes,
            CancellationToken cancellationToken)
        {
            if (hashes.Count > 0)
            {
                await _dbContext.UploadedHashes.AddRangeAsync(hashes, cancellationToken);
            }

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            finally
            {
                foreach (UploadedHash hash in hashes)
                {
                    _dbContext.Entry(hash).State = EntityState.Detached;
                }
            }
        }

        private async Task<HashSet<string>> LoadExistingHashesAsync(
            Guid moduleId,
            IReadOnlyCollection<UploadedHash> hashes,
            CancellationToken cancellationToken)
        {
            if (hashes.Count == 0)
            {
                return new HashSet<string>(StringComparer.Ordinal);
            }

            string[] keys = hashes.Select(x => x.Hash).ToArray();
            List<string> existingHashes = await _dbContext.UploadedHashes
                .AsNoTracking()
                .Where(x => x.ModuleId == moduleId && keys.Contains(x.Hash))
                .Select(x => x.Hash)
                .ToListAsync(cancellationToken);
            return existingHashes.ToHashSet(StringComparer.Ordinal);
        }
    }
}
