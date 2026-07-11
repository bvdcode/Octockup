// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.Abstractions;
using Microsoft.EntityFrameworkCore;
using Octockup.Server.Abstractions;
using Octockup.Server.Archives;
using Octockup.Server.Database;
using Octockup.Server.Models;
using Octockup.Server.Streams;
using System.Runtime.CompilerServices;

namespace Octockup.Server.Services
{
    public class SnapshotArchiveRunner(
        IStreamCipher _crypto,
        AppDbContext _dbContext,
        SnapshotChunkReferenceIndexer _referenceIndexer,
        ILogger<SnapshotArchiveRunner> _logger,
        IEnumerable<IBackupProvider> _providers)
    {
        private const int FileBatchSize = 50;

        public async Task WriteAsync(
            SnapshotArchiveJob job,
            SnapshotArchiveProgressTracker progress,
            Stream output,
            CancellationToken cancellationToken)
        {
            Snapshot snapshot = await LoadSnapshotAsync(job, cancellationToken)
                .ConfigureAwait(false);
            await EnsureCleanupIsNotActiveAsync(
                snapshot.Backup.StorageId,
                cancellationToken).ConfigureAwait(false);
            IBackupStorage storage = ResolveStorage(snapshot);

            await _referenceIndexer.IndexSnapshotAsync(
                snapshot.Backup.StorageId,
                snapshot.Id,
                progress.ReportPreparationAsync,
                cancellationToken).ConfigureAwait(false);
            await progress.BeginStreamingAsync(cancellationToken).ConfigureAwait(false);

            string spoolDirectory = Path.Combine(
                Path.GetTempPath(),
                "octockup-archive-metadata");
            Directory.CreateDirectory(spoolDirectory);
            string spoolPath = Path.Combine(
                spoolDirectory,
                $"{job.Id:N}-{job.RunId:N}.zipdir");
            FileStreamOptions options = new()
            {
                Mode = FileMode.Create,
                Access = FileAccess.ReadWrite,
                Share = FileShare.None,
                BufferSize = 64 * 1024,
                Options = FileOptions.Asynchronous |
                    FileOptions.SequentialScan |
                    FileOptions.DeleteOnClose
            };
            await using FileStream centralDirectorySpool = new(spoolPath, options);

            await StoredZipArchiveWriter.WriteAsync(
                output,
                EnumerateEntriesAsync(
                    snapshot,
                    storage,
                    progress,
                    cancellationToken),
                centralDirectorySpool,
                progress.ReportStreamingAsync,
                cancellationToken).ConfigureAwait(false);
            await progress.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        private async Task<Snapshot> LoadSnapshotAsync(
            SnapshotArchiveJob job,
            CancellationToken cancellationToken)
        {
            Snapshot? snapshot = await _dbContext.Snapshots
                .AsNoTracking()
                .Include(x => x.Backup)
                    .ThenInclude(x => x.Source)
                .Include(x => x.Backup)
                    .ThenInclude(x => x.Storage)
                .SingleOrDefaultAsync(
                    x => x.Id == job.SnapshotId &&
                        x.CompletedAt != null &&
                        x.Backup.Source.UserId == job.UserId,
                    cancellationToken)
                .ConfigureAwait(false);
            return snapshot ?? throw new InvalidOperationException(
                "The completed snapshot for this archive job no longer exists.");
        }

        private async Task EnsureCleanupIsNotActiveAsync(
            Guid storageId,
            CancellationToken cancellationToken)
        {
            bool cleanupActive = await _dbContext.StorageCleanupJobs
                .AsNoTracking()
                .AnyAsync(
                    x => x.ActiveStorageId == storageId,
                    cancellationToken)
                .ConfigureAwait(false);
            if (cleanupActive)
            {
                throw new InvalidOperationException(
                    "Storage cleanup is active. Retry the archive after cleanup finishes.");
            }
        }

        private IBackupStorage ResolveStorage(Snapshot snapshot)
        {
            IBackupProvider? provider = _providers.FirstOrDefault(
                x => x.Id == snapshot.Backup.Storage.BackupModuleId);
            if (provider is null)
            {
                throw new InvalidOperationException(
                    "The snapshot storage provider is unavailable.");
            }

            provider.SetParameters(snapshot.Backup.Storage.Params(_crypto).Snapshot());
            return provider as IBackupStorage ?? throw new InvalidOperationException(
                "The snapshot provider is not a backup storage.");
        }

        private async IAsyncEnumerable<StoredZipArchiveEntry> EnumerateEntriesAsync(
            Snapshot snapshot,
            IBackupStorage storage,
            SnapshotArchiveProgressTracker progress,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            string? lastPath = null;
            while (true)
            {
                IQueryable<SnapshotFile> query = _dbContext.SnapshotFiles
                    .AsNoTracking()
                    .Where(x => x.SnapshotId == snapshot.Id);
                if (lastPath is not null)
                {
                    query = query.Where(x => string.Compare(x.Path, lastPath) > 0);
                }

                List<SnapshotArchiveFileDescriptor> files = await query
                    .OrderBy(x => x.Path)
                    .Take(FileBatchSize)
                    .Select(x => new SnapshotArchiveFileDescriptor
                    {
                        Id = x.Id,
                        Path = x.Path,
                        Name = x.Name,
                        Size = x.Size,
                        LastModified = x.LastModified
                    })
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);
                if (files.Count == 0)
                {
                    yield break;
                }

                Dictionary<Guid, long> restoredSizes = await LoadRestoredSizesAsync(
                    snapshot.Backup.StorageId,
                    files,
                    cancellationToken).ConfigureAwait(false);
                foreach (SnapshotArchiveFileDescriptor file in files)
                {
                    string fallbackName = file.Name.Length > 0
                        ? file.Name
                        : file.Id.ToString("N");
                    string entryName = StoredZipArchiveWriter.NormalizeEntryName(
                        file.Path,
                        fallbackName);
                    await progress
                        .SetCurrentPathAsync(entryName, cancellationToken)
                        .ConfigureAwait(false);

                    long restoredSize = restoredSizes[file.Id];
                    yield return new StoredZipArchiveEntry(
                        entryName,
                        restoredSize,
                        file.LastModified,
                        streamCancellationToken =>
                        {
                            SnapshotChunkDescriptorReader chunks = new(
                                _dbContext,
                                snapshot.Backup.StorageId,
                                file.Id,
                                _logger);
                            SnapshotFile snapshotFile = new()
                            {
                                Path = file.Path,
                                Name = file.Name,
                                Size = file.Size,
                                LastModified = file.LastModified
                            };
                            Stream stream = new SnapshotConcatStream(
                                _logger,
                                storage,
                                chunks.ReadNextAsync,
                                snapshotFile,
                                _crypto,
                                restoredSize,
                                streamCancellationToken);
                            return Task.FromResult(stream);
                        });
                }

                lastPath = files[^1].Path;
                _dbContext.ChangeTracker.Clear();
            }
        }

        private async Task<Dictionary<Guid, long>> LoadRestoredSizesAsync(
            Guid storageId,
            IReadOnlyCollection<SnapshotArchiveFileDescriptor> files,
            CancellationToken cancellationToken)
        {
            Guid[] fileIds = files.Select(x => x.Id).ToArray();
            var chunkMetadata =
                from reference in _dbContext.SnapshotChunkReferences.AsNoTracking()
                where fileIds.Contains(reference.SnapshotFileId)
                join uploadedHash in _dbContext.UploadedHashes
                        .AsNoTracking()
                        .Where(x => x.ModuleId == storageId)
                    on reference.ChunkHash equals uploadedHash.Hash into uploadedHashes
                from uploadedHash in uploadedHashes.DefaultIfEmpty()
                select new
                {
                    reference.SnapshotFileId,
                    OriginalSize = uploadedHash == null
                        ? (long?)null
                        : uploadedHash.OriginalSize
                };
            var aggregates = await chunkMetadata
                .GroupBy(x => x.SnapshotFileId)
                .Select(group => new
                {
                    SnapshotFileId = group.Key,
                    ReferenceCount = group.Count(),
                    KnownSizeCount = group.Count(x => x.OriginalSize.HasValue),
                    RestoredSize = group.Sum(x => x.OriginalSize) ?? 0
                })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            Dictionary<Guid, long> restoredSizes = files.ToDictionary(
                x => x.Id,
                x => x.Size);
            foreach (var aggregate in aggregates)
            {
                if (aggregate.ReferenceCount == aggregate.KnownSizeCount)
                {
                    restoredSizes[aggregate.SnapshotFileId] = aggregate.RestoredSize;
                }
            }

            return restoredSizes;
        }
    }
}
