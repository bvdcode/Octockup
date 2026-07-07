// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.Abstractions;
using Microsoft.EntityFrameworkCore;
using Octockup.Server.Abstractions;
using Octockup.Server.Database;
using Octockup.Server.Helpers;
using Octockup.Server.Models;
using Octockup.Server.Models.Dto;
using Octockup.Server.Models.Enums;
using System.Diagnostics;

namespace Octockup.Server.Services
{
    public class StorageCleanupRunner(
        IStreamCipher _crypto,
        AppDbContext _dbContext,
        ILogger<StorageCleanupRunner> _logger,
        IEnumerable<IBackupProvider> _providers,
        ChunkReferenceCollector _chunkReferenceCollector)
    {
        private const int UploadedHashDeleteBatchSize = 500;
        private static readonly TimeSpan ProgressPublishInterval = TimeSpan.FromSeconds(1);

        public async Task RunAsync(
            StorageCleanupJobState state,
            Func<StorageCleanupJobDto, CancellationToken, Task> publishAsync,
            CancellationToken cancellationToken)
        {
            state.Update(x => x.Status = StorageCleanupStatus.Running);
            await publishAsync(state.Snapshot(), cancellationToken).ConfigureAwait(false);

            Module storageModule = await GetStorageModuleAsync(state, cancellationToken).ConfigureAwait(false);
            (IBackupStorage storage, IBackupStorageInventory inventory) = CreateStorageInventory(storageModule);
            Stopwatch publishStopwatch = Stopwatch.StartNew();

            (HashSet<string> referencedChunks, long referenceCount) = await _chunkReferenceCollector
                .CollectWithReferenceCountForStorageAsync(
                    storageModule.Id,
                    cancellationToken,
                    async (snapshotFilesScanned, currentReferenceCount, referencedChunkCount, ct) =>
                    {
                        state.Update(x =>
                        {
                            x.Phase = StorageCleanupPhase.CollectingReferences;
                            x.SnapshotFilesScanned = snapshotFilesScanned;
                            x.ReferenceCount = currentReferenceCount;
                            x.ReferencedChunks = referencedChunkCount;
                        });

                        await PublishIfDueAsync(state, publishAsync, publishStopwatch, ct)
                            .ConfigureAwait(false);
                    })
                .ConfigureAwait(false);

            state.Update(x =>
            {
                x.Phase = StorageCleanupPhase.ScanningStorage;
                x.ReferenceCount = referenceCount;
                x.ReferencedChunks = referencedChunks.Count;
            });
            await publishAsync(state.Snapshot(), cancellationToken).ConfigureAwait(false);

            List<string> uploadedHashDeleteBatch = [];
            publishStopwatch.Restart();

            await foreach (BackupFileInfo storageObject in inventory.GetFilesAsync(true, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                long objectSize = storageObject.Size ?? 0;
                state.Update(x =>
                {
                    x.StorageObjectsScanned++;
                    x.StorageBytesScanned += objectSize;
                    x.CurrentPath = storageObject.Path;
                });

                if (!TryCreateStorageChunk(storageObject, storage.PathSeparator, out StorageChunkObject chunkObject))
                {
                    state.Update(x => x.SkippedObjects++);
                    await PublishIfDueAsync(state, publishAsync, publishStopwatch, cancellationToken)
                        .ConfigureAwait(false);
                    continue;
                }

                state.Update(x => x.ChunkObjectsScanned++);

                if (referencedChunks.Contains(chunkObject.ChunkKey))
                {
                    state.Update(x =>
                    {
                        x.ReferencedObjects++;
                        x.ReferencedBytes += chunkObject.Size;
                    });
                    await PublishIfDueAsync(state, publishAsync, publishStopwatch, cancellationToken)
                        .ConfigureAwait(false);
                    continue;
                }

                await DeleteOrphanChunkAsync(
                    state,
                    storage,
                    chunkObject,
                    uploadedHashDeleteBatch,
                    cancellationToken).ConfigureAwait(false);

                if (uploadedHashDeleteBatch.Count >= UploadedHashDeleteBatchSize)
                {
                    await FlushUploadedHashDeleteBatchAsync(
                        state,
                        uploadedHashDeleteBatch,
                        cancellationToken).ConfigureAwait(false);
                }

                await PublishIfDueAsync(state, publishAsync, publishStopwatch, cancellationToken)
                    .ConfigureAwait(false);
            }

            await FlushUploadedHashDeleteBatchAsync(
                state,
                uploadedHashDeleteBatch,
                cancellationToken).ConfigureAwait(false);

            state.Update(x => x.CurrentPath = null);
            await publishAsync(state.Snapshot(), cancellationToken).ConfigureAwait(false);
        }

        private async Task<Module> GetStorageModuleAsync(
            StorageCleanupJobState state,
            CancellationToken cancellationToken)
        {
            Module? storageModule = await _dbContext.Modules
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.Id == state.StorageId &&
                        x.UserId == state.UserId &&
                        x.Destination == ModuleDestination.Target,
                    cancellationToken)
                .ConfigureAwait(false);

            if (storageModule is null)
            {
                throw new InvalidOperationException("Storage not found: " + state.StorageId);
            }

            return storageModule;
        }

        private (IBackupStorage Storage, IBackupStorageInventory Inventory) CreateStorageInventory(
            Module storageModule)
        {
            IBackupProvider? provider = _providers
                .FirstOrDefault(x => x.Id == storageModule.BackupModuleId);

            if (provider is not IBackupStorage storage)
            {
                throw new InvalidOperationException(
                    "Storage provider not found: " + storageModule.BackupModuleId);
            }

            if (provider is not IBackupStorageInventory inventory)
            {
                throw new InvalidOperationException(
                    "Storage provider does not support inventory scanning: " + storageModule.BackupModuleId);
            }

            provider.SetParameters(storageModule.Params(_crypto).Snapshot());
            return (storage, inventory);
        }

        private static bool TryCreateStorageChunk(
            BackupFileInfo storageObject,
            char pathSeparator,
            out StorageChunkObject chunkObject)
        {
            chunkObject = default;

            if (!StorageChunkPathParser.TryParse(storageObject.Path, pathSeparator, out string? chunkKey))
            {
                return false;
            }

            chunkObject = new StorageChunkObject(
                chunkKey,
                storageObject.Path,
                storageObject.Size ?? 0);
            return true;
        }

        private async Task DeleteOrphanChunkAsync(
            StorageCleanupJobState state,
            IBackupStorage storage,
            StorageChunkObject chunkObject,
            List<string> uploadedHashDeleteBatch,
            CancellationToken cancellationToken)
        {
            state.Update(x =>
            {
                x.OrphanObjects++;
                x.OrphanBytes += chunkObject.Size;
            });

            bool? deleteResult;
            try
            {
                deleteResult = await storage
                    .DeleteAsync(chunkObject.Path, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to delete orphan chunk {ChunkKey} at {StoragePath}.",
                    chunkObject.ChunkKey,
                    chunkObject.Path);
                state.Update(x => x.FailedDeletes++);
                return;
            }

            if (deleteResult == true)
            {
                uploadedHashDeleteBatch.Add(chunkObject.ChunkKey);
                state.Update(x =>
                {
                    x.DeletedObjects++;
                    x.FreedBytes += chunkObject.Size;
                });
                return;
            }

            if (deleteResult == false)
            {
                uploadedHashDeleteBatch.Add(chunkObject.ChunkKey);
                state.Update(x => x.MissingObjects++);
                return;
            }

            state.Update(x => x.FailedDeletes++);
        }

        private async Task FlushUploadedHashDeleteBatchAsync(
            StorageCleanupJobState state,
            List<string> uploadedHashDeleteBatch,
            CancellationToken cancellationToken)
        {
            if (uploadedHashDeleteBatch.Count == 0)
            {
                return;
            }

            List<string> chunkKeys = uploadedHashDeleteBatch
                .Distinct(StringComparer.Ordinal)
                .ToList();

            uploadedHashDeleteBatch.Clear();

            foreach (string[] batch in chunkKeys.Chunk(UploadedHashDeleteBatchSize))
            {
                int deletedRows = await _dbContext.UploadedHashes
                    .Where(x => x.ModuleId == state.StorageId && batch.Contains(x.Hash))
                    .ExecuteDeleteAsync(cancellationToken)
                    .ConfigureAwait(false);

                state.Update(x => x.UploadedHashRowsDeleted += deletedRows);
            }
        }

        private static async Task PublishIfDueAsync(
            StorageCleanupJobState state,
            Func<StorageCleanupJobDto, CancellationToken, Task> publishAsync,
            Stopwatch publishStopwatch,
            CancellationToken cancellationToken)
        {
            if (publishStopwatch.Elapsed < ProgressPublishInterval)
            {
                return;
            }

            publishStopwatch.Restart();
            await publishAsync(state.Snapshot(), cancellationToken).ConfigureAwait(false);
        }
    }
}
