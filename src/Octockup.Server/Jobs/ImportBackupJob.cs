// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using EasyExtensions.Abstractions;
using EasyExtensions.Quartz.Attributes;
using Microsoft.EntityFrameworkCore;
using Octockup.Server.Database;
using Quartz;
using System.IO.Compression;
using System.Text.Json;

namespace Octockup.Server.Jobs
{
    [JobTrigger(days: 365, startNow: false)]
    public class ImportBackupJob(
        IStreamCipher _streamCipher,
        AppDbContext _dbContext,
        ILogger<ImportBackupJob> _logger) : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            CancellationToken cancellationToken = context.CancellationToken;

            string importBaseDir = Path.Combine(Path.GetTempPath(), "octockup-imports");
            if (!Directory.Exists(importBaseDir))
            {
                return;
            }

            var userDirs = Directory.GetDirectories(importBaseDir);
            foreach (var userDir in userDirs)
            {
                if (!Guid.TryParse(Path.GetFileName(userDir), out Guid userId))
                {
                    _logger.LogWarning("Invalid user directory name: {UserDir}", userDir);
                    continue;
                }

                var importFiles = Directory.GetFiles(userDir, "*.octockup");
                foreach (var importFile in importFiles)
                {
                    try
                    {
                        _logger.LogInformation("Processing import file: {ImportFile}", importFile);
                        await ProcessImportFileAsync(userId, importFile, cancellationToken);
                        
                        File.Delete(importFile);
                        _logger.LogInformation("Successfully processed and deleted import file: {ImportFile}", importFile);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to process import file: {ImportFile}", importFile);
                        
                        string failedPath = importFile + ".failed";
                        if (File.Exists(failedPath))
                        {
                            File.Delete(failedPath);
                        }
                        File.Move(importFile, failedPath);
                        _logger.LogInformation("Renamed failed import file to: {FailedPath}", failedPath);
                    }
                }

                if (!Directory.EnumerateFileSystemEntries(userDir).Any())
                {
                    Directory.Delete(userDir);
                    _logger.LogInformation("Deleted empty user import directory: {UserDir}", userDir);
                }
            }
        }

        private async Task ProcessImportFileAsync(Guid userId, string filePath, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting import for user {UserId} from file {FilePath}", userId, filePath);

            var user = await _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

            int usersCount = await _dbContext.Users.CountAsync(cancellationToken);
            if (usersCount == 1)
            {
                user = await _dbContext.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(cancellationToken);
                _logger.LogInformation("Only one user in the system, using user {UserId} for import", user?.Id);
            }

            if (user == null)
            {
                _logger.LogWarning("User {UserId} not found, skipping import", userId);
                return;
            }

            await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            await using var brotliStream = new BrotliStream(fileStream, CompressionMode.Decompress, leaveOpen: true);
            using var decryptedStream = new MemoryStream();
            
            await _streamCipher.DecryptAsync(brotliStream, decryptedStream, ct: cancellationToken);
            decryptedStream.Seek(0, SeekOrigin.Begin);

            var importData = await JsonSerializer.DeserializeAsync<ImportData>(decryptedStream, cancellationToken: cancellationToken);
            
            if (importData == null)
            {
                _logger.LogWarning("Failed to deserialize import data from file {FilePath}", filePath);
                return;
            }

            _logger.LogInformation("Import data contains: {ModuleCount} modules, {BackupCount} backups, {ScheduleCount} schedules, {SnapshotCount} snapshots, {SnapshotFileCount} snapshot files",
                importData.Modules.Count,
                importData.Backups.Count,
                importData.Schedules.Count,
                importData.Snapshots.Count,
                importData.SnapshotFiles.Count);

            foreach (var item in importData.Modules)
            {
                item.User = user;
            }

            foreach (var snapshot in importData.Snapshots)
            {
                snapshot.Files = [.. importData.SnapshotFiles.Where(sf => sf.SnapshotId == snapshot.Id)];
                snapshot.Backup = importData.Backups.First(b => b.Id == snapshot.BackupId);
                snapshot.Backup.Schedules = [.. importData.Schedules.Where(s => s.BackupId == snapshot.BackupId)];
                snapshot.Backup.Source = importData.Modules.First(m => m.Id == snapshot.Backup.SourceId);
                snapshot.Backup.Storage = importData.Modules.First(m => m.Id == snapshot.Backup.StorageId);
            }

            await _dbContext.Modules.AddRangeAsync(importData.Modules, cancellationToken);
            await _dbContext.Backups.AddRangeAsync(importData.Backups, cancellationToken);
            await _dbContext.Schedules.AddRangeAsync(importData.Schedules, cancellationToken);
            await _dbContext.Snapshots.AddRangeAsync(importData.Snapshots, cancellationToken);
            await _dbContext.SnapshotFiles.AddRangeAsync(importData.SnapshotFiles, cancellationToken);

            _logger.LogInformation("Saving imported data to the database for user {UserId}...", userId);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Successfully completed import for user {UserId} from file {FilePath}", userId, filePath);
        }

        private class ImportData
        {
            public List<Module> Modules { get; set; } = [];
            public List<Backup> Backups { get; set; } = [];
            public List<Schedule> Schedules { get; set; } = [];
            public List<Snapshot> Snapshots { get; set; } = [];
            public List<SnapshotFile> SnapshotFiles { get; set; } = [];
        }
    }
}
