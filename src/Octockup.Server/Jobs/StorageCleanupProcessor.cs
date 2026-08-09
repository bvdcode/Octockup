// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Octockup.Server.Abstractions;
using Octockup.Server.Database;
using Octockup.Server.Helpers;
using Octockup.Server.Models.Enums;

namespace Octockup.Server.Jobs
{
    public class StorageCleanupProcessor(
        IStreamCipher crypto,
        AppDbContext dbContext,
        IEnumerable<IBackupProvider> providers,
        ILogger<StorageCleanupProcessor> logger)
    {
        internal const int ScanBatchSize = 10_000;
        internal const int DeleteBatchSize = 250;
        private const int MaximumQueuedChunks = ScanBatchSize * 2;
        private static readonly TimeSpan DeleteDelay = TimeSpan.FromMilliseconds(50);

        public async Task ProcessAsync(
            StorageCleanup cleanup,
            StorageCleanupRun run,
            CancellationToken cancellationToken)
        {
            IBackupStorage storage = CreateStorage(cleanup.Module);
            await ProcessQueuedChunksAsync(cleanup, run, storage, cancellationToken);

            int queuedChunks = await dbContext.StorageCleanupChunks
                .CountAsync(x => x.ModuleId == cleanup.ModuleId, cancellationToken);
            if (queuedChunks >= MaximumQueuedChunks)
            {
                return;
            }

            if (cleanup.ScanUpperBoundHash is null)
            {
                cleanup.ScanUpperBoundHash = await dbContext.UploadedHashes
                    .Where(x => x.ModuleId == cleanup.ModuleId)
                    .MaxAsync(x => (string?)x.Hash, cancellationToken);
                cleanup.CursorHash = null;
                await dbContext.SaveChangesAsync(cancellationToken);

                if (cleanup.ScanUpperBoundHash is null)
                {
                    await CompleteAsync(cleanup, run, cancellationToken);
                    return;
                }
            }

            int scanLimit = Math.Min(ScanBatchSize, MaximumQueuedChunks - queuedChunks);
            List<UploadedHash> candidates = await LoadCandidatesAsync(
                cleanup,
                scanLimit,
                cancellationToken);
            if (candidates.Count == 0)
            {
                if (queuedChunks == 0)
                {
                    await CompleteAsync(cleanup, run, cancellationToken);
                }
                return;
            }

            await QueueOrphansAsync(cleanup, run, candidates, cancellationToken);
        }

        private IBackupStorage CreateStorage(Module module)
        {
            IBackupProvider? provider = providers.FirstOrDefault(x => x.Id == module.BackupModuleId);
            if (provider is null)
            {
                throw new StorageCleanupConfigurationException(
                    $"Storage provider not found: {module.BackupModuleId}");
            }

            provider.SetParameters(module.Params(crypto).Snapshot());
            if (provider is not IBackupStorage storage)
            {
                throw new StorageCleanupConfigurationException(
                    $"Provider is not a backup storage: {module.BackupModuleId}");
            }

            return storage;
        }

        private async Task<List<UploadedHash>> LoadCandidatesAsync(
            StorageCleanup cleanup,
            int scanLimit,
            CancellationToken cancellationToken)
        {
            string upperBoundHash = cleanup.ScanUpperBoundHash!;
            string? cursorHash = cleanup.CursorHash;
            var query = dbContext.UploadedHashes
                .AsNoTracking()
                .Where(x =>
                    x.ModuleId == cleanup.ModuleId &&
                    string.Compare(x.Hash, upperBoundHash) <= 0);

            if (cursorHash is not null)
            {
                query = query.Where(x => string.Compare(x.Hash, cursorHash) > 0);
            }

            return await query
                .OrderBy(x => x.Hash)
                .Take(scanLimit)
                .ToListAsync(cancellationToken);
        }

        private async Task QueueOrphansAsync(
            StorageCleanup cleanup,
            StorageCleanupRun run,
            IReadOnlyList<UploadedHash> candidates,
            CancellationToken cancellationToken)
        {
            string[] candidateHashes = candidates.Select(x => x.Hash).ToArray();
            HashSet<string> referencedHashes = await LoadReferencedHashesAsync(
                cleanup.ModuleId,
                candidateHashes,
                cancellationToken);
            List<UploadedHash> orphans = candidates
                .Where(x => !referencedHashes.Contains(x.Hash))
                .ToList();

            await using IDbContextTransaction transaction = await dbContext.Database
                .BeginTransactionAsync(cancellationToken);

            if (orphans.Count > 0)
            {
                List<StorageCleanupChunk> queuedChunks = orphans
                    .Select(x => new StorageCleanupChunk
                    {
                        ModuleId = x.ModuleId,
                        Hash = x.Hash,
                        StoredSize = x.StoredSize,
                        OriginalSize = x.OriginalSize,
                        CompressionAlgorithm = x.CompressionAlgorithm,
                    })
                    .ToList();
                Guid[] orphanIds = orphans.Select(x => x.Id).ToArray();

                await dbContext.StorageCleanupChunks.AddRangeAsync(queuedChunks, cancellationToken);
                await dbContext.UploadedHashes
                    .Where(x => orphanIds.Contains(x.Id))
                    .ExecuteDeleteAsync(cancellationToken);
            }

            cleanup.CursorHash = candidates[^1].Hash;
            cleanup.ScannedChunks += candidates.Count;
            run.ScannedChunks += candidates.Count;
            run.ErrorMessage = null;
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(
                "Storage cleanup {CleanupId} scanned {ScannedCount} chunks and queued {OrphanCount} orphans.",
                cleanup.Id,
                candidates.Count,
                orphans.Count);
        }

        private async Task ProcessQueuedChunksAsync(
            StorageCleanup cleanup,
            StorageCleanupRun run,
            IBackupStorage storage,
            CancellationToken cancellationToken)
        {
            List<StorageCleanupChunk> queuedChunks = await dbContext.StorageCleanupChunks
                .AsNoTracking()
                .Where(x => x.ModuleId == cleanup.ModuleId)
                .OrderBy(x => x.UpdatedAt)
                .ThenBy(x => x.Hash)
                .Take(DeleteBatchSize)
                .ToListAsync(cancellationToken);
            if (queuedChunks.Count == 0)
            {
                return;
            }

            string[] queuedHashes = queuedChunks.Select(x => x.Hash).ToArray();
            HashSet<string> referencedHashes = await LoadReferencedHashesAsync(
                cleanup.ModuleId,
                queuedHashes,
                cancellationToken);
            await RestoreReferencedChunksAsync(queuedChunks, referencedHashes, cancellationToken);

            List<Guid> deletedIds = [];
            long reclaimedBytes = 0;
            bool deletionFailed = false;
            foreach (StorageCleanupChunk chunk in queuedChunks)
            {
                if (referencedHashes.Contains(chunk.Hash))
                {
                    continue;
                }

                string path = ChunkStorageHelpers.GetStoragePath(chunk.Hash, storage.PathSeparator);
                bool? deleted = await storage.DeleteAsync(path, cancellationToken);
                if (deleted is not true)
                {
                    bool? exists = await storage.ExistsAsync(path, cancellationToken);
                    if (exists is not false)
                    {
                        deletionFailed = true;
                        cleanup.ErrorMessage = $"Failed to delete chunk {chunk.Hash}.";
                        await dbContext.StorageCleanupChunks
                            .Where(x => x.Id == chunk.Id)
                            .ExecuteUpdateAsync(
                                setters => setters.SetProperty(x => x.UpdatedAt, DateTime.UtcNow),
                                cancellationToken);
                        continue;
                    }
                }

                deletedIds.Add(chunk.Id);
                reclaimedBytes += chunk.StoredSize;
                await Task.Delay(DeleteDelay, cancellationToken);
            }

            if (!deletionFailed)
            {
                cleanup.ErrorMessage = null;
                run.ErrorMessage = null;
            }
            else
            {
                run.ErrorMessage = cleanup.ErrorMessage;
            }

            if (deletedIds.Count == 0)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            await using IDbContextTransaction transaction = await dbContext.Database
                .BeginTransactionAsync(cancellationToken);
            await dbContext.StorageCleanupChunks
                .Where(x => deletedIds.Contains(x.Id))
                .ExecuteDeleteAsync(cancellationToken);
            cleanup.TotalDeletedChunks += deletedIds.Count;
            cleanup.TotalReclaimedBytes += reclaimedBytes;
            run.DeletedChunks += deletedIds.Count;
            run.ReclaimedBytes += reclaimedBytes;
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(
                "Storage cleanup {CleanupId} deleted {DeletedCount} chunks and reclaimed {ReclaimedBytes} bytes.",
                cleanup.Id,
                deletedIds.Count,
                reclaimedBytes);
        }

        private async Task RestoreReferencedChunksAsync(
            IReadOnlyList<StorageCleanupChunk> queuedChunks,
            IReadOnlySet<string> referencedHashes,
            CancellationToken cancellationToken)
        {
            if (referencedHashes.Count == 0)
            {
                return;
            }

            string[] referenced = referencedHashes.ToArray();
            HashSet<string> registeredHashes = await dbContext.UploadedHashes
                .AsNoTracking()
                .Where(x => x.ModuleId == queuedChunks[0].ModuleId && referenced.Contains(x.Hash))
                .Select(x => x.Hash)
                .ToHashSetAsync(cancellationToken);
            List<StorageCleanupChunk> chunksToRestore = queuedChunks
                .Where(x => referencedHashes.Contains(x.Hash) && !registeredHashes.Contains(x.Hash))
                .ToList();
            Guid[] queueIds = queuedChunks
                .Where(x => referencedHashes.Contains(x.Hash))
                .Select(x => x.Id)
                .ToArray();

            await using IDbContextTransaction transaction = await dbContext.Database
                .BeginTransactionAsync(cancellationToken);
            if (chunksToRestore.Count > 0)
            {
                List<UploadedHash> restored = chunksToRestore
                    .Select(x => new UploadedHash
                    {
                        ModuleId = x.ModuleId,
                        Hash = x.Hash,
                        StoredSize = x.StoredSize,
                        OriginalSize = x.OriginalSize,
                        CompressionAlgorithm = x.CompressionAlgorithm,
                    })
                    .ToList();
                await dbContext.UploadedHashes.AddRangeAsync(restored, cancellationToken);
            }

            await dbContext.StorageCleanupChunks
                .Where(x => queueIds.Contains(x.Id))
                .ExecuteDeleteAsync(cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }

        private async Task<HashSet<string>> LoadReferencedHashesAsync(
            Guid storageId,
            string[] candidateHashes,
            CancellationToken cancellationToken)
        {
            return await dbContext.SnapshotFiles
                .AsNoTracking()
                .Where(x => x.Snapshot.Backup.StorageId == storageId)
                .SelectMany(x => x.ChunkHashes)
                .Where(x => candidateHashes.Contains(x))
                .Distinct()
                .ToHashSetAsync(cancellationToken);
        }

        private async Task CompleteAsync(
            StorageCleanup cleanup,
            StorageCleanupRun run,
            CancellationToken cancellationToken)
        {
            DateTime completedAt = DateTime.UtcNow;
            cleanup.Status = StorageCleanupStatus.Completed;
            cleanup.CursorHash = null;
            cleanup.ScanUpperBoundHash = null;
            cleanup.LastCompletedAt = completedAt;
            cleanup.ErrorMessage = null;
            run.Status = StorageCleanupStatus.Completed;
            run.CompletedAt = completedAt;
            run.ErrorMessage = null;
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
