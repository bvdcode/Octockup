// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using Quartz;
using System.Buffers;
using EasyExtensions;
using Octockup.Server.Hubs;
using System.IO.Compression;
using EasyExtensions.Streams;
using Octockup.Server.Models;
using Octockup.Server.Helpers;
using Octockup.Server.Database;
using EasyExtensions.Abstractions;
using Octockup.Server.Models.Enums;
using Microsoft.AspNetCore.SignalR;
using Octockup.Server.Abstractions;
using Microsoft.EntityFrameworkCore;
using EasyExtensions.Quartz.Attributes;
using System.Security.Cryptography;

namespace Octockup.Server.Jobs
{
    [JobTrigger(minutes: 1)]
    public class ExecuteBackupJob(
        IStreamCipher _crypto,
        AppDbContext _dbContext,
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
            ScheduleReport report = new(userId, next.Id, _hubContext);

            if (_providers.FirstOrDefault(x => x.Id == next.Backup.Source.BackupModuleId) is not IBackupSource foundSourceProvider)
            {
                next.ErrorMessage = $"Source provider not found: {next.Backup.Source.BackupModuleId}";
                next.Status = ScheduleStatus.Failed;
                next.FinishedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
                _logger.LogWarning("{msg}", next.ErrorMessage);
                await report.SendAsync(0, next.ErrorMessage);
                return;
            }
            foundSourceProvider.SetParameters(next.Backup.Source.Parameters);

            if (_providers.FirstOrDefault(x => x.Id == next.Backup.Storage.BackupModuleId) is not IBackupStorage foundStorageProvider)
            {
                next.ErrorMessage = $"Storage provider not found: {next.Backup.Storage.BackupModuleId}";
                next.Status = ScheduleStatus.Failed;
                next.FinishedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
                _logger.LogWarning("{msg}", next.ErrorMessage);
                await report.SendAsync(0, next.ErrorMessage);
                return;
            }
            foundStorageProvider.SetParameters(next.Backup.Storage.Parameters);

            await report.SendAsync(0, "Listing files to backup...");
            next.Status = ScheduleStatus.Running;
            await _dbContext.SaveChangesAsync();

            try
            {
                var filesToBackup = foundSourceProvider.GetFiles(recursive: true).ToList();
                report.Total = filesToBackup.Count;
                await BackupAsync(next, foundSourceProvider, foundStorageProvider, report, filesToBackup);
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
        }

        private async Task BackupAsync(
            Schedule schedule,
            IBackupSource source,
            IBackupStorage storage,
            ScheduleReport report,
            List<BackupFileInfo> files)
        {
            Snapshot snapshot = new()
            {
                BackupId = schedule.BackupId,
            };

            await _dbContext.Snapshots.AddAsync(snapshot);
            await _dbContext.SaveChangesAsync();

            long processedBytes = 0;
            for (int i = 0; i < files.Count; i++)
            {
                var file = files[i];
                await report.SendAsync(i, $"Processing: {file.Name}", processedBytes: processedBytes);

                using var stream = await source.GetFileStreamAsync(file);
                if (stream == Stream.Null)
                {
                    _logger.LogWarning("Schedule {ScheduleId}: Unable to get stream for file {FileName}, skipping",
                        schedule.Id, file.Name);
                    continue;
                }
                using var chunker = new ChunkedStream(stream, ChunkSize);

                byte[] buffer = ArrayPool<byte>.Shared.Rent(ChunkSize);
                try
                {
                    // File-level incremental hasher
                    using var fileHasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

                    List<string> chunkHashes = [];
                    foreach (Stream chunk in chunker.GetChunks())
                    {
                        if (_stoppingSchedules.Contains(schedule.Id))
                        {
                            _stoppingSchedules.Remove(schedule.Id);
                            throw new OperationCanceledException("Backup stopped by user request.");
                        }

                        // Compute chunk hash while also updating the file hasher in a single pass
                        chunk.Seek(0, SeekOrigin.Begin);
                        using var chunkHasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                        int read;
                        long chunkLength = 0L;
                        while ((read = await chunk.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, ChunkSize)))) > 0)
                        {
                            chunkHasher.AppendData(buffer, 0, read);
                            fileHasher.AppendData(buffer, 0, read);
                            chunkLength += read;
                        }
                        string hash = Convert.ToHexString(chunkHasher.GetHashAndReset()).ToLowerInvariant();

                        var alreadyUploaded = await _dbContext.SnapshotFiles
                            .AsNoTracking()
                            .Where(x => x.Snapshot.BackupId == schedule.BackupId)
                            .AnyAsync(x => x.ChunkHashes.Contains(hash));

                        if (alreadyUploaded)
                        {
                            _logger.LogInformation("Schedule {ScheduleId}: Chunk {ChunkHash} for file {FileName} already uploaded in previous snapshot, skipping upload",
                                schedule.Id, hash, file.Name);
                            chunkHashes.Add(hash);
                            processedBytes += chunkLength;
                            await report.SendAsync(i, $"Uploading: {file.Name}", processedBytes: processedBytes);
                            await chunk.DisposeAsync();
                            continue;
                        }

                        // Compress the chunk (second pass over in-memory chunk stream)
                        chunk.Seek(0, SeekOrigin.Begin);
                        await using var compressed = new MemoryStream();
                        await using (var brotli = new BrotliStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
                        {
                            int r;
                            while ((r = await chunk.ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, ChunkSize)))) > 0)
                            {
                                await brotli.WriteAsync(buffer.AsMemory(0, r));
                            }
                        }
                        compressed.Seek(0, SeekOrigin.Begin);

                        using var encryptedStream = new MemoryStream();
                        await _crypto.EncryptAsync(compressed, encryptedStream);
                        string path = ScheduleHelpers.SplitHash(hash, storage.PathSeparator);
                        bool? exists = await storage.ExistsAsync(path);
                        if (exists.HasValue && exists.Value == false)
                        {
                            _logger.LogInformation("Schedule {ScheduleId}: Uploading chunk {ChunkHash} for file {FileName}",
                                schedule.Id, hash, file.Name);
                            await storage.UploadAsync(path, encryptedStream);
                        }
                        else if (exists.HasValue && exists.Value == true)
                        {
                            _logger.LogInformation("Schedule {ScheduleId}: Chunk {ChunkHash} for file {FileName} already exists, skipping upload",
                                schedule.Id, hash, file.Name);
                        }

                        processedBytes += chunkLength;
                        await report.SendAsync(i, $"Uploading: {file.Name}", processedBytes: processedBytes);

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
                    await _dbContext.SnapshotFiles.AddAsync(snapshotFile);
                    await _dbContext.SaveChangesAsync();

                    snapshot.CompletedAt = DateTime.UtcNow;
                    await _dbContext.SaveChangesAsync();
                    _logger.LogInformation("Schedule {ScheduleId}: {Message} ({Processed}/{Total})",
                        schedule.Id, report.Message, report.Processed, report.Total);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
        }
    }
}
