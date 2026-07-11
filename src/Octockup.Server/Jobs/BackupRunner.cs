// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.Abstractions;
using EasyExtensions.Models.Enums;
using EasyExtensions.Streams;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Octockup.Server.Abstractions;
using Octockup.Server.Collections;
using Octockup.Server.Database;
using Octockup.Server.Helpers;
using Octockup.Server.Models;
using Octockup.Server.Models.Enums;
using Octockup.Server.Models.Options;
using Octockup.Server.Services;
using System.Buffers;
using System.Diagnostics;
using System.Security.Cryptography;

namespace Octockup.Server.Jobs
{
    public class BackupRunner(
        IStreamCipher crypto,
        AppDbContext dbContext,
        IServiceProvider serviceProvider,
        ILogger<BackupRunner> logger,
        IScheduleProgressPublisher scheduleProgressPublisher,
        IOptions<BackupProgressOptions> backupProgressOptions,
        TimeProvider timeProvider,
        ILogger<ScheduleReport> scheduleReportLogger,
        IEnumerable<IBackupProvider> providers,
        UploadedChunkLookup uploadedChunkLookup,
        PreviousSnapshotFileLookup previousSnapshotFileLookup,
        UploadedHashWriter uploadedHashWriter,
        SnapshotChunkReferenceWriter snapshotChunkReferenceWriter)
    {
        private const int ChunkSize = 8 * 1024 * 1024;
        private const int FileEnumerationBufferCapacity = 1_000;
        private readonly List<UploadedHash> _pendingUploadedHashes = [];
        private readonly Stopwatch _uploadedHashesStopwatch = Stopwatch.StartNew();
        private const int UploadedHashesFlushCount = 500; // flush every 500 new hashes
        private static readonly TimeSpan UploadedHashesFlushInterval = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan SnapshotFlushInterval = TimeSpan.FromSeconds(10);

        public async Task RunAsync(Schedule schedule, CancellationToken cancellationToken)
        {
            Guid userId = schedule.Backup.Source.UserId;
            ScheduleReport report = new(
                userId,
                schedule.Id,
                schedule.BackupId,
                scheduleProgressPublisher,
                backupProgressOptions,
                timeProvider,
                scheduleReportLogger);
            IBackupSource? sourceProvider = null;
            IBackupStorage? storageProvider = null;
            report.Update(
                0,
                "Preparing backup...",
                stage: BackupProgressStage.Preparing);
            report.StartBackgroundReporting(cancellationToken);

            try
            {
                sourceProvider = await CreateSourceProviderAsync(schedule, cancellationToken);
                if (sourceProvider is null)
                {
                    await report.PublishFinalAsync(
                        0,
                        schedule.ErrorMessage ?? "Source provider not found.",
                        ScheduleStatus.Failed,
                        BackupProgressStage.Failed,
                        cancellationToken);
                    return;
                }

                storageProvider = await CreateStorageProviderAsync(schedule, cancellationToken);
                if (storageProvider is null)
                {
                    await report.PublishFinalAsync(
                        0,
                        schedule.ErrorMessage ?? "Storage provider not found.",
                        ScheduleStatus.Failed,
                        BackupProgressStage.Failed,
                        cancellationToken);
                    return;
                }

                report.Update(
                    0,
                    "Listing files to backup...",
                    stage: BackupProgressStage.Listing);
                schedule.Status = ScheduleStatus.Running;
                await dbContext.SaveChangesAsync(cancellationToken);

                // Set ignored paths before enumerating files
                sourceProvider.SetIgnoredPaths(schedule.Backup.IgnoredPaths);

                IAsyncEnumerable<BackupFileInfo> filesToBackup = sourceProvider.GetFilesAsync(
                    recursive: true,
                    cancellationToken: cancellationToken);
                await BackupAsync(schedule, sourceProvider, storageProvider, report, filesToBackup, cancellationToken);

                schedule.Status = ScheduleStatus.Completed;
                schedule.FinishedAt = DateTime.UtcNow;
                schedule.NextRunAt = ScheduleHelpers.CalculateNextRun(
                    schedule,
                    schedule.FinishedAt.Value);
                await dbContext.SaveChangesAsync(cancellationToken);

                logger.LogInformation("Schedule {ScheduleId} backup completed successfully", schedule.Id);
                await report.PublishFinalAsync(
                    report.Processed,
                    "Backup completed successfully.",
                    ScheduleStatus.Completed,
                    BackupProgressStage.Completed,
                    cancellationToken);
            }
            catch (OperationCanceledException ex)
            {
                // Log the actual cancellation source for debugging
                logger.LogWarning(ex, "Schedule {ScheduleId} received OperationCanceledException. " +
                    "Token.IsCancellationRequested={IsCancellationRequested}, InnerException={InnerException}",
                    schedule.Id, cancellationToken.IsCancellationRequested, ex.InnerException?.Message);

                if (cancellationToken.IsCancellationRequested)
                {
                    await HandleCancellationAsync(schedule, report);
                }
                else
                {
                    // Cancellation from somewhere else (timeout, network issue, etc.)
                    schedule.ErrorMessage = $"Backup interrupted: {ex.Message}";
                    schedule.Status = ScheduleStatus.Failed;
                    schedule.FinishedAt = DateTime.UtcNow;
                    schedule.NextRunAt = ScheduleHelpers.CalculateNextRun(
                        schedule,
                        schedule.FinishedAt.Value);
                    await dbContext.SaveChangesAsync(CancellationToken.None);

                    logger.LogError(ex, "Schedule {ScheduleId} backup interrupted unexpectedly", schedule.Id);
                    await report.PublishFinalAsync(
                        report.Processed,
                        schedule.ErrorMessage,
                        ScheduleStatus.Failed,
                        BackupProgressStage.Failed,
                        CancellationToken.None);
                }
                return;
            }
            catch (Exception ex)
            {
                schedule.ErrorMessage = $"Backup failed: {ex.Message}";
                schedule.Status = ScheduleStatus.Failed;
                schedule.FinishedAt = DateTime.UtcNow;
                schedule.NextRunAt = ScheduleHelpers.CalculateNextRun(
                    schedule,
                    schedule.FinishedAt.Value);
                await dbContext.SaveChangesAsync(CancellationToken.None);

                logger.LogError(ex, "Schedule {ScheduleId} backup failed", schedule.Id);
                await report.PublishFinalAsync(
                    report.Processed,
                    schedule.ErrorMessage,
                    ScheduleStatus.Failed,
                    BackupProgressStage.Failed,
                    CancellationToken.None);
            }
            finally
            {
                try
                {
                    await FlushUploadedHashesAsync(CancellationToken.None);
                }
                catch (Exception flushEx)
                {
                    logger.LogError(flushEx, "Failed to flush pending uploaded hashes after backup execution");
                }

                await DisposeProviderAsync(storageProvider);
                if (!ReferenceEquals(sourceProvider, storageProvider))
                {
                    await DisposeProviderAsync(sourceProvider);
                }
                await report.DisposeAsync();
            }
        }

        private async ValueTask DisposeProviderAsync(IBackupProvider? provider)
        {
            try
            {
                if (provider is IAsyncDisposable asyncDisposable)
                {
                    await asyncDisposable.DisposeAsync();
                }
                else if (provider is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to dispose backup provider {Provider}", provider?.Id);
            }
        }

        private async Task<IBackupSource?> CreateSourceProviderAsync(
            Schedule schedule,
            CancellationToken cancellationToken)
        {
            if (providers.FirstOrDefault(x => x.Id == schedule.Backup.Source.BackupModuleId) is not IBackupSource foundSourceTypeProvider)
            {
                schedule.ErrorMessage = $"Source provider not found: {schedule.Backup.Source.BackupModuleId}";
                schedule.Status = ScheduleStatus.Failed;
                schedule.FinishedAt = DateTime.UtcNow;
                schedule.NextRunAt = ScheduleHelpers.CalculateNextRun(
                    schedule,
                    schedule.FinishedAt.Value);
                await dbContext.SaveChangesAsync(cancellationToken);
                logger.LogWarning("{msg}", schedule.ErrorMessage);

                return null;
            }

            IBackupSource foundSourceProvider = (IBackupSource)ActivatorUtilities.CreateInstance(serviceProvider, foundSourceTypeProvider.GetType());
            foundSourceProvider.SetParameters(schedule.Backup.Source.Params(crypto).Snapshot());
            return foundSourceProvider;
        }

        private async Task<IBackupStorage?> CreateStorageProviderAsync(
            Schedule schedule,
            CancellationToken cancellationToken)
        {
            if (providers.FirstOrDefault(x => x.Id == schedule.Backup.Storage.BackupModuleId) is not IBackupStorage foundStorageTypeProvider)
            {
                schedule.ErrorMessage = $"Storage provider not found: {schedule.Backup.Storage.BackupModuleId}";
                schedule.Status = ScheduleStatus.Failed;
                schedule.FinishedAt = DateTime.UtcNow;
                schedule.NextRunAt = ScheduleHelpers.CalculateNextRun(
                    schedule,
                    schedule.FinishedAt.Value);
                await dbContext.SaveChangesAsync(cancellationToken);
                logger.LogWarning("{msg}", schedule.ErrorMessage);

                return null;
            }

            IBackupStorage foundStorageProvider = (IBackupStorage)ActivatorUtilities.CreateInstance(serviceProvider, foundStorageTypeProvider.GetType());
            foundStorageProvider.SetParameters(schedule.Backup.Storage.Params(crypto).Snapshot());
            return foundStorageProvider;
        }

        private async Task BackupAsync(
            Schedule schedule,
            IBackupSource source,
            IBackupStorage storage,
            ScheduleReport report,
            IAsyncEnumerable<BackupFileInfo> lazyFiles,
            CancellationToken cancellationToken)
        {
            report.SetStage(BackupProgressStage.Preparing, "Loading previous snapshot metadata...");
            await previousSnapshotFileLookup
                .InitializeAsync(schedule.BackupId, cancellationToken)
                .ConfigureAwait(false);
            logger.LogInformation(
                "Using completed snapshot {SnapshotId} with {FileCount} files as the incremental baseline.",
                previousSnapshotFileLookup.SnapshotId,
                previousSnapshotFileLookup.PreviousFileCount);

            report.SetStage(BackupProgressStage.Preparing, "Indexing stored chunks...");
            await uploadedChunkLookup
                .InitializeAsync(
                    schedule.Backup.StorageId,
                    indexed => report.SetStage(
                        BackupProgressStage.Preparing,
                        $"Indexing stored chunks: {indexed:N0}"),
                    cancellationToken)
                .ConfigureAwait(false);
            logger.LogInformation(
                "Indexed {ChunkCount} stored chunks using {FilterBytes} bytes of bounded lookup memory.",
                uploadedChunkLookup.IndexedCount,
                uploadedChunkLookup.FilterByteCount);
            report.SetStage(BackupProgressStage.Listing, "Listing files to backup...");

            SnapshotBatchWriter snapshotWriter = new(
                dbContext,
                snapshotChunkReferenceWriter);
            Snapshot snapshot = await snapshotWriter.CreateAsync(
                schedule.BackupId,
                schedule,
                cancellationToken);
            await using LazyLoader<BackupFileInfo> loader = new(
                lazyFiles,
                FileEnumerationBufferCapacity,
                cancellationToken);

            int counter = 0;
            Stopwatch stopwatch = Stopwatch.StartNew();
            List<BackupFileInfo> fileBatch = new(PreviousSnapshotFileLookup.MaxBatchSize);

            cancellationToken.ThrowIfCancellationRequested();
            await foreach (BackupFileInfo file in loader.ReadAllAsync(cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                fileBatch.Add(file);
                if (fileBatch.Count < PreviousSnapshotFileLookup.MaxBatchSize)
                {
                    continue;
                }

                counter = await ProcessFileBatchAsync(
                    schedule,
                    snapshot,
                    snapshotWriter,
                    source,
                    storage,
                    report,
                    loader,
                    stopwatch,
                    counter,
                    fileBatch,
                    cancellationToken);
                fileBatch.Clear();
            }

            if (fileBatch.Count > 0)
            {
                counter = await ProcessFileBatchAsync(
                    schedule,
                    snapshot,
                    snapshotWriter,
                    source,
                    storage,
                    report,
                    loader,
                    stopwatch,
                    counter,
                    fileBatch,
                    cancellationToken);
            }

            report.SetEnumeration(loader.Total, true);
            await FinalizeSnapshotAsync(
                schedule,
                snapshot,
                snapshotWriter,
                loader,
                report,
                cancellationToken);
        }

        private async Task<int> ProcessFileBatchAsync(
            Schedule schedule,
            Snapshot snapshot,
            SnapshotBatchWriter snapshotWriter,
            IBackupSource source,
            IBackupStorage storage,
            ScheduleReport report,
            LazyLoader<BackupFileInfo> loader,
            Stopwatch stopwatch,
            int counter,
            IReadOnlyList<BackupFileInfo> files,
            CancellationToken cancellationToken)
        {
            string[] paths = files.Select(x => x.Path).ToArray();
            IReadOnlyDictionary<string, SnapshotFile> previousFiles =
                await previousSnapshotFileLookup
                    .LoadBatchAsync(paths, cancellationToken)
                    .ConfigureAwait(false);

            foreach (BackupFileInfo file in files)
            {
                counter = await ProcessFileAsync(
                    schedule,
                    snapshot,
                    snapshotWriter,
                    source,
                    storage,
                    report,
                    loader,
                    previousFiles,
                    stopwatch,
                    counter,
                    file,
                    cancellationToken);
            }

            return counter;
        }

        private async Task<int> ProcessFileAsync(
            Schedule schedule,
            Snapshot snapshot,
            SnapshotBatchWriter snapshotWriter,
            IBackupSource source,
            IBackupStorage storage,
            ScheduleReport report,
            LazyLoader<BackupFileInfo> loader,
            IReadOnlyDictionary<string, SnapshotFile> previousFiles,
            Stopwatch stopwatch,
            int counter,
            BackupFileInfo file,
            CancellationToken cancellationToken)
        {
            counter++;
            report.SetCurrentFile(
                file.Name ?? Path.GetFileName(file.Path),
                file.Path,
                loader.Total,
                loader.IsEnumerationCompleted);
            report.Update(
                counter,
                $"Processing: {file.Name}",
                stage: BackupProgressStage.Preparing);

            if (ShouldIgnoreFile(schedule, file))
            {
                logger.LogDebug("Schedule {ScheduleId}: File {FileName} is ignored by path rules, skipping",
                    schedule.Id, file.Name);
                return counter;
            }

            cancellationToken.ThrowIfCancellationRequested();
            previousFiles.TryGetValue(file.Path, out var previousFile);

            if (previousFile != null && CanReusePreviousFile(previousFile, file, out bool datesMatch))
            {
                await ReusePreviousFileAsync(
                    schedule,
                    snapshot,
                    snapshotWriter,
                    previousFile,
                    file,
                    datesMatch,
                    stopwatch,
                    report,
                    counter,
                    cancellationToken);
                return counter;
            }

            if (logger.IsEnabled(LogLevel.Debug) && previousFile != null)
            {
                bool diagnosticDatesMatch = previousFile.LastModified == null || file.LastModified == null ||
                    Math.Abs((previousFile.LastModified.Value - file.LastModified.Value).TotalSeconds) < 2;

                logger.LogDebug("File {FileName} NOT skipped - foundFile!=null: true, hasHashsum: {HasHashsum}, sizeMatch: " +
                    "{SizeMatch} ({OldSize} vs {NewSize}), datesMatch: {DatesMatch}, oldDate: {OldDate}, newDate: {NewDate}, diffSec: {DiffSec}",
                    file.Name,
                    previousFile.Hashsum != null,
                    file.Size == previousFile.Size, previousFile.Size, file.Size,
                    diagnosticDatesMatch,
                    previousFile.LastModified?.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    file.LastModified?.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                    previousFile.LastModified != null && file.LastModified != null
                        ? Math.Abs((previousFile.LastModified.Value - file.LastModified.Value).TotalSeconds).ToString("F3")
                        : "N/A");
            }
            else if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("File {FileName} NOT found in previous snapshot", file.Path);
            }

            if (logger.IsEnabled(LogLevel.Debug) && previousFile != null)
            {
                logger.LogDebug("File {FileName} changed - HasHashsum: {HasHashsum}, Size: {OldSize} vs {NewSize}, LastModified: {OldModified} vs {NewModified} (diff: {DiffSeconds}s)",
                    file.Name, previousFile.Hashsum != null, previousFile.Size, file.Size,
                    previousFile.LastModified?.ToString("yyyy-MM-dd HH:mm:ss"), file.LastModified?.ToString("yyyy-MM-dd HH:mm:ss"),
                    previousFile.LastModified != null && file.LastModified != null
                        ? Math.Abs((previousFile.LastModified.Value - file.LastModified.Value).TotalSeconds)
                        : -1);
            }

            report.SetStage(BackupProgressStage.Reading, $"Reading: {file.Name}");
            using var stream = await source.GetFileStreamAsync(file, cancellationToken);
            if (stream == Stream.Null)
            {
                logger.LogWarning("Unable to get stream for file {FileName}, skipping", file.Name);
                return counter;
            }

            var (fileHash, chunkHashes) = await ProcessChunksAsync(
                schedule,
                file,
                storage,
                report,
                stream,
                counter,
                cancellationToken);

            report.SetStage(BackupProgressStage.Recording, $"Recording: {file.Name}");
            SnapshotFile snapshotFile = new()
            {
                Path = file.Path,
                Hashsum = fileHash,
                Size = file.Size ?? 0,
                ChunkHashes = chunkHashes,
                Name = file.Name ?? file.Path,
                LastModified = file.LastModified,
            };
            await snapshotWriter.AddFileAsync(
                snapshot,
                schedule,
                schedule.Backup.StorageId,
                snapshotFile,
                cancellationToken);
            await PersistSnapshotIfDueAsync(
                schedule,
                snapshot,
                snapshotWriter,
                stopwatch,
                report,
                file.Name ?? file.Path,
                cancellationToken);

            return counter;
        }

        private async Task<(string FileHash, List<string> ChunkHashes)> ProcessChunksAsync(
            Schedule schedule,
            BackupFileInfo file,
            IBackupStorage storage,
            ScheduleReport report,
            Stream stream,
            int counter,
            CancellationToken cancellationToken)
        {
            using var chunker = new ChunkedStream(stream, ChunkSize);
            byte[] buffer = ArrayPool<byte>.Shared.Rent(ChunkSize);
            try
            {
                using var fileHasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                List<string> chunkHashes = [];

                foreach (Stream chunk in chunker.GetChunks())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    report.SetStage(BackupProgressStage.Hashing, $"Hashing: {file.Name}");

                    chunk.Seek(0, SeekOrigin.Begin);
                    using var chunkHasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                    int read;
                    long chunkLength = 0L;
                    while ((read = await chunk.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, ChunkSize)), cancellationToken)) > 0)
                    {
                        chunkHasher.AppendData(buffer, 0, read);
                        fileHasher.AppendData(buffer, 0, read);
                        chunkLength += read;
                    }

                    string contentHash = Convert.ToHexString(chunkHasher.GetHashAndReset()).ToLowerInvariant();
                    CompressionAlgorithm algorithm = schedule.Backup.DisableCompression
                        ? CompressionAlgorithm.None
                        : CompressionHelpers.Algorithm;
                    bool encryptChunk = !schedule.Backup.DisableEncryption;
                    string chunkKey = ChunkStorageHelpers.CreateKey(contentHash, algorithm, encryptChunk);
                    bool alreadyUploaded = await uploadedChunkLookup
                        .ContainsAsync(chunkKey, cancellationToken)
                        .ConfigureAwait(false);
                    if (alreadyUploaded)
                    {
                        logger.LogDebug(
                            "Chunk {ChunkHash} for file {FileName} already uploaded, skipping upload",
                            contentHash,
                            file.Name);
                        chunkHashes.Add(chunkKey);
                        report.Update(
                            counter,
                            $"Processing: {file.Name}",
                            processedBytes: chunkLength,
                            stage: BackupProgressStage.Preparing);
                        await chunk.DisposeAsync();
                        continue;
                    }

                    logger.LogDebug(
                        "Processing chunk {ChunkHash} for file {FileName}",
                        contentHash,
                        file.Path);

                    long storedSize = 0;
                    string path = ChunkStorageHelpers.GetStoragePath(chunkKey, storage.PathSeparator);
                    logger.LogDebug(
                        "Uploading chunk {ChunkHash} for file {FileName}, size {ChunkBytes} bytes",
                        contentHash,
                        file.Name,
                        chunkLength);

                    chunk.Seek(0, SeekOrigin.Begin);

                    MemoryStream? compressedStream = null;
                    Stream src = chunk;
                    if (algorithm != CompressionAlgorithm.None)
                    {
                        report.SetStage(BackupProgressStage.Compressing, $"Compressing: {file.Name}");
                        compressedStream = new MemoryStream();
                        await using (var compressed = CompressionHelpers.CreateCompressionStream(compressedStream))
                        {
                            int r;
                            while ((r = await chunk.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, ChunkSize)), cancellationToken)) > 0)
                            {
                                await compressed.WriteAsync(buffer.AsMemory(0, r), cancellationToken);
                            }
                        }
                        compressedStream.Seek(0, SeekOrigin.Begin);
                        src = compressedStream;
                        logger.LogDebug(
                            "Chunk {ChunkHash} for file {FileName} compressed from {OriginalBytes} " +
                            "to {CompressedBytes} bytes using {Algorithm}",
                            contentHash,
                            file.Name,
                            chunkLength,
                            src.Length,
                            algorithm);
                    }
                    else
                    {
                        logger.LogDebug(
                            "Chunk {ChunkHash} for file {FileName} stored without compression",
                            contentHash,
                            file.Name);
                    }

                    try
                    {
                        if (src.CanSeek)
                        {
                            src.Seek(0, SeekOrigin.Begin);
                        }

                        if (encryptChunk)
                        {
                            report.SetStage(BackupProgressStage.Encrypting, $"Encrypting: {file.Name}");
                            using var encryptedStream = new MemoryStream();
                            await crypto.EncryptAsync(src, encryptedStream, ct: cancellationToken);
                            encryptedStream.Seek(0, SeekOrigin.Begin);
                            storedSize = encryptedStream.Length;
                            report.SetStage(BackupProgressStage.Uploading, $"Uploading: {file.Name}");
                            await storage.UploadAsync(path, encryptedStream, cancellationToken);
                        }
                        else
                        {
                            storedSize = src.CanSeek ? src.Length : chunkLength;
                            report.SetStage(BackupProgressStage.Uploading, $"Uploading: {file.Name}");
                            await storage.UploadAsync(path, src, cancellationToken);
                        }

                        report.SetStage(BackupProgressStage.Recording, $"Recording: {file.Name}");
                        RecordUploadedHash(
                            schedule.Backup.StorageId,
                            chunkKey,
                            storedSize,
                            chunkLength,
                            algorithm);

                        report.Update(
                            counter,
                            $"Processing: {file.Name}",
                            processedBytes: chunkLength,
                            stage: BackupProgressStage.Preparing);

                        chunkHashes.Add(chunkKey);
                        cancellationToken.ThrowIfCancellationRequested();
                    }
                    finally
                    {
                        if (compressedStream != null)
                        {
                            await compressedStream.DisposeAsync();
                        }
                    }

                    await chunk.DisposeAsync();

                    // Periodically flush batched UploadedHashes to DB
                    if (_pendingUploadedHashes.Count >= UploadedHashesFlushCount ||
                        _uploadedHashesStopwatch.Elapsed > UploadedHashesFlushInterval)
                    {
                        report.SetStage(BackupProgressStage.Persisting, $"Saving chunks: {file.Name}");
                        await FlushUploadedHashesAsync(cancellationToken);
                    }
                }

                string fileHash = Convert.ToHexString(fileHasher.GetHashAndReset()).ToLowerInvariant();
                return (fileHash, chunkHashes);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        private void RecordUploadedHash(
            Guid storageModuleId,
            string chunkKey,
            long storedSize,
            long originalSize,
            CompressionAlgorithm algorithm)
        {
            if (!uploadedChunkLookup.MarkPending(chunkKey))
            {
                return;
            }

            UploadedHash uploadedHash = new()
            {
                Hash = chunkKey,
                StoredSize = storedSize,
                OriginalSize = originalSize,
                ModuleId = storageModuleId,
                CompressionAlgorithm = algorithm
            };
            _pendingUploadedHashes.Add(uploadedHash);
        }

        private async Task FlushUploadedHashesAsync(CancellationToken cancellationToken)
        {
            if (_pendingUploadedHashes.Count == 0)
            {
                _uploadedHashesStopwatch.Restart();
                return;
            }

            await uploadedHashWriter.FlushAsync(_pendingUploadedHashes, cancellationToken);
            _pendingUploadedHashes.Clear();
            uploadedChunkLookup.CommitPending();
            _uploadedHashesStopwatch.Restart();
        }

        private static bool CanReusePreviousFile(SnapshotFile previousFile, BackupFileInfo currentFile, out bool datesMatch)
        {
            datesMatch = previousFile.LastModified == null || currentFile.LastModified == null ||
                Math.Abs((previousFile.LastModified.Value - currentFile.LastModified.Value).TotalSeconds) < 2;

            return previousFile.Hashsum != null && currentFile.Size == previousFile.Size && datesMatch;
        }

        private async Task HandleCancellationAsync(Schedule schedule, ScheduleReport report)
        {
            logger.LogInformation("Schedule {ScheduleId} backup canceled", schedule.Id);

            schedule.Status = ScheduleStatus.Failed;
            schedule.ErrorMessage = "Backup was canceled.";
            schedule.FinishedAt = DateTime.UtcNow;
            schedule.NextRunAt = ScheduleHelpers.CalculateNextRun(
                schedule,
                schedule.FinishedAt.Value);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            await report.PublishFinalAsync(
                report.Processed,
                "Backup canceled.",
                ScheduleStatus.Failed,
                BackupProgressStage.Failed,
                CancellationToken.None);
        }

        private async Task ReusePreviousFileAsync(
            Schedule schedule,
            Snapshot snapshot,
            SnapshotBatchWriter snapshotWriter,
            SnapshotFile previousFile,
            BackupFileInfo currentFile,
            bool datesMatch,
            Stopwatch stopwatch,
            ScheduleReport report,
            int counter,
            CancellationToken cancellationToken)
        {
            logger.LogDebug("File {FileName} unchanged since last snapshot (size: {Size}, date match: {DateMatch}), reusing metadata",
                currentFile.Name, currentFile.Size, datesMatch);

            SnapshotFile snapshotFile = new()
            {
                Path = currentFile.Path,
                Size = currentFile.Size ?? 0,
                Hashsum = previousFile.Hashsum,
                Name = currentFile.Name ?? currentFile.Path,
                LastModified = currentFile.LastModified,
                ChunkHashes = previousFile.ChunkHashes,
            };
            await snapshotWriter.AddFileAsync(
                snapshot,
                schedule,
                schedule.Backup.StorageId,
                snapshotFile,
                cancellationToken);
            await PersistSnapshotIfDueAsync(
                schedule,
                snapshot,
                snapshotWriter,
                stopwatch,
                report,
                currentFile.Name ?? currentFile.Path,
                cancellationToken);

            report.Update(
                counter,
                $"Processing: {currentFile.Name}",
                processedBytes: snapshotFile.Size,
                stage: BackupProgressStage.Preparing);
        }

        private async Task FinalizeSnapshotAsync(
            Schedule schedule,
            Snapshot snapshot,
            SnapshotBatchWriter snapshotWriter,
            LazyLoader<BackupFileInfo> loader,
            ScheduleReport report,
            CancellationToken cancellationToken)
        {
            report.SetStage(BackupProgressStage.Finalizing, "Finalizing snapshot...");
            await FlushUploadedHashesAsync(cancellationToken);
            report.SetEnumeration(loader.Total, true);
            report.Update(
                report.Processed,
                "Finalizing snapshot...",
                stage: BackupProgressStage.Finalizing);
            await snapshotWriter.CompleteAsync(snapshot, schedule, cancellationToken);
        }

        private async Task PersistSnapshotIfDueAsync(
            Schedule schedule,
            Snapshot snapshot,
            SnapshotBatchWriter snapshotWriter,
            Stopwatch stopwatch,
            ScheduleReport report,
            string fileName,
            CancellationToken cancellationToken)
        {
            if (stopwatch.Elapsed < SnapshotFlushInterval)
            {
                return;
            }

            report.SetStage(BackupProgressStage.Persisting, $"Saving: {fileName}");
            await snapshotWriter.FlushAsync(snapshot, schedule, cancellationToken);
            stopwatch.Restart();
        }

        private static bool ShouldIgnoreFile(Schedule schedule, BackupFileInfo file)
        {
            if (schedule.Backup.IgnoredPaths is null)
            {
                return false;
            }

            return ScheduleHelpers.IsPathIgnored(file.Path, file.Name, schedule.Backup.IgnoredPaths);
        }
    }
}
