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
    [JobTrigger(minutes: 1)]
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
                        
                        // Delete file after successful processing
                        File.Delete(importFile);
                        _logger.LogInformation("Successfully processed and deleted import file: {ImportFile}", importFile);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to process import file: {ImportFile}", importFile);
                        
                        // Rename failed file to avoid reprocessing
                        string failedPath = importFile + ".failed";
                        if (File.Exists(failedPath))
                        {
                            File.Delete(failedPath);
                        }
                        File.Move(importFile, failedPath);
                        _logger.LogInformation("Renamed failed import file to: {FailedPath}", failedPath);
                    }
                }

                // Clean up empty user directories
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

            // Verify user exists
            var user = await _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

            if (user == null)
            {
                _logger.LogWarning("User {UserId} not found, skipping import", userId);
                return;
            }

            // Read and decrypt the file
            await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            await using var brotliStream = new BrotliStream(fileStream, CompressionMode.Decompress, leaveOpen: true);
            using var decryptedStream = new MemoryStream();
            
            await _streamCipher.DecryptAsync(brotliStream, decryptedStream, ct: cancellationToken);
            decryptedStream.Seek(0, SeekOrigin.Begin);

            // Deserialize the data
            var importData = await JsonSerializer.DeserializeAsync<ImportData>(decryptedStream, cancellationToken: cancellationToken);
            
            if (importData == null)
            {
                _logger.LogWarning("Failed to deserialize import data from file {FilePath}", filePath);
                return;
            }

            _logger.LogInformation("Import data contains: {ModuleCount} modules, {BackupCount} backups, {ScheduleCount} schedules, {SnapshotCount} snapshots, {SnapshotFileCount} snapshot files",
                importData.Modules?.Count ?? 0,
                importData.Backups?.Count ?? 0,
                importData.Schedules?.Count ?? 0,
                importData.Snapshots?.Count ?? 0,
                importData.SnapshotFiles?.Count ?? 0);

            // Track ID mappings for relationships
            var moduleIdMap = new Dictionary<Guid, Guid>();
            var backupIdMap = new Dictionary<Guid, Guid>();
            var snapshotIdMap = new Dictionary<Guid, Guid>();

            // Import Modules
            if (importData.Modules != null)
            {
                foreach (var oldModule in importData.Modules)
                {
                    var oldId = oldModule.Id;
                    var existingModule = await _dbContext.Modules
                        .FirstOrDefaultAsync(m => m.UserId == userId && m.Tag == oldModule.Tag, cancellationToken);

                    if (existingModule != null)
                    {
                        _logger.LogInformation("Module with tag {Tag} already exists for user {UserId}, using existing module", oldModule.Tag, userId);
                        moduleIdMap[oldId] = existingModule.Id;
                    }
                    else
                    {
                        var newModule = new Module
                        {
                            UserId = userId,
                            Tag = oldModule.Tag,
                            Destination = oldModule.Destination,
                            BackupModuleId = oldModule.BackupModuleId,
                            Parameters = oldModule.Parameters,
                            EncryptedParameters = oldModule.EncryptedParameters
                        };
                        await _dbContext.Modules.AddAsync(newModule, cancellationToken);
                        await _dbContext.SaveChangesAsync(cancellationToken);
                        moduleIdMap[oldId] = newModule.Id;
                        _logger.LogInformation("Imported module {Tag} with new ID {NewId}", newModule.Tag, newModule.Id);
                    }
                }
            }

            // Import Backups
            if (importData.Backups != null)
            {
                foreach (var oldBackup in importData.Backups)
                {
                    var oldId = oldBackup.Id;
                    var existingBackup = await _dbContext.Backups
                        .Include(b => b.Source)
                        .FirstOrDefaultAsync(b => b.Tag == oldBackup.Tag && b.Source.UserId == userId, cancellationToken);

                    if (existingBackup != null)
                    {
                        _logger.LogInformation("Backup with tag {Tag} already exists for user {UserId}, using existing backup", oldBackup.Tag, userId);
                        backupIdMap[oldId] = existingBackup.Id;
                    }
                    else
                    {
                        // Get mapped module IDs
                        if (!moduleIdMap.TryGetValue(oldBackup.SourceId, out var newSourceId))
                        {
                            _logger.LogWarning("Source module {SourceId} not found for backup {Tag}, skipping", oldBackup.SourceId, oldBackup.Tag);
                            continue;
                        }
                        if (!moduleIdMap.TryGetValue(oldBackup.StorageId, out var newStorageId))
                        {
                            _logger.LogWarning("Storage module {StorageId} not found for backup {Tag}, skipping", oldBackup.StorageId, oldBackup.Tag);
                            continue;
                        }

                        var newBackup = new Backup
                        {
                            SourceId = newSourceId,
                            StorageId = newStorageId,
                            Tag = oldBackup.Tag,
                            IgnoredPaths = oldBackup.IgnoredPaths
                        };

                        await _dbContext.Backups.AddAsync(newBackup, cancellationToken);
                        await _dbContext.SaveChangesAsync(cancellationToken);
                        backupIdMap[oldId] = newBackup.Id;
                        _logger.LogInformation("Imported backup {Tag} with new ID {NewId}", newBackup.Tag, newBackup.Id);
                    }
                }
            }

            // Import Schedules
            if (importData.Schedules != null)
            {
                foreach (var oldSchedule in importData.Schedules)
                {
                    if (backupIdMap.TryGetValue(oldSchedule.BackupId, out var newBackupId))
                    {
                        var newSchedule = new Schedule
                        {
                            BackupId = newBackupId,
                            StartAt = oldSchedule.StartAt,
                            Interval = oldSchedule.Interval,
                            Status = oldSchedule.Status,
                            ErrorMessage = oldSchedule.ErrorMessage,
                            FinishedAt = oldSchedule.FinishedAt
                        };
                        await _dbContext.Schedules.AddAsync(newSchedule, cancellationToken);
                        await _dbContext.SaveChangesAsync(cancellationToken);
                        _logger.LogInformation("Imported schedule with new ID {NewId}", newSchedule.Id);
                    }
                    else
                    {
                        _logger.LogWarning("Backup {BackupId} not found for schedule, skipping", oldSchedule.BackupId);
                    }
                }
            }

            // Import Snapshots
            if (importData.Snapshots != null)
            {
                foreach (var oldSnapshot in importData.Snapshots)
                {
                    var oldId = oldSnapshot.Id;
                    if (backupIdMap.TryGetValue(oldSnapshot.BackupId, out var newBackupId))
                    {
                        var newSnapshot = new Snapshot
                        {
                            BackupId = newBackupId,
                            TotalSize = oldSnapshot.TotalSize,
                            FilesCount = oldSnapshot.FilesCount,
                            CompletedAt = oldSnapshot.CompletedAt
                        };
                        await _dbContext.Snapshots.AddAsync(newSnapshot, cancellationToken);
                        await _dbContext.SaveChangesAsync(cancellationToken);
                        snapshotIdMap[oldId] = newSnapshot.Id;
                        _logger.LogInformation("Imported snapshot with new ID {NewId}", newSnapshot.Id);
                    }
                    else
                    {
                        _logger.LogWarning("Backup {BackupId} not found for snapshot, skipping", oldSnapshot.BackupId);
                    }
                }
            }

            // Import Snapshot Files
            if (importData.SnapshotFiles != null)
            {
                foreach (var oldSnapshotFile in importData.SnapshotFiles)
                {
                    if (snapshotIdMap.TryGetValue(oldSnapshotFile.SnapshotId, out var newSnapshotId))
                    {
                        var newSnapshotFile = new SnapshotFile
                        {
                            SnapshotId = newSnapshotId,
                            Path = oldSnapshotFile.Path,
                            Name = oldSnapshotFile.Name,
                            Size = oldSnapshotFile.Size,
                            Hashsum = oldSnapshotFile.Hashsum,
                            ChunkHashes = oldSnapshotFile.ChunkHashes,
                            LastModified = oldSnapshotFile.LastModified
                        };
                        await _dbContext.SnapshotFiles.AddAsync(newSnapshotFile, cancellationToken);
                    }
                    else
                    {
                        _logger.LogWarning("Snapshot {SnapshotId} not found for snapshot file, skipping", oldSnapshotFile.SnapshotId);
                    }
                }
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            _logger.LogInformation("Successfully completed import for user {UserId} from file {FilePath}", userId, filePath);
        }

        private class ImportData
        {
            public List<Module>? Modules { get; set; }
            public List<Backup>? Backups { get; set; }
            public List<Schedule>? Schedules { get; set; }
            public List<Snapshot>? Snapshots { get; set; }
            public List<SnapshotFile>? SnapshotFiles { get; set; }
        }
    }
}
