// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.Abstractions;
using Microsoft.EntityFrameworkCore;
using Octockup.Server.Abstractions;
using Octockup.Server.Database;
using Octockup.Server.Helpers;
using Octockup.Server.Models.Enums;
using Octockup.Server.Models.Results;

namespace Octockup.Server.Services
{
    public class StorageGarbageCollectionService(
        IStreamCipher _crypto,
        AppDbContext _dbContext,
        ILogger<StorageGarbageCollectionService> _logger,
        IEnumerable<IBackupProvider> _providers,
        ChunkReferenceCollector _chunkReferenceCollector)
    {
        public async Task<StorageGarbageCollectionResult> CollectAsync(
            Guid userId,
            Guid storageId,
            CancellationToken cancellationToken)
        {
            Module? storageModule = await _dbContext.Modules
                .FirstOrDefaultAsync(
                    x => x.Id == storageId &&
                        x.UserId == userId &&
                        x.Destination == ModuleDestination.Target,
                    cancellationToken);

            if (storageModule is null)
            {
                throw new InvalidOperationException("Storage not found: " + storageId);
            }

            IBackupStorage storage = CreateStorage(storageModule);
            HashSet<string> referencedChunks = await _chunkReferenceCollector
                .CollectForStorageAsync(storageId, cancellationToken);

            List<UploadedHash> uploadedHashes = await _dbContext.UploadedHashes
                .Where(x => x.ModuleId == storageId)
                .ToListAsync(cancellationToken);

            StorageGarbageCollectionResult result = new()
            {
                StorageId = storageId,
                UploadedHashesScanned = uploadedHashes.Count,
                ReferencedChunks = referencedChunks.Count
            };

            foreach (UploadedHash uploadedHash in uploadedHashes)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (referencedChunks.Contains(uploadedHash.Hash))
                {
                    continue;
                }

                result.OrphanChunks++;
                string path = ChunkStorageHelpers.GetStoragePath(
                    uploadedHash.Hash,
                    storage.PathSeparator);

                bool? deleted = await DeleteStorageObjectAsync(
                    storage,
                    path,
                    uploadedHash.Hash,
                    cancellationToken);

                if (deleted is null)
                {
                    result.FailedDeletes++;
                    continue;
                }

                if (deleted == true)
                {
                    result.DeletedObjects++;
                }
                else
                {
                    result.MissingObjects++;
                }

                result.FreedStoredSize += uploadedHash.StoredSize;
                _dbContext.UploadedHashes.Remove(uploadedHash);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            return result;
        }

        private IBackupStorage CreateStorage(Module storageModule)
        {
            IBackupProvider? provider = _providers
                .FirstOrDefault(x => x.Id == storageModule.BackupModuleId);

            if (provider is not IBackupStorage storageProvider)
            {
                throw new InvalidOperationException(
                    "Storage provider not found: " + storageModule.BackupModuleId);
            }

            storageProvider.SetParameters(storageModule.Params(_crypto).Snapshot());
            return storageProvider;
        }

        private async Task<bool?> DeleteStorageObjectAsync(
            IBackupStorage storage,
            string path,
            string chunkHash,
            CancellationToken cancellationToken)
        {
            try
            {
                bool? deleteResult = await storage.DeleteAsync(path, cancellationToken);

                if (deleteResult == true)
                {
                    return true;
                }

                if (deleteResult == false)
                {
                    _logger.LogInformation(
                        "Chunk {ChunkHash} was already missing from storage path {StoragePath}.",
                        chunkHash,
                        path);
                    return false;
                }

                _logger.LogWarning(
                    "Storage did not confirm deletion for chunk {ChunkHash} at {StoragePath}.",
                    chunkHash,
                    path);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to delete unreferenced chunk {ChunkHash} at {StoragePath}.",
                    chunkHash,
                    path);
                return null;
            }
        }
    }
}
