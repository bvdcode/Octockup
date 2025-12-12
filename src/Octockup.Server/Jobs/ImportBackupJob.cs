// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using EasyExtensions.Abstractions;
using EasyExtensions.Quartz.Attributes;
using Microsoft.EntityFrameworkCore;
using Octockup.Server.Database;
using Quartz;
using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Octockup.Server.Jobs
{
    [JobTrigger(days: 365, startNow: false)]
    public class ImportBackupJob(
        IStreamCipher _streamCipher,
        AppDbContext _dbContext,
        ILogger<ImportBackupJob> _logger) : IJob
    {
        private static readonly ConcurrentDictionary<Type, Action<object, Guid>?> _idSetterCache = new();

        public static JsonSerializerOptions CreateOptions()
        {
            var resolver = new DefaultJsonTypeInfoResolver();
            resolver.Modifiers.Add(static ti =>
            {
                if (ti.Kind != JsonTypeInfoKind.Object)
                    return;

                // Ищем JSON property "Id" (имя будет "Id" по умолчанию)
                var jsonProp = ti.Properties.FirstOrDefault(p =>
                    string.Equals(p.Name, "Id", StringComparison.Ordinal) &&
                    p.PropertyType == typeof(Guid));

                if (jsonProp is null)
                {
                    return;
                }

                // Если уже есть setter — не трогаем
                if (jsonProp.Set is not null)
                {
                    return;
                }

                // Если у типа нет property Id — выходим
                var idPropInfo = ti.Type.GetProperty("Id",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

                var setMethod = idPropInfo?.GetSetMethod(nonPublic: true);
                if (setMethod is null)
                {
                    return;
                }

                // Кэшируем быстрый делегат
                var setter = _idSetterCache.GetOrAdd(ti.Type, _ =>
                {
                    // Создаём делегат вида (object obj, Guid value) => ((T)obj).set_Id(value)
                    return (obj, value) => setMethod.Invoke(obj, [value]);
                });

                jsonProp.Set = (obj, value) =>
                {
                    // value приходит как object, но это Guid
                    setter?.Invoke(obj!, (Guid)value!);
                };
            });

            return new JsonSerializerOptions
            {
                TypeInfoResolver = resolver,
                PropertyNameCaseInsensitive = true
            };
        }

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

            var importData = await JsonSerializer.DeserializeAsync<ImportData>(
                decryptedStream, options: CreateOptions(),
                cancellationToken: cancellationToken);

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
                item.UserId = user.Id;
            }

            var modulesById = importData.Modules.ToDictionary(m => m.Id);
            var backupsById = importData.Backups.ToDictionary(b => b.Id);
            var snapshotsById = importData.Snapshots.ToDictionary(s => s.Id);

            // Восстанавливаем связи Module <-> Backup
            foreach (var backup in importData.Backups)
            {
                if (modulesById.TryGetValue(backup.SourceId, out var source))
                {
                    backup.Source = source;
                    backup.SourceId = source.Id;
                }
                else
                {
                    throw new InvalidOperationException($"Source module with ID {backup.SourceId} not found for backup {backup.Id}");
                }

                if (modulesById.TryGetValue(backup.StorageId, out var storage))
                {
                    backup.Storage = storage;
                    backup.StorageId = storage.Id;
                }
                else
                {
                    throw new InvalidOperationException($"Storage module with ID {backup.StorageId} not found for backup {backup.Id}");
                }
            }

            // Восстанавливаем связи Backup <-> Schedule
            foreach (var schedule in importData.Schedules)
            {
                if (backupsById.TryGetValue(schedule.BackupId, out var backup))
                {
                    schedule.Backup = backup;
                    schedule.BackupId = backup.Id;
                    backup.Schedules ??= [];
                    backup.Schedules.Add(schedule);
                }
                else
                {
                    throw new InvalidOperationException($"Backup with ID {schedule.BackupId} not found for schedule {schedule.Id}");
                }
            }

            // Восстанавливаем связи Backup <-> Snapshot
            foreach (var snapshot in importData.Snapshots)
            {
                if (backupsById.TryGetValue(snapshot.BackupId, out var backup))
                {
                    snapshot.Backup = backup;
                    snapshot.BackupId = backup.Id;
                    backup.Snapshots ??= [];
                    backup.Snapshots.Add(snapshot);
                }
                else
                {
                    throw new InvalidOperationException($"Backup with ID {snapshot.BackupId} not found for snapshot {snapshot.Id}");
                }
            }

            // Восстанавливаем связи Snapshot <-> SnapshotFile
            foreach (var snapshotFile in importData.SnapshotFiles)
            {
                if (snapshotsById.TryGetValue(snapshotFile.SnapshotId, out var snapshot))
                {
                    snapshotFile.Snapshot = snapshot;
                    snapshotFile.SnapshotId = snapshot.Id;
                    snapshot.Files ??= [];
                    snapshot.Files.Add(snapshotFile);
                }
                else
                {
                    throw new InvalidOperationException($"Snapshot with ID {snapshotFile.SnapshotId} not found for snapshot file {snapshotFile.Id}");
                }
            }

            _logger.LogInformation("Saving imported data to the database for user {UserId}...", userId);

            await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            _dbContext.Modules.AddRange(importData.Modules);
            _dbContext.Backups.AddRange(importData.Backups);
            _dbContext.Schedules.AddRange(importData.Schedules);
            _dbContext.Snapshots.AddRange(importData.Snapshots);
            _dbContext.SnapshotFiles.AddRange(importData.SnapshotFiles);

            await _dbContext.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

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
