// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov | bvdcode | belov.us

using EasyExtensions.Abstractions;
using EasyExtensions.Models.Enums;
using EasyExtensions.Streams;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Octockup.Server.Abstractions;
using Octockup.Server.Collections;
using Octockup.Server.Database;
using Octockup.Server.Helpers;
using Octockup.Server.Hubs;
using Octockup.Server.Models;
using Octockup.Server.Models.Enums;
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
        IHubContext<EventHub> hubContext,
        IEnumerable<IBackupProvider> providers)
    {
        private const int ChunkSize = 8 * 1024 * 1024;
        private readonly List<UploadedHash> _pendingUploadedHashes = [];
        private readonly Stopwatch _uploadedHashesStopwatch = Stopwatch.StartNew();
        private const int UploadedHashesFlushCount = 500; // flush every 500 new hashes
        private static readonly TimeSpan UploadedHashesFlushInterval = TimeSpan.FromSeconds(5);

        public async Task RunAsync(Schedule schedule, CancellationToken cancellationToken)
        {
            Guid userId = schedule.Backup.Source.UserId;
            ScheduleReport report = new(userId, schedule.Id, schedule.BackupId, hubContext);
            report.StartBackgroundReporting(cancellationToken);

            try
            {
                var sourceProvider = CreateSourceProvider(schedule);
                if (sourceProvider is null)
                {
                    await report.SendAsync(0, schedule.ErrorMessage ?? "Source provider not found.", cancellationToken: cancellationToken);
                    return;
                }

                var storageProvider = CreateStorageProvider(schedule);
                if (storageProvider is null)
                {
                    await report.SendAsync(0, schedule.ErrorMessage ?? "Storage provider not found.", cancellationToken: cancellationToken);
                    return;
                }

                await report.SendAsync(0, "Listing files to backup...", cancellationToken: cancellationToken);
                schedule.Status = ScheduleStatus.Running;
                await dbContext.SaveChangesAsync(cancellationToken);

                // Set ignored paths before enumerating files
                sourceProvider.SetIgnoredPaths(schedule.Backup.IgnoredPaths);

                var filesToBackup = sourceProvider.GetFiles(recursive: true, cancellationToken: cancellationToken);
                await BackupAsync(schedule, sourceProvider, storageProvider, report, filesToBackup, cancellationToken);

                schedule.Status = ScheduleStatus.Completed;
                schedule.FinishedAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);

                logger.LogInformation("Schedule {ScheduleId} backup completed successfully", schedule.Id);
                await report.SendAsync(report.Processed, "Backup completed successfully.", status: ScheduleStatus.Completed, cancellationToken: cancellationToken);
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
                    await dbContext.SaveChangesAsync(CancellationToken.None);

                    logger.LogError(ex, "Schedule {ScheduleId} backup interrupted unexpectedly", schedule.Id);
                    await report.SendAsync(report.Processed, schedule.ErrorMessage, status: ScheduleStatus.Failed, cancellationToken: CancellationToken.None);
                }
                return;
            }
            catch (Exception ex)
            {
                schedule.ErrorMessage = $"Backup failed: {ex.Message}";
                schedule.Status = ScheduleStatus.Failed;
                schedule.FinishedAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(CancellationToken.None);

                logger.LogError(ex, "Schedule {ScheduleId} backup failed", schedule.Id);
                await report.SendAsync(report.Processed, schedule.ErrorMessage, status: ScheduleStatus.Failed, cancellationToken: CancellationToken.None);
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

                await report.DisposeAsync();
            }
        }

        private IBackupSource? CreateSourceProvider(Schedule schedule)
        {
            if (providers.FirstOrDefault(x => x.Id == schedule.Backup.Source.BackupModuleId) is not IBackupSource foundSourceTypeProvider)
            {
                schedule.ErrorMessage = $"Source provider not found: {schedule.Backup.Source.BackupModuleId}";
                schedule.Status = ScheduleStatus.Failed;
                schedule.FinishedAt = DateTime.UtcNow;
                dbContext.SaveChanges();
                logger.LogWarning("{msg}", schedule.ErrorMessage);

                return null;
            }

            IBackupSource foundSourceProvider = (IBackupSource)ActivatorUtilities.CreateInstance(serviceProvider, foundSourceTypeProvider.GetType());
            foundSourceProvider.SetParameters(schedule.Backup.Source.Params(crypto).Snapshot());
            return foundSourceProvider;
        }

        private IBackupStorage? CreateStorageProvider(Schedule schedule)
        {
            if (providers.FirstOrDefault(x => x.Id == schedule.Backup.Storage.BackupModuleId) is not IBackupStorage foundStorageTypeProvider)
            {
                schedule.ErrorMessage = $"Storage provider not found: {schedule.Backup.Storage.BackupModuleId}";
                schedule.Status = ScheduleStatus.Failed;
                schedule.FinishedAt = DateTime.UtcNow;
                dbContext.SaveChanges();
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
            IEnumerable<BackupFileInfo> lazyFiles,
            CancellationToken cancellationToken)
        {
            Snapshot snapshot = await CreateNewSnapshotWithTracking(schedule.BackupId, cancellationToken);
            using LazyLoader<BackupFileInfo> loader = new(lazyFiles);
            HashSet<string> uploadedChunks = await LoadChunkHashesAsync(schedule.Backup.StorageId, cancellationToken);
            var previousFiles = await GetFilesFromLastSnapshotAsync(schedule.BackupId, cancellationToken);
            logger.LogInformation("Previous snapshot had {Count} files", previousFiles.Count);

            int counter = 0;
            Stopwatch stopwatch = Stopwatch.StartNew();

            cancellationToken.ThrowIfCancellationRequested();
            foreach (var file in loader)
            {
                cancellationToken.ThrowIfCancellationRequested();
                counter = await ProcessFileAsync(
                    schedule,
                    snapshot,
                    source,
                    storage,
                    report,
                    loader,
                    uploadedChunks,
                    previousFiles,
                    stopwatch,
                    counter,
                    file,
                    cancellationToken);
            }

            await FinalizeSnapshotAsync(snapshot, loader, report, cancellationToken);
        }

        private async Task<int> ProcessFileAsync(
            Schedule schedule,
            Snapshot snapshot,
            IBackupSource source,
            IBackupStorage storage,
            ScheduleReport report,
            LazyLoader<BackupFileInfo> loader,
            HashSet<string> uploadedChunks,
            IDictionary<string, SnapshotFile> previousFiles,
            Stopwatch stopwatch,
            int counter,
            BackupFileInfo file,
            CancellationToken cancellationToken)
        {
            counter++;
            report.Total = loader.Total;
            report.IsEnumerationCompleted = loader.IsEnumerationCompleted;
            report.CurrentFile = file.Name ?? Path.GetFileName(file.Path);
            report.CurrentPath = file.Path;
            await report.SendAsync(counter, $"Processing: {file.Name}", cancellationToken: cancellationToken);

            if (ShouldIgnoreFile(schedule, file))
            {
                logger.LogInformation("Schedule {ScheduleId}: File {FileName} is ignored by path rules, skipping",
                    schedule.Id, file.Name);
                return counter;
            }

            cancellationToken.ThrowIfCancellationRequested();
            previousFiles.TryGetValue(file.Path, out var previousFile);

            if (previousFile != null && CanReusePreviousFile(previousFile, file, out bool datesMatch))
            {
                await ReusePreviousFileAsync(schedule, snapshot, previousFile, file, datesMatch, stopwatch, report, counter, cancellationToken);
                return counter;
            }

            // Diagnostic: why file was not skipped
            if (previousFile != null)
            {
                bool diagnosticDatesMatch = previousFile.LastModified == null || file.LastModified == null ||
                    Math.Abs((previousFile.LastModified.Value - file.LastModified.Value).TotalSeconds) < 2;

                logger.LogWarning("File {FileName} NOT skipped - foundFile!=null: true, hasHashsum: {HasHashsum}, sizeMatch: " +
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
            else
            {
                logger.LogInformation("File {FileName} NOT found in previous snapshot", file.Path);
            }

            if (previousFile != null)
            {
                logger.LogInformation("File {FileName} changed - HasHashsum: {HasHashsum}, Size: {OldSize} vs {NewSize}, LastModified: {OldModified} vs {NewModified} (diff: {DiffSeconds}s)",
                    file.Name, previousFile.Hashsum != null, previousFile.Size, file.Size,
                    previousFile.LastModified?.ToString("yyyy-MM-dd HH:mm:ss"), file.LastModified?.ToString("yyyy-MM-dd HH:mm:ss"),
                    previousFile.LastModified != null && file.LastModified != null
                        ? Math.Abs((previousFile.LastModified.Value - file.LastModified.Value).TotalSeconds)
                        : -1);
            }

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
                uploadedChunks,
                stream,
                counter,
                cancellationToken);

            SnapshotFile snapshotFile = new()
            {
                Path = file.Path,
                Hashsum = fileHash,
                Snapshot = snapshot,
                Size = file.Size ?? 0,
                SnapshotId = snapshot.Id,
                ChunkHashes = chunkHashes,
                Name = file.Name ?? file.Path,
                LastModified = file.LastModified,
            };
            await dbContext.SnapshotFiles.AddAsync(snapshotFile, cancellationToken);
            snapshot.TotalSize += snapshotFile.Size;
            snapshot.FilesCount += 1;

            if (stopwatch.Elapsed.TotalSeconds > 10)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                // Clear change tracker to release memory from tracked entities
                dbContext.ChangeTracker.Clear();
                // Re-attach entities to continue tracking them
                dbContext.Attach(snapshot);
                dbContext.Attach(schedule);
                stopwatch.Restart();
            }

            logger.LogInformation("Schedule {ScheduleId}: {Message} ({Processed}/{Total})",
                schedule.Id, report.Message, report.Processed, report.Total);

            return counter;
        }

        private async Task<(string FileHash, List<string> ChunkHashes)> ProcessChunksAsync(
            Schedule schedule,
            BackupFileInfo file,
            IBackupStorage storage,
            ScheduleReport report,
            HashSet<string> uploadedChunks,
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

                    string hash = Convert.ToHexString(chunkHasher.GetHashAndReset()).ToLowerInvariant();
                    string shortHash = hash[^8..];

                    var alreadyUploaded = uploadedChunks.Contains(hash);
                    if (alreadyUploaded)
                    {
                        logger.LogInformation("Chunk {shortHash} for file {FileName} already uploaded in previous snapshot, skipping upload", shortHash, file.Name);
                        chunkHashes.Add(hash);
                        await report.SendAsync(counter, $"Processing: {file.Name}", processedBytes: chunkLength, cancellationToken: cancellationToken);
                        await chunk.DisposeAsync();
                        continue;
                    }

                    logger.LogInformation("Processing chunk {shortHash} for file {FileName}", shortHash, file.Path);

                    long storedSize = 0;
                    string path = ScheduleHelpers.SplitPlainHash(hash, storage.PathSeparator);
                    CompressionAlgorithm algorithm;
                    string size = $"{(chunkLength / (1024.0 * 1024.0)):F2} MB";
                    logger.LogInformation("Uploading chunk {shortHash} for file {FileName}, size: {size}", shortHash, file.Name, size);

                    chunk.Seek(0, SeekOrigin.Begin);

                    Stream src;
                    MemoryStream? compressedStream = null;
                    bool needsCompression = CompressionHelpers.ShouldCompressChunk(file.Name ?? file.Path, chunkLength);
                    if (needsCompression)
                    {
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
                        if (compressedStream.Length >= chunkLength)
                        {
                            // Compression did not reduce size enough, use original
                            logger.LogInformation("Chunk {shortHash} for file {FileName} compression did not reduce size enough ({originalSize} to {compressedSize}), using original",
                                shortHash, file.Name, size,
                                $"{(compressedStream.Length / (1024.0 * 1024.0)):F2} MB");
                            await compressedStream.DisposeAsync();
                            compressedStream = null;
                            chunk.Seek(0, SeekOrigin.Begin);
                            src = chunk;
                            algorithm = CompressionAlgorithm.None;
                        }
                        else
                        {
                            src = compressedStream;
                            algorithm = CompressionHelpers.Algorithm;
                            logger.LogInformation("Chunk {shortHash} for file {FileName} compressed from {originalSize} to {compressedSize} using {algorithm}",
                                shortHash, file.Name, size,
                                $"{(src.Length / (1024.0 * 1024.0)):F2} MB",
                                algorithm);
                        }
                    }
                    else
                    {
                        algorithm = CompressionAlgorithm.None;
                        src = chunk;
                        logger.LogInformation("Chunk {shortHash} for file {FileName} is not compressed due to insufficient size reduction", shortHash, file.Name);
                    }

                    try
                    {
                        using var encryptedStream = new MemoryStream();
                        if (src.CanSeek)
                        {
                            src.Seek(0, SeekOrigin.Begin);
                        }
                        await crypto.EncryptAsync(src, encryptedStream, ct: cancellationToken);
                        encryptedStream.Seek(0, SeekOrigin.Begin);
                        storedSize = encryptedStream.Length;
                        await storage.UploadAsync(path, encryptedStream, cancellationToken);
                        uploadedChunks.Add(hash);

                        await EnsureUploadedHashRecordedAsync(schedule.Backup.StorageId, hash, storedSize, chunkLength, algorithm, cancellationToken);

                        await report.SendAsync(counter, $"Uploading: {file.Name}", processedBytes: chunkLength, cancellationToken: cancellationToken);

                        chunkHashes.Add(hash);
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

        private async Task EnsureUploadedHashRecordedAsync(
            Guid storageModuleId,
            string hash,
            long storedSize,
            long originalSize,
            CompressionAlgorithm algorithm,
            CancellationToken cancellationToken)
        {
            bool chunkRecorded = await dbContext.UploadedHashes
                .AsNoTracking()
                .AnyAsync(x => x.Hash == hash && x.ModuleId == storageModuleId, cancellationToken);
            if (chunkRecorded)
            {
                return;
            }

            var uploadedHash = new UploadedHash
            {
                Hash = hash,
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

            await dbContext.UploadedHashes.AddRangeAsync(_pendingUploadedHashes, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            _pendingUploadedHashes.Clear();
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
            await dbContext.SaveChangesAsync(CancellationToken.None);

            await report.SendAsync(
                report.Processed,
                "Backup canceled.",
                status: ScheduleStatus.Failed,
                cancellationToken: CancellationToken.None);
        }

        private async Task ReusePreviousFileAsync(
            Schedule schedule,
            Snapshot snapshot,
            SnapshotFile previousFile,
            BackupFileInfo currentFile,
            bool datesMatch,
            Stopwatch stopwatch,
            ScheduleReport report,
            int counter,
            CancellationToken cancellationToken)
        {
            logger.LogInformation("File {FileName} unchanged since last snapshot (size: {Size}, date match: {DateMatch}), reusing metadata",
                currentFile.Name, currentFile.Size, datesMatch);

            SnapshotFile snapshotFile = new()
            {
                Path = currentFile.Path,
                Snapshot = snapshot,
                Size = currentFile.Size ?? 0,
                SnapshotId = snapshot.Id,
                Hashsum = previousFile.Hashsum,
                Name = currentFile.Name ?? currentFile.Path,
                LastModified = currentFile.LastModified,
                ChunkHashes = previousFile.ChunkHashes,
            };
            await dbContext.SnapshotFiles.AddAsync(snapshotFile, cancellationToken);
            snapshot.TotalSize += snapshotFile.Size;
            snapshot.FilesCount += 1;

            if (stopwatch.Elapsed.TotalSeconds > 10)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                // Clear change tracker to release memory from tracked entities
                dbContext.ChangeTracker.Clear();
                // Re-attach entities to continue tracking them
                dbContext.Attach(snapshot);
                dbContext.Attach(schedule);
                stopwatch.Restart();
            }

            await report.SendAsync(counter, $"Processing: {currentFile.Name}", processedBytes: snapshotFile.Size, cancellationToken: cancellationToken);
        }

        private async Task FinalizeSnapshotAsync(
            Snapshot snapshot,
            LazyLoader<BackupFileInfo> loader,
            ScheduleReport report,
            CancellationToken cancellationToken)
        {
            await FlushUploadedHashesAsync(cancellationToken);
            report.Total = loader.Total;
            report.IsEnumerationCompleted = true;
            await report.SendAsync(report.Processed, "Finalizing snapshot...", cancellationToken: cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            snapshot.CompletedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);

            snapshot.TotalSize = await dbContext.SnapshotFiles
                .Where(x => x.SnapshotId == snapshot.Id)
                .SumAsync(x => (long?)x.Size, cancellationToken: cancellationToken) ?? 0L;

            snapshot.FilesCount = await dbContext.SnapshotFiles
                .Where(x => x.SnapshotId == snapshot.Id)
                .CountAsync(cancellationToken: cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        private async Task<IDictionary<string, SnapshotFile>> GetFilesFromLastSnapshotAsync(Guid backupId, CancellationToken cancellationToken)
        {
            const int maxFilesToFetch = 1_000_000;

            var files = await dbContext.Snapshots
                .AsNoTracking()
                .Where(x => x.BackupId == backupId)
                .OrderByDescending(x => x.CreatedAt)
                .SelectMany(x => x.Files)
                .Take(maxFilesToFetch)
                .ToListAsync(cancellationToken: cancellationToken);

            return files.DistinctBy(x => x.Path).ToDictionary(x => x.Path, x => x);
        }

        private Task<HashSet<string>> LoadChunkHashesAsync(Guid storageId, CancellationToken cancellationToken)
        {
            return dbContext.UploadedHashes
                .AsNoTracking()
                .Where(x => x.ModuleId == storageId)
                .Select(x => x.Hash)
                .ToHashSetAsync(cancellationToken: cancellationToken);
        }

        private async Task<Snapshot> CreateNewSnapshotWithTracking(Guid backupId, CancellationToken cancellationToken)
        {
            Snapshot snapshot = new()
            {
                BackupId = backupId,
            };
            await dbContext.Snapshots.AddAsync(snapshot, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return snapshot;
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
