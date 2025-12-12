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

            // Просто добавляем всё как есть, EF разберется с ID и конфликтами
            if (importData.Modules != null)
            {
                foreach (var module in importData.Modules)
                {
                    var exists = await _dbContext.Modules.AnyAsync(m => m.Id == module.Id, cancellationToken);
                    if (!exists)
                    {
                        _dbContext.Modules.Add(module);
                        _logger.LogInformation("Added module {Tag} with ID {Id}", module.Tag, module.Id);
                    }
                    else
                    {
                        _logger.LogInformation("Module {Tag} with ID {Id} already exists, skipping", module.Tag, module.Id);
                    }
                }
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            if (importData.Backups != null)
            {
                foreach (var backup in importData.Backups)
                {
                    var exists = await _dbContext.Backups.AnyAsync(b => b.Id == backup.Id, cancellationToken);
                    if (!exists)
                    {
                        _dbContext.Backups.Add(backup);
                        _logger.LogInformation("Added backup {Tag} with ID {Id}", backup.Tag, backup.Id);
                    }
                    else
                    {
                        _logger.LogInformation("Backup {Tag} with ID {Id} already exists, skipping", backup.Tag, backup.Id);
                    }
                }
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            if (importData.Schedules != null)
            {
                foreach (var schedule in importData.Schedules)
                {
                    var exists = await _dbContext.Schedules.AnyAsync(s => s.Id == schedule.Id, cancellationToken);
                    if (!exists)
                    {
                        _dbContext.Schedules.Add(schedule);
                        _logger.LogInformation("Added schedule with ID {Id}", schedule.Id);
                    }
                    else
                    {
                        _logger.LogInformation("Schedule with ID {Id} already exists, skipping", schedule.Id);
                    }
                }
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            if (importData.Snapshots != null)
            {
                foreach (var snapshot in importData.Snapshots)
                {
                    var exists = await _dbContext.Snapshots.AnyAsync(s => s.Id == snapshot.Id, cancellationToken);
                    if (!exists)
                    {
                        _dbContext.Snapshots.Add(snapshot);
                        _logger.LogInformation("Added snapshot with ID {Id}", snapshot.Id);
                    }
                    else
                    {
                        _logger.LogInformation("Snapshot with ID {Id} already exists, skipping", snapshot.Id);
                    }
                }
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            if (importData.SnapshotFiles != null)
            {
                foreach (var snapshotFile in importData.SnapshotFiles)
                {
                    var exists = await _dbContext.SnapshotFiles.AnyAsync(sf => sf.Id == snapshotFile.Id, cancellationToken);
                    if (!exists)
                    {
                        _dbContext.SnapshotFiles.Add(snapshotFile);
                    }
                    else
                    {
                        _logger.LogInformation("SnapshotFile with ID {Id} already exists, skipping", snapshotFile.Id);
                    }
                }
                await _dbContext.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Added {Count} snapshot files", importData.SnapshotFiles.Count);
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
