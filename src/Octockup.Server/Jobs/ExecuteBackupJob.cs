// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using EasyExtensions.Abstractions;
using EasyExtensions.Quartz.Attributes;
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
using Quartz;
using System.Buffers;
using System.IO.Compression;
using System.Security.Cryptography;

namespace Octockup.Server.Jobs
{
    [JobTrigger(minutes: 1)]
    public class ExecuteBackupJob(
        IStreamCipher _crypto,
        AppDbContext _dbContext,
        IServiceProvider _serviceProvider,
        ILogger<ExecuteBackupJob> _logger,
        IHubContext<EventHub> _hubContext,
        IEnumerable<IBackupProvider> _providers) : IJob
    {
        public static void StopRunningBackup(Guid scheduleId)
        {
            _stoppingSchedules.Add(scheduleId);
        }

        private static readonly List<Guid> _stoppingSchedules = [];
        private const int ChunkSize = 8 * 1024 * 1024;

        public async Task Execute(IJobExecutionContext context)
        {
            Schedule? next = await ScheduleHelpers.GetNextScheduleAsync(_dbContext.Schedules);
            if (next == null)
            {
                return;
            }
            Guid userId = next.Backup.Source.UserId;
            ScheduleReport report = new(userId, next.Id, next.BackupId, _hubContext);
            report.StartBackgroundReporting();

            try
            {
                if (_providers.FirstOrDefault(x => x.Id == next.Backup.Source.BackupModuleId) is not IBackupSource foundSourceTypeProvider)
                {
                    next.ErrorMessage = $"Source provider not found: {next.Backup.Source.BackupModuleId}";
                    next.Status = ScheduleStatus.Failed;
                    next.FinishedAt = DateTime.UtcNow;
                    await _dbContext.SaveChangesAsync();
                    _logger.LogWarning("{msg}", next.ErrorMessage);
                    await report.SendAsync(0, next.ErrorMessage);
                    return;
                }
                IBackupSource foundSourceProvider = (IBackupSource)ActivatorUtilities.CreateInstance(_serviceProvider, foundSourceTypeProvider.GetType());
                foundSourceProvider.SetParameters(next.Backup.Source.Parameters);

                if (_providers.FirstOrDefault(x => x.Id == next.Backup.Storage.BackupModuleId) is not IBackupStorage foundStorageTypeProvider)
                {
                    next.ErrorMessage = $"Storage provider not found: {next.Backup.Storage.BackupModuleId}";
                    next.Status = ScheduleStatus.Failed;
                    next.FinishedAt = DateTime.UtcNow;
                    await _dbContext.SaveChangesAsync();
                    _logger.LogWarning("{msg}", next.ErrorMessage);
                    await report.SendAsync(0, next.ErrorMessage);
                    return;
                }
                IBackupStorage foundStorageProvider = (IBackupStorage)ActivatorUtilities.CreateInstance(_serviceProvider, foundStorageTypeProvider.GetType());
                foundStorageProvider.SetParameters(next.Backup.Storage.Parameters);

                await report.SendAsync(0, "Listing files to backup...");
                next.Status = ScheduleStatus.Running;
                await _dbContext.SaveChangesAsync();

                // Set ignored paths before enumerating files
                foundSourceProvider.SetIgnoredPaths(next.Backup.IgnoredPaths);

                var filesToBackup = foundSourceProvider.GetFiles(recursive: true);
                await BackupAsync(next, foundSourceProvider, foundStorageProvider, report, filesToBackup, context.CancellationToken);
                next.Status = ScheduleStatus.Completed;
                next.FinishedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
                _logger.LogInformation("Schedule {ScheduleId} backup completed successfully", next.Id);
                await report.SendAsync(report.Processed, "Backup completed successfully.", status: ScheduleStatus.Completed);
            }
            catch (Exception ex)
            {
                next.ErrorMessage = $"Backup failed: {ex.Message}";
                next.Status = ScheduleStatus.Failed;
                next.FinishedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
                _logger.LogError(ex, "Schedule {ScheduleId} backup failed", next.Id);
                await report.SendAsync(report.Processed, next.ErrorMessage, status: ScheduleStatus.Failed);
            }
            finally
            {
                await report.DisposeAsync();
            }
        }

        private async Task BackupAsync(
            Schedule schedule,
            IBackupSource source,
            IBackupStorage storage,
            ScheduleReport report,
            IEnumerable<BackupFileInfo> lazyFiles,
            CancellationToken cancellationToken)
        {
            Snapshot snapshot = new()
            {
                BackupId = schedule.BackupId,
            };

            await _dbContext.Snapshots.AddAsync(snapshot, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            using LazyLoader<BackupFileInfo> loader = new(lazyFiles);
            var uploadedChunks = (await _dbContext.SnapshotFiles
                    .AsNoTracking()
                    .Where(x => x.Snapshot.BackupId == schedule.BackupId)
                    .Select(x => x.ChunkHashes)
                    .ToListAsync(cancellationToken: cancellationToken))
                .Where(list => list != null)
                .SelectMany(list => list)
                .Distinct()
                .ToHashSet();

            // Cache all previous snapshot files by path (take the most recent one if duplicates exist)
            var previousFiles = (await _dbContext.SnapshotFiles
                .AsNoTracking()
                .Where(x => x.Snapshot.BackupId == schedule.BackupId)
                .ToListAsync(cancellationToken: cancellationToken))
                .GroupBy(x => x.Path)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(f => f.LastModified).First());

            int counter = 0;
            CheckIfStopped(schedule.Id, cancellationToken);
            foreach (var file in loader)
            {
                CheckIfStopped(schedule.Id, cancellationToken);
                counter++;
                report.Total = loader.Total;
                await report.SendAsync(counter, $"Processing: {file.Name}");
                if (schedule.Backup.IgnoredPaths != null && ScheduleHelpers.IsPathIgnored(file.Path, file.Name, schedule.Backup.IgnoredPaths))
                {
                    _logger.LogInformation("Schedule {ScheduleId}: File {FileName} is ignored by path rules, skipping",
                        schedule.Id, file.Name);
                    continue;
                }

                CheckIfStopped(schedule.Id, cancellationToken);
                previousFiles.TryGetValue(file.Path, out var foundFile);
                if (foundFile != null && foundFile.Hashsum != null && file.Size == foundFile.Size && file.LastModified == foundFile.LastModified)
                {
                    _logger.LogInformation("Schedule {ScheduleId}: File {FileName} unchanged since last snapshot, skipping",
                        schedule.Id, file.Name);
                    SnapshotFile snapshotFile = new()
                    {
                        Path = file.Path,
                        Hashsum = foundFile.Hashsum,
                        Snapshot = snapshot,
                        Size = file.Size ?? 0,
                        SnapshotId = snapshot.Id,
                        ChunkHashes = foundFile.ChunkHashes,
                        Name = file.Name ?? file.Path,
                        LastModified = file.LastModified,
                    };
                    await _dbContext.SnapshotFiles.AddAsync(snapshotFile, cancellationToken);
                    await _dbContext.SaveChangesAsync(cancellationToken);

                    await report.SendAsync(counter, $"Processing: {file.Name}", processedBytes: snapshotFile.Size);
                    continue;
                }

                using var stream = await source.GetFileStreamAsync(file);
                if (stream == Stream.Null)
                {
                    _logger.LogWarning("Schedule {ScheduleId}: Unable to get stream for file {FileName}, skipping",
                        schedule.Id, file.Name);
                    continue;
                }
                using var chunker = new ChunkedStream(stream, ChunkSize);

                CheckIfStopped(schedule.Id, cancellationToken);
                byte[] buffer = ArrayPool<byte>.Shared.Rent(ChunkSize);
                try
                {
                    // File-level incremental hasher
                    using var fileHasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

                    List<string> chunkHashes = [];
                    foreach (Stream chunk in chunker.GetChunks())
                    {
                        CheckIfStopped(schedule.Id, cancellationToken);

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
                            await report.SendAsync(counter, $"Processing: {file.Name}", processedBytes: chunkLength);
                            await chunk.DisposeAsync();
                            continue;
                        }

                        string path = ScheduleHelpers.SplitHash(hash, storage.PathSeparator);
                        bool exists = await storage.ExistsAsync(path) ?? false;
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

                            // Encrypt and upload the chunk
                            long storedSize;
                            using var encryptedStream = new MemoryStream();
                            await _crypto.EncryptAsync(compressed, encryptedStream, ct: cancellationToken);
                            encryptedStream.Seek(0, SeekOrigin.Begin);
                            storedSize = encryptedStream.Length;
                            await storage.UploadAsync(path, encryptedStream);
                            uploadedChunks.Add(hash);

                            bool chunkRecorded = await _dbContext.UploadedHashes.AnyAsync(x => x.Hash == hash, cancellationToken: cancellationToken);
                            if (!chunkRecorded)
                            {
                                var uploadedHash = new UploadedHash
                                {
                                    Hash = hash,
                                    OriginalSize = chunkLength,
                                    StoredSize = storedSize,
                                    ModuleId = schedule.Backup.StorageId,
                                };
                                await _dbContext.UploadedHashes.AddAsync(uploadedHash, cancellationToken);
                                await _dbContext.SaveChangesAsync(cancellationToken);
                            }
                        }
                        else
                        {
                            _logger.LogInformation("Chunk {shortHash} for file {FileName} already exists, skipping upload", shortHash, file.Name);
                        }

                        await report.SendAsync(counter, $"Uploading: {file.Name}", processedBytes: chunkLength);

                        chunkHashes.Add(hash);
                        if (_stoppingSchedules.Contains(schedule.Id))
                        {
                            _stoppingSchedules.Remove(schedule.Id);
                            throw new OperationCanceledException("Backup stopped by user request.");
                        }

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
                    await _dbContext.SaveChangesAsync(cancellationToken);

                    _logger.LogInformation("Schedule {ScheduleId}: {Message} ({Processed}/{Total})",
                        schedule.Id, report.Message, report.Processed, report.Total);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }

            report.Total = loader.Total;
            await report.SendAsync(report.Processed, "Finalizing snapshot...");
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

        private static void CheckIfStopped(Guid scheduleId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool removed = _stoppingSchedules.Remove(scheduleId);
            if (removed)
            {
                throw new OperationCanceledException("Backup stopped by user request.");
            }
        }
    }
}
