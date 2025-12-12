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
                importData.Modules?.Count ?? 0,
                importData.Backups?.Count ?? 0,
                importData.Schedules?.Count ?? 0,
                importData.Snapshots?.Count ?? 0,
                importData.SnapshotFiles?.Count ?? 0);

            // Track old ID -> new ID mappings for foreign keys
            var moduleIdMap = new Dictionary<Guid, Guid>();
            var backupIdMap = new Dictionary<Guid, Guid>();
            var snapshotIdMap = new Dictionary<Guid, Guid>();

            // Import Modules - check by tag
            if (importData.Modules != null)
            {
                foreach (var module in importData.Modules)
                {
                    var oldId = module.Id;
                    var existing = await _dbContext.Modules
                        .FirstOrDefaultAsync(m => m.Tag == module.Tag && m.UserId == user.Id, cancellationToken);
                    
                    if (existing != null)
                    {
                        _logger.LogInformation("Module with tag {Tag} already exists for user, mapping old ID {OldId} to existing ID {NewId}", 
                            module.Tag, oldId, existing.Id);
                        moduleIdMap[oldId] = existing.Id;
                    }
                    else
                    {
                        // JSON deserializer won't set protected Id, so EF will generate new one
                        module.UserId = user.Id;
                        _dbContext.Modules.Add(module);
                        await _dbContext.SaveChangesAsync(cancellationToken);
                        moduleIdMap[oldId] = module.Id;
                        _logger.LogInformation("Added module {Tag}, mapped old ID {OldId} to new ID {NewId}", 
                            module.Tag, oldId, module.Id);
                    }
                }
            }

            // Import Backups - check by tag and remap source/storage IDs
            if (importData.Backups != null)
            {
                foreach (var backup in importData.Backups)
                {
                    var oldId = backup.Id;
                    var existing = await _dbContext.Backups
                        .Include(b => b.Source)
                        .FirstOrDefaultAsync(b => b.Tag == backup.Tag && b.Source.UserId == user.Id, cancellationToken);
                    
                    if (existing != null)
                    {
                        _logger.LogInformation("Backup with tag {Tag} already exists, mapping old ID {OldId} to existing ID {NewId}", 
                            backup.Tag, oldId, existing.Id);
                        backupIdMap[oldId] = existing.Id;
                    }
                    else
                    {
                        // Remap foreign keys
                        if (moduleIdMap.TryGetValue(backup.SourceId, out var newSourceId))
                        {
                            backup.SourceId = newSourceId;
                        }
                        else
                        {
                            _logger.LogWarning("Source module not found for backup {Tag}, skipping", backup.Tag);
                            continue;
                        }
                        
                        if (moduleIdMap.TryGetValue(backup.StorageId, out var newStorageId))
                        {
                            backup.StorageId = newStorageId;
                        }
                        else
                        {
                            _logger.LogWarning("Storage module not found for backup {Tag}, skipping", backup.Tag);
                            continue;
                        }

                        _dbContext.Backups.Add(backup);
                        await _dbContext.SaveChangesAsync(cancellationToken);
                        backupIdMap[oldId] = backup.Id;
                        _logger.LogInformation("Added backup {Tag}, mapped old ID {OldId} to new ID {NewId}", 
                            backup.Tag, oldId, backup.Id);
                    }
                }
            }

            // Import Schedules - remap backup IDs
            if (importData.Schedules != null)
            {
                foreach (var schedule in importData.Schedules)
                {
                    if (backupIdMap.TryGetValue(schedule.BackupId, out var newBackupId))
                    {
                        schedule.BackupId = newBackupId;
                        _dbContext.Schedules.Add(schedule);
                        _logger.LogInformation("Added schedule for backup ID {BackupId}", newBackupId);
                    }
                    else
                    {
                        _logger.LogWarning("Backup not found for schedule, skipping");
                    }
                }
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            // Import Snapshots - remap backup IDs
            if (importData.Snapshots != null)
            {
                foreach (var snapshot in importData.Snapshots)
                {
                    var oldId = snapshot.Id;
                    if (backupIdMap.TryGetValue(snapshot.BackupId, out var newBackupId))
                    {
                        snapshot.BackupId = newBackupId;
                        _dbContext.Snapshots.Add(snapshot);
                        await _dbContext.SaveChangesAsync(cancellationToken);
                        snapshotIdMap[oldId] = snapshot.Id;
                        _logger.LogInformation("Added snapshot, mapped old ID {OldId} to new ID {NewId}", oldId, snapshot.Id);
                    }
                    else
                    {
                        _logger.LogWarning("Backup not found for snapshot, skipping");
                    }
                }
            }

            // Import Snapshot Files - remap snapshot IDs
            if (importData.SnapshotFiles != null)
            {
                int addedCount = 0;
                foreach (var snapshotFile in importData.SnapshotFiles)
                {
                    if (snapshotIdMap.TryGetValue(snapshotFile.SnapshotId, out var newSnapshotId))
                    {
                        snapshotFile.SnapshotId = newSnapshotId;
                        _dbContext.SnapshotFiles.Add(snapshotFile);
                        addedCount++;
                    }
                    else
                    {
                        _logger.LogWarning("Snapshot not found for snapshot file, skipping");
                    }
                }
                await _dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Added {Count} snapshot files", addedCount);
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
