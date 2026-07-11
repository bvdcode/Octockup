// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.Abstractions;
using Microsoft.EntityFrameworkCore;
using Octockup.Server.Database;
using Octockup.Server.Models.Enums;
using Octockup.Server.Models.Transfer;
using System.Text.Json;

namespace Octockup.Server.Services
{
    public class ServerBackupImportBatchWriter(
        AppDbContext _dbContext,
        IStreamCipher _crypto,
        Guid _userId,
        ILogger _logger)
    {
        private const int BatchSize = 500;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly long[] _sectionCounts = new long[5];
        private int _pendingCount;

        public IReadOnlyList<long> SectionCounts => _sectionCounts;

        public async Task ProcessAsync(
            ServerBackupJsonEvent jsonEvent,
            CancellationToken cancellationToken)
        {
            if (jsonEvent.SectionCompleted)
            {
                await FlushAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogInformation(
                    "Imported {Count} records from server backup section {Section}.",
                    _sectionCounts[(int)jsonEvent.Section],
                    jsonEvent.Section);
                return;
            }

            JsonElement item = jsonEvent.Document?.RootElement
                ?? throw new JsonException("Server backup array item is missing.");
            switch (jsonEvent.Section)
            {
                case ServerBackupSection.Modules:
                    AddModule(Deserialize<ServerBackupModuleRecord>(item));
                    break;
                case ServerBackupSection.Backups:
                    AddBackup(Deserialize<ServerBackupBackupRecord>(item));
                    break;
                case ServerBackupSection.Schedules:
                    AddSchedule(Deserialize<ServerBackupScheduleRecord>(item));
                    break;
                case ServerBackupSection.Snapshots:
                    AddSnapshot(Deserialize<ServerBackupSnapshotRecord>(item));
                    break;
                case ServerBackupSection.SnapshotFiles:
                    AddSnapshotFile(Deserialize<ServerBackupSnapshotFileRecord>(item));
                    break;
                default:
                    throw new JsonException(
                        $"Unsupported server backup section {jsonEvent.Section}.");
            }

            _sectionCounts[(int)jsonEvent.Section]++;
            _pendingCount++;
            if (_pendingCount >= BatchSize)
            {
                await FlushAsync(cancellationToken).ConfigureAwait(false);
            }
        }

        public Task CompleteAsync(CancellationToken cancellationToken)
        {
            return FlushAsync(cancellationToken);
        }

        private void AddModule(ServerBackupModuleRecord record)
        {
            Module module = new()
            {
                UserId = _userId,
                Tag = record.Tag,
                Destination = record.Destination,
                BackupModuleId = record.BackupModuleId
            };
            foreach (KeyValuePair<string, string> parameter in record.Parameters)
            {
                module.Params(_crypto)[parameter.Key] = parameter.Value;
            }

            AddWithIdentity(_dbContext.Modules, module, record.Id);
        }

        private void AddBackup(ServerBackupBackupRecord record)
        {
            Backup backup = new()
            {
                UserId = _userId,
                SourceId = record.SourceId,
                StorageId = record.StorageId,
                Tag = record.Tag,
                IgnoredPaths = record.IgnoredPaths,
                DisableCompression = record.DisableCompression,
                DisableEncryption = record.DisableEncryption
            };
            AddWithIdentity(_dbContext.Backups, backup, record.Id);
        }

        private void AddSchedule(ServerBackupScheduleRecord record)
        {
            Schedule schedule = new()
            {
                BackupId = record.BackupId,
                FinishedAt = NormalizeUtc(record.FinishedAt),
                Status = record.Status,
                StartAt = NormalizeUtc(record.StartAt),
                NextRunAt = NormalizeUtc(record.NextRunAt),
                Interval = record.Interval,
                ErrorMessage = record.ErrorMessage
            };
            AddWithIdentity(_dbContext.Schedules, schedule, record.Id);
        }

        private void AddSnapshot(ServerBackupSnapshotRecord record)
        {
            Snapshot snapshot = new()
            {
                BackupId = record.BackupId,
                CompletedAt = NormalizeUtc(record.CompletedAt),
                TotalSize = record.TotalSize,
                FilesCount = record.FilesCount
            };
            AddWithIdentity(_dbContext.Snapshots, snapshot, record.Id);
        }

        private void AddSnapshotFile(ServerBackupSnapshotFileRecord record)
        {
            SnapshotFile snapshotFile = new()
            {
                SnapshotId = record.SnapshotId,
                Size = record.Size,
                LastModified = NormalizeUtc(record.LastModified),
                Name = record.Name,
                Path = record.Path,
                Hashsum = record.Hashsum,
                ChunkHashes = record.ChunkHashes,
                ChunkReferencesIndexed = false
            };
            AddWithIdentity(_dbContext.SnapshotFiles, snapshotFile, record.Id);
        }

        private void AddWithIdentity<TEntity>(
            DbSet<TEntity> dbSet,
            TEntity entity,
            Guid id)
            where TEntity : class
        {
            if (id == Guid.Empty)
            {
                throw new JsonException(
                    $"Server backup {typeof(TEntity).Name} has an empty ID.");
            }

            dbSet.Add(entity);
            _dbContext.Entry(entity).Property("Id").CurrentValue = id;
        }

        private async Task FlushAsync(CancellationToken cancellationToken)
        {
            if (_pendingCount == 0)
            {
                return;
            }

            await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            _dbContext.ChangeTracker.Clear();
            _pendingCount = 0;
        }

        private static T Deserialize<T>(JsonElement item)
        {
            return item.Deserialize<T>(JsonOptions)
                ?? throw new JsonException(
                    $"Failed to deserialize server backup {typeof(T).Name}.");
        }

        private static DateTime NormalizeUtc(DateTime value)
        {
            return value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
            };
        }

        private static DateTime? NormalizeUtc(DateTime? value)
        {
            return value.HasValue ? NormalizeUtc(value.Value) : null;
        }
    }
}
