// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore;
using Octockup.Server.Database;
using Octockup.Server.Helpers;
using Octockup.Server.Models;

namespace Octockup.Server.Services
{
    public class SnapshotChunkDescriptorReader(
        AppDbContext _dbContext,
        Guid _storageId,
        Guid _snapshotFileId,
        ILogger _logger)
    {
        public const int BatchSize = 500;

        private readonly Queue<ChunkStorageDescriptor> _buffer = new(BatchSize);
        private int _lastOrdinal = -1;
        private bool _completed;

        public async ValueTask<ChunkStorageDescriptor?> ReadNextAsync(
            CancellationToken cancellationToken)
        {
            if (_buffer.TryDequeue(out ChunkStorageDescriptor descriptor))
            {
                return descriptor;
            }

            if (_completed)
            {
                return null;
            }

            List<SnapshotChunkReference> references = await _dbContext
                .SnapshotChunkReferences
                .AsNoTracking()
                .Where(x =>
                    x.SnapshotFileId == _snapshotFileId &&
                    x.Ordinal > _lastOrdinal)
                .OrderBy(x => x.Ordinal)
                .Take(BatchSize)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            if (references.Count == 0)
            {
                _completed = true;
                return null;
            }

            string[] chunkKeys = references
                .Select(x => x.ChunkHash)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            List<UploadedHash> metadata = await _dbContext.UploadedHashes
                .AsNoTracking()
                .Where(x => x.ModuleId == _storageId && chunkKeys.Contains(x.Hash))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            Dictionary<string, UploadedHash> metadataByHash = metadata
                .ToDictionary(x => x.Hash, StringComparer.Ordinal);

            foreach (SnapshotChunkReference reference in references)
            {
                ChunkStorageDescriptor chunk;
                if (metadataByHash.TryGetValue(reference.ChunkHash, out UploadedHash? found))
                {
                    chunk = ChunkStorageHelpers.Parse(
                        found.Hash,
                        found.CompressionAlgorithm,
                        found.OriginalSize);
                }
                else
                {
                    _logger.LogWarning(
                        "Chunk hash metadata not found in DB: {ChunkKey}",
                        reference.ChunkHash);
                    chunk = ChunkStorageHelpers.Parse(reference.ChunkHash);
                }

                _buffer.Enqueue(chunk);
            }

            _lastOrdinal = references[^1].Ordinal;
            return _buffer.Dequeue();
        }
    }
}
