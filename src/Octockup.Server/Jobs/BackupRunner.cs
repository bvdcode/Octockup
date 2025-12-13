// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using EasyExtensions.Abstractions;
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
using System.IO.Compression;
using System.Security.Cryptography;

namespace Octockup.Server.Jobs
{
    public class BackupRunner
    {
        private readonly IStreamCipher _crypto;
        private readonly AppDbContext _dbContext;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<BackupRunner> _logger;
        private readonly IHubContext<EventHub> _hubContext;
        private readonly IEnumerable<IBackupProvider> _providers;

        private const int ChunkSize = 8 * 1024 * 1024;

        public BackupRunner(
            IStreamCipher crypto,
            AppDbContext dbContext,
            IServiceProvider serviceProvider,
            ILogger<BackupRunner> logger,
            IHubContext<EventHub> hubContext,
            IEnumerable<IBackupProvider> providers)
        {
            _crypto = crypto;
            _dbContext = dbContext;
            _serviceProvider = serviceProvider;
            _logger = logger;
            _hubContext = hubContext;
            _providers = providers;
        }

        public async Task RunAsync(Schedule schedule, CancellationToken cancellationToken)
        {
            Guid userId = schedule.Backup.Source.UserId;
            ScheduleReport report = new(userId, schedule.Id, schedule.BackupId, _hubContext);
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
                await _dbContext.SaveChangesAsync(cancellationToken);

                // Set ignored paths before enumerating files
                sourceProvider.SetIgnoredPaths(schedule.Backup.IgnoredPaths);

                var filesToBackup = sourceProvider.GetFiles(recursive: true, cancellationToken: cancellationToken);
                await BackupAsync(schedule, sourceProvider, storageProvider, report, filesToBackup, cancellationToken);

                schedule.Status = ScheduleStatus.Completed;
                schedule.FinishedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("Schedule {ScheduleId} backup completed successfully", schedule.Id);
                await report.SendAsync(report.Processed, "Backup completed successfully.", status: ScheduleStatus.Completed, cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                schedule.ErrorMessage = $"Backup failed: {ex.Message}";
                schedule.Status = ScheduleStatus.Failed;
                schedule.FinishedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);

                _logger.LogError(ex, "Schedule {ScheduleId} backup failed", schedule.Id);
                await report.SendAsync(report.Processed, schedule.ErrorMessage, status: ScheduleStatus.Failed, cancellationToken: cancellationToken);
            }
            finally
            {
                await report.DisposeAsync();
            }
        }

        private IBackupSource? CreateSourceProvider(Schedule schedule)
        {
            if (_providers.FirstOrDefault(x => x.Id == schedule.Backup.Source.BackupModuleId) is not IBackupSource foundSourceTypeProvider)
            {
                schedule.ErrorMessage = $"Source provider not found: {schedule.Backup.Source.BackupModuleId}";
                schedule.Status = ScheduleStatus.Failed;
                schedule.FinishedAt = DateTime.UtcNow;
                _dbContext.SaveChanges();
                _logger.LogWarning("{msg}", schedule.ErrorMessage);

                return null;
            }

            IBackupSource foundSourceProvider = (IBackupSource)ActivatorUtilities.CreateInstance(_serviceProvider, foundSourceTypeProvider.GetType());
            foundSourceProvider.SetParameters(schedule.Backup.Source.Params(_crypto).Snapshot());
            return foundSourceProvider;
        }

        private IBackupStorage? CreateStorageProvider(Schedule schedule)
        {
            if (_providers.FirstOrDefault(x => x.Id == schedule.Backup.Storage.BackupModuleId) is not IBackupStorage foundStorageTypeProvider)
            {
                schedule.ErrorMessage = $"Storage provider not found: {schedule.Backup.Storage.BackupModuleId}";
                schedule.Status = ScheduleStatus.Failed;
                schedule.FinishedAt = DateTime.UtcNow;
                _dbContext.SaveChanges();
                _logger.LogWarning("{msg}", schedule.ErrorMessage);

                return null;
            }

            IBackupStorage foundStorageProvider = (IBackupStorage)ActivatorUtilities.CreateInstance(_serviceProvider, foundStorageTypeProvider.GetType());
            foundStorageProvider.SetParameters(schedule.Backup.Storage.Params(_crypto).Snapshot());
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
            _logger.LogInformation("Previous snapshot had {Count} files", previousFiles.Count);

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
            await report.SendAsync(counter, $"Processing: {file.Name}", cancellationToken: cancellationToken);

            if (ShouldIgnoreFile(schedule, file))
            {
                _logger.LogInformation("Schedule {ScheduleId}: File {FileName} is ignored by path rules, skipping",
                    schedule.Id, file.Name);
                return counter;
            }

            cancellationToken.ThrowIfCancellationRequested();
            previousFiles.TryGetValue(file.Path, out var previousFile);

            if (previousFile != null && CanReusePreviousFile(previousFile, file, out bool datesMatch))
            {
                await ReusePreviousFileAsync(snapshot, previousFile, file, datesMatch, stopwatch, report, counter, cancellationToken);
                return counter;
            }

            // Diagnostic: why file was not skipped
            if (previousFile != null)
            {
                bool diagnosticDatesMatch = previousFile.LastModified == null || file.LastModified == null ||
                    Math.Abs((previousFile.LastModified.Value - file.LastModified.Value).TotalSeconds) < 2;

                _logger.LogWarning("File {FileName} NOT skipped - foundFile!=null: true, hasHashsum: {HasHashsum}, sizeMatch: " +
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
                _logger.LogInformation("File {FileName} NOT found in previous snapshot", file.Path);
            }

            if (previousFile != null)
            {
                _logger.LogInformation("File {FileName} changed - HasHashsum: {HasHashsum}, Size: {OldSize} vs {NewSize}, LastModified: {OldModified} vs {NewModified} (diff: {DiffSeconds}s)",
                    file.Name, previousFile.Hashsum != null, previousFile.Size, file.Size,
                    previousFile.LastModified?.ToString("yyyy-MM-dd HH:mm:ss"), previousFile.LastModified?.ToString("yyyy-MM-dd HH:mm:ss"),
                    previousFile.LastModified != null && file.LastModified != null
                        ? Math.Abs((previousFile.LastModified.Value - file.LastModified.Value).TotalSeconds)
                        : -1);
            }

            using var stream = await source.GetFileStreamAsync(file, cancellationToken);
            if (stream == Stream.Null)
            {
                _logger.LogWarning("Unable to get stream for file {FileName}, skipping", file.Name);
                return counter;
            }

            using var chunker = new ChunkedStream(stream, ChunkSize);

            cancellationToken.ThrowIfCancellationRequested();
            byte[] buffer = ArrayPool<byte>.Shared.Rent(ChunkSize);
            try
            {
                // File-level incremental hasher
                using var fileHasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

                List<string> chunkHashes = [];
                foreach (Stream chunk in chunker.GetChunks())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    // Compute chunk hash while also updating the file hasher in a single pass
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
                        _logger.LogInformation("Chunk {shortHash} for file {FileName} already uploaded in previous snapshot, skipping upload", shortHash, file.Name);
                        chunkHashes.Add(hash);
                        await report.SendAsync(counter, $"Processing: {file.Name}", processedBytes: chunkLength, cancellationToken: cancellationToken);
                        await chunk.DisposeAsync();
                        continue;
                    }

                    _logger.LogInformation("Processing chunk {shortHash} for file {FileName}", shortHash, file.Path);

                    long storedSize = 0;
                    string path = ScheduleHelpers.SplitHash(hash, storage.PathSeparator);
                    bool exists = await storage.ExistsAsync(path, cancellationToken) ?? false;
                    if (!exists)
                    {
                        string size = $"{(chunkLength / (1024.0 * 1024.0)):F2} MB";
                        _logger.LogInformation("Uploading chunk {shortHash} for file {FileName}, size: {size}", shortHash, file.Name, size);

                        // Compress the chunk (second pass over in-memory chunk stream)
                        chunk.Seek(0, SeekOrigin.Begin);
                        await using var compressed = new MemoryStream();
                        await using (var brotli = new BrotliStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
                        {
                            int r;
                            while ((r = await chunk.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, ChunkSize)), cancellationToken)) > 0)
                            {
                                await brotli.WriteAsync(buffer.AsMemory(0, r), cancellationToken);
                            }
                        }

                        compressed.Seek(0, SeekOrigin.Begin);
                        using var encryptedStream = new MemoryStream();
                        await _crypto.EncryptAsync(compressed, encryptedStream, ct: cancellationToken);
                        encryptedStream.Seek(0, SeekOrigin.Begin);
                        storedSize = encryptedStream.Length;
                        await storage.UploadAsync(path, encryptedStream, cancellationToken);
                        uploadedChunks.Add(hash);
                    }
                    else
                    {
                        _logger.LogInformation("Chunk {shortHash} for file {FileName} already exists, skipping upload", shortHash, file.Name);
                        var storedChunkInfo = await storage.GetFileInfoAsync(path, cancellationToken)
                            ?? throw new Exception($"Failed to get info for existing chunk {shortHash} in storage");
                        _logger.LogInformation("Fetched existing chunk {shortHash} info: Size = {Size}", shortHash, storedChunkInfo.Size);
                        storedSize = storedChunkInfo.Size ?? 0;
                    }

                    bool chunkRecorded = await _dbContext.UploadedHashes.AnyAsync(x => x.Hash == hash, cancellationToken: cancellationToken);
                    if (!chunkRecorded)
                    {
                        var uploadedHash = new UploadedHash
                        {
                            Hash = hash,
                            StoredSize = storedSize,
                            OriginalSize = chunkLength,
                            ModuleId = schedule.Backup.StorageId,
                        };
                        await _dbContext.UploadedHashes.AddAsync(uploadedHash, cancellationToken);
                        await _dbContext.SaveChangesAsync(cancellationToken);
                    }

                    await report.SendAsync(counter, $"Uploading: {file.Name}", processedBytes: chunkLength, cancellationToken: cancellationToken);

                    chunkHashes.Add(hash);
                    cancellationToken.ThrowIfCancellationRequested();

                    await chunk.DisposeAsync();
                }

                // Finalize file hash after all chunks processed
                string fileHash = Convert.ToHexString(fileHasher.GetHashAndReset()).ToLowerInvariant();

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
                await _dbContext.SnapshotFiles.AddAsync(snapshotFile, cancellationToken);
                snapshot.TotalSize += snapshotFile.Size;
                snapshot.FilesCount += 1;

                if (stopwatch.Elapsed.TotalSeconds > 10)
                {
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    stopwatch.Restart();
                }

                _logger.LogInformation("Schedule {ScheduleId}: {Message} ({Processed}/{Total})",
                    schedule.Id, report.Message, report.Processed, report.Total);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }

            return counter;
        }

        private static bool CanReusePreviousFile(SnapshotFile previousFile, BackupFileInfo currentFile, out bool datesMatch)
        {
            datesMatch = previousFile.LastModified == null || currentFile.LastModified == null ||
                Math.Abs((previousFile.LastModified.Value - currentFile.LastModified.Value).TotalSeconds) < 2;

            return previousFile.Hashsum != null && currentFile.Size == previousFile.Size && datesMatch;
        }

        private async Task ReusePreviousFileAsync(
            Snapshot snapshot,
            SnapshotFile previousFile,
            BackupFileInfo currentFile,
            bool datesMatch,
            Stopwatch stopwatch,
            ScheduleReport report,
            int counter,
            CancellationToken cancellationToken)
        {
            _logger.LogInformation("File {FileName} unchanged since last snapshot (size: {Size}, date match: {DateMatch}), reusing metadata",
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
            await _dbContext.SnapshotFiles.AddAsync(snapshotFile, cancellationToken);

            if (stopwatch.Elapsed.TotalSeconds > 10)
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
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
            report.Total = loader.Total;
            report.IsEnumerationCompleted = true;
            await report.SendAsync(report.Processed, "Finalizing snapshot...", cancellationToken: cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            snapshot.CompletedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            snapshot.TotalSize = await _dbContext.SnapshotFiles
                .Where(x => x.SnapshotId == snapshot.Id)
                .SumAsync(x => (long?)x.Size, cancellationToken: cancellationToken) ?? 0L;

            snapshot.FilesCount = await _dbContext.SnapshotFiles
                .Where(x => x.SnapshotId == snapshot.Id)
                .CountAsync(cancellationToken: cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        private async Task<IDictionary<string, SnapshotFile>> GetFilesFromLastSnapshotAsync(Guid backupId, CancellationToken cancellationToken)
        {
            var files = await _dbContext.Snapshots
                .AsNoTracking()
                .Where(x => x.BackupId == backupId)
                .OrderByDescending(x => x.CreatedAt)
                .SelectMany(x => x.Files)
                .ToListAsync(cancellationToken: cancellationToken);
            return files.ToDictionary(x => x.Path, x => x);
        }

        private Task<HashSet<string>> LoadChunkHashesAsync(Guid storageId, CancellationToken cancellationToken)
        {
            return _dbContext.UploadedHashes
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
            await _dbContext.Snapshots.AddAsync(snapshot, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
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
