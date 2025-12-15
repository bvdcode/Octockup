// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using EasyExtensions.Abstractions;
using EasyExtensions.Quartz.Attributes;
using Microsoft.EntityFrameworkCore;
using Octockup.Server.Database;
using Octockup.Server.Helpers;
using Quartz;
using System.Collections.Concurrent;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Octockup.Server.Jobs
{
    [JobTrigger(days: 365, startNow: false)]
    public class ImportBackupJob(
        IStreamCipher _crypto,
        AppDbContext _dbContext,
        ILogger<ImportBackupJob> _logger) : IJob
    {
        private const int BATCH_SIZE = 500;
        private static readonly ConcurrentDictionary<Type, Action<object, Guid>?> _idSetterCache = new();
        private static readonly Lazy<JsonSerializerOptions> _jsonOptions = new(CreateOptions);

        public static JsonSerializerOptions CreateOptions()
        {
            var resolver = new DefaultJsonTypeInfoResolver();
            resolver.Modifiers.Add(static ti =>
            {
                if (ti.Kind != JsonTypeInfoKind.Object)
                {
                    return;
                }

                var jsonProp = ti.Properties.FirstOrDefault(p =>
                    string.Equals(p.Name, "Id", StringComparison.Ordinal) &&
                    p.PropertyType == typeof(Guid));

                if (jsonProp is null)
                {
                    return;
                }

                if (jsonProp.Set is not null)
                {
                    return;
                }

                var idPropInfo = ti.Type.GetProperty("Id",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);

                var setMethod = idPropInfo?.GetSetMethod(nonPublic: true);
                if (setMethod is null)
                {
                    return;
                }

                var setter = _idSetterCache.GetOrAdd(ti.Type, _ =>
                {
                    return (obj, value) => setMethod.Invoke(obj, [value]);
                });

                jsonProp.Set = (obj, value) =>
                {
                    setter?.Invoke(obj!, (Guid)value!);
                };
            });

            return new JsonSerializerOptions
            {
                IncludeFields = true,
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

            foreach (var userDir in Directory.GetDirectories(importBaseDir))
            {
                if (!Guid.TryParse(Path.GetFileName(userDir), out Guid userId))
                {
                    _logger.LogWarning("Invalid user directory name: {UserDir}", userDir);
                    continue;
                }

                await ProcessUserDirectoryAsync(userId, userDir, cancellationToken);
            }
        }

        private async Task ProcessUserDirectoryAsync(Guid userId, string userDir, CancellationToken cancellationToken)
        {
            foreach (var importFile in Directory.GetFiles(userDir, "*.octockup"))
            {
                await ProcessSingleFileWithFailureHandlingAsync(userId, importFile, cancellationToken);
            }

            // Delete directory only when empty
            if (!Directory.EnumerateFileSystemEntries(userDir).Any())
            {
                Directory.Delete(userDir);
                _logger.LogInformation("Deleted empty user import directory: {UserDir}", userDir);
            }
        }

        private async Task ProcessSingleFileWithFailureHandlingAsync(Guid userId, string importFile, CancellationToken cancellationToken)
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

        private async Task ProcessImportFileAsync(Guid userId, string filePath, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting import for user {UserId} from file {FilePath}", userId, filePath);

            var user = await ResolveTargetUserAsync(userId, cancellationToken);
            if (user is null)
            {
                _logger.LogWarning("User {UserId} not found, skipping import", userId);
                return;
            }

            await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            await using var decompressedStream = CompressionHelpers.CreateDecompressionStream(fileStream);
            using var decryptedStream = new MemoryStream();

            await _crypto.DecryptAsync(decompressedStream, decryptedStream, ct: cancellationToken);
            decryptedStream.Seek(0, SeekOrigin.Begin);

            var importData = await JsonSerializer.DeserializeAsync<ImportData>(
                decryptedStream,
                options: _jsonOptions.Value,
                cancellationToken: cancellationToken);

            if (importData == null)
            {
                _logger.LogWarning("Failed to deserialize import data from file {FilePath}", filePath);
                return;
            }

            _logger.LogInformation(
                "Import data contains: {ModuleCount} modules, {BackupCount} backups, {ScheduleCount} schedules, {SnapshotCount} snapshots, {SnapshotFileCount} snapshot files",
                importData.Modules.Count,
                importData.Backups.Count,
                importData.Schedules.Count,
                importData.Snapshots.Count,
                importData.SnapshotFiles.Count);

            // Simply update UserId WITHOUT restoring navigations
            foreach (var item in importData.Modules)
            {
                item.UserId = user.Id;
            }

            _logger.LogInformation("Saving imported data to the database for user {UserId} in batches...", userId);

            await using var tx = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                RestoreModuleParameters(importData);

                // Modules - small, can be saved all at once, but DETACH after
                _dbContext.Modules.AddRange(importData.Modules);
                await _dbContext.SaveChangesAsync(cancellationToken);
                _dbContext.ChangeTracker.Clear();
                _logger.LogInformation("Imported {Count} modules", importData.Modules.Count);

                // Backups - small, but DETACH after
                _dbContext.Backups.AddRange(importData.Backups);
                await _dbContext.SaveChangesAsync(cancellationToken);
                _dbContext.ChangeTracker.Clear();
                _logger.LogInformation("Imported {Count} backups", importData.Backups.Count);

                // Schedules - small, but DETACH after
                _dbContext.Schedules.AddRange(importData.Schedules);
                await _dbContext.SaveChangesAsync(cancellationToken);
                _dbContext.ChangeTracker.Clear();
                _logger.LogInformation("Imported {Count} schedules", importData.Schedules.Count);

                // Snapshots - in batches with DETACH
                await SaveInBatchesWithDetachAsync(importData.Snapshots, _dbContext.Snapshots, "snapshots", cancellationToken);

                // SnapshotFiles - LARGE, must be in batches + clear ChangeTracker
                await SaveInBatchesWithDetachAsync(importData.SnapshotFiles, _dbContext.SnapshotFiles, "snapshot files", cancellationToken);

                await tx.CommitAsync(cancellationToken);
                _logger.LogInformation("Successfully committed import transaction for user {UserId}", userId);
            }
            catch
            {
                await tx.RollbackAsync(cancellationToken);
                throw;
            }

            _logger.LogInformation("Successfully completed import for user {UserId} from file {FilePath}", userId, filePath);
        }

        private async Task<User?> ResolveTargetUserAsync(Guid userId, CancellationToken cancellationToken)
        {
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

            return user;
        }

        private void RestoreModuleParameters(ImportData importData)
        {
            foreach (var module in importData.Modules)
            {
#pragma warning disable CS0618 // Type or member is obsolete
                foreach (var item in module.Parameters)
                {
                    module.Params(_crypto)[item.Key] = item.Value;
                    _logger.LogInformation("Restored parameter '{ParamKey}' for Module {ModuleId}.", item.Key, module.Id);
                }
#pragma warning restore CS0618 // Type or member is obsolete
            }
        }

        private async Task SaveInBatchesWithDetachAsync<T>(List<T> items, DbSet<T> dbSet, string entityName, CancellationToken ct) where T : class
        {
            if (items.Count == 0)
            {
                return;
            }

            int totalBatches = (items.Count + BATCH_SIZE - 1) / BATCH_SIZE;
            for (int i = 0; i < items.Count; i += BATCH_SIZE)
            {
                var batch = items.Skip(i).Take(BATCH_SIZE).ToList();
                dbSet.AddRange(batch);
                await _dbContext.SaveChangesAsync(ct);

                // Clear ChangeTracker to free memory
                _dbContext.ChangeTracker.Clear();

                int currentBatch = (i / BATCH_SIZE) + 1;
                _logger.LogInformation(
                    "Imported batch {CurrentBatch}/{TotalBatches} of {EntityName} ({Count} items), memory freed",
                    currentBatch,
                    totalBatches,
                    entityName,
                    batch.Count);
            }
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
