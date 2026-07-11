// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Octockup.Server.Collections;
using Octockup.Server.Database;
using Octockup.Server.Models.Options;

namespace Octockup.Server.Services
{
    public class UploadedChunkLookup(
        AppDbContext _dbContext,
        IOptions<BackupExecutionOptions> _options)
    {
        private const int MinimumFilterBytes = 64 * 1024;
        private const int BitsPerExpectedItem = 10;
        private const int ProgressInterval = 100_000;
        private readonly HashSet<string> _pendingHashes = new(StringComparer.Ordinal);
        private BoundedBloomFilter? _filter;
        private Guid _storageId;

        public long IndexedCount { get; private set; }
        public int FilterByteCount => _filter?.ByteCount ?? 0;

        public async Task InitializeAsync(
            Guid storageId,
            Action<long>? reportProgress,
            CancellationToken cancellationToken)
        {
            _storageId = storageId;
            _pendingHashes.Clear();
            long expectedItems = await _dbContext.UploadedHashes
                .AsNoTracking()
                .LongCountAsync(x => x.ModuleId == storageId, cancellationToken);
            int filterByteCount = CalculateFilterByteCount(
                expectedItems,
                _options.Value.MaxChunkLookupMemoryBytes);
            _filter = new BoundedBloomFilter(filterByteCount, expectedItems);
            IndexedCount = 0;

            IQueryable<string> hashes = _dbContext.UploadedHashes
                .AsNoTracking()
                .Where(x => x.ModuleId == storageId)
                .Select(x => x.Hash);
            await foreach (string hash in hashes
                .AsAsyncEnumerable()
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
            {
                _filter.Add(hash);
                IndexedCount++;
                if (IndexedCount % ProgressInterval == 0)
                {
                    reportProgress?.Invoke(IndexedCount);
                }
            }

            reportProgress?.Invoke(IndexedCount);
        }

        public async Task<bool> ContainsAsync(
            string chunkKey,
            CancellationToken cancellationToken)
        {
            BoundedBloomFilter filter = _filter ?? throw new InvalidOperationException(
                "Uploaded chunk lookup has not been initialized.");
            if (_pendingHashes.Contains(chunkKey))
            {
                return true;
            }

            if (!filter.MightContain(chunkKey))
            {
                return false;
            }

            return await _dbContext.UploadedHashes
                .AsNoTracking()
                .AnyAsync(
                    x => x.ModuleId == _storageId && x.Hash == chunkKey,
                    cancellationToken);
        }

        public bool MarkPending(string chunkKey)
        {
            BoundedBloomFilter filter = _filter ?? throw new InvalidOperationException(
                "Uploaded chunk lookup has not been initialized.");
            filter.Add(chunkKey);
            return _pendingHashes.Add(chunkKey);
        }

        public void CommitPending()
        {
            _pendingHashes.Clear();
        }

        private static int CalculateFilterByteCount(long expectedItems, int maximumBytes)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(maximumBytes, MinimumFilterBytes);
            double desiredBytes = Math.Ceiling(expectedItems * BitsPerExpectedItem / 8.0);
            return (int)Math.Clamp(desiredBytes, MinimumFilterBytes, maximumBytes);
        }
    }
}
