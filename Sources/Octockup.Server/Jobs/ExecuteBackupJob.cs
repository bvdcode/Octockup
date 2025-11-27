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
        private const int ChunkSize = 4 * 1024 * 1024;

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
                next.Status = BackupStatus.Failed;
                next.FinishedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
                _logger.LogWarning("{msg}", next.ErrorMessage);
                await report.SendAsync(0, next.ErrorMessage);
                return;
            }
            if (_providers.FirstOrDefault(x => x.Id == next.Backup.Storage.BackupModuleId) is not IBackupStorage foundStorageProvider)
            {
                next.ErrorMessage = $"Storage provider not found: {next.Backup.Storage.BackupModuleId}";
                next.Status = BackupStatus.Failed;
                next.FinishedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
                _logger.LogWarning("{msg}", next.ErrorMessage);
                await report.SendAsync(0, next.ErrorMessage);
                return;
            }

            await report.SendAsync(0, "Listing files to backup...");
            next.Status = BackupStatus.Running;
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
                next.Status = BackupStatus.Failed;
                next.FinishedAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync();
                _logger.LogError(ex, "Schedule {ScheduleId} backup failed", next.Id);
                await report.SendAsync(report.Processed, next.ErrorMessage, status: BackupStatus.Failed);
            }
        }

        private async Task BackupAsync(
            Schedule schedule,
            IBackupSource source,
            IBackupStorage storage,
            ScheduleReport report,
            List<BackupFileInfo> files)
        {
            long processedBytes = 0;
            for (int i = 0; i < files.Count; i++)
            {
                var file = files[i];
                await report.SendAsync(i, $"Processing: {file.Name}", processedBytes: processedBytes);
                
                using var stream = await source.GetFileStreamAsync(file);
                using var chunker = new ChunkedStream(stream, ChunkSize);

                byte[] buffer = ArrayPool<byte>.Shared.Rent(ChunkSize);
                List<string> chunkHashes = [];
                foreach (Stream chunk in chunker.GetChunks())
                {
                    int bytesRead = await chunk.ReadAsync(buffer.AsMemory(0, ChunkSize));
                    if (bytesRead <= 0)
                    {
                        break;
                    }
                    string hash = chunk.Sha256();
                    chunk.Seek(default, SeekOrigin.Begin);
                    using var compressedStream = new BrotliStream(chunk, CompressionLevel.Fastest);
                    using var encryptedStream = await _crypto.EncryptAsync(compressedStream);
                    string path = ScheduleHelpers.SplitHash(hash, storage.PathSeparator);
                    await storage.UploadAsync(path, encryptedStream);
                    processedBytes += bytesRead;
                    await report.SendAsync(i, $"Uploading chunk: {file.Name} ({hash})", processedBytes: processedBytes);
                    chunkHashes.Add(hash);
                }

                // TODO: save snapshot file



                _logger.LogInformation("Schedule {ScheduleId}: {Message} ({Processed}/{Total})",
                    schedule.Id, report.Message, report.Processed, report.Total);
            }
        }
    }
}
