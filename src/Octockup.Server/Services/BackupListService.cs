// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore;
using Octockup.Server.Database;
using Octockup.Server.Models.Dto;
using Octockup.Server.Models.Enums;

namespace Octockup.Server.Services
{
    public class BackupListService(AppDbContext _dbContext)
    {
        public async Task<IReadOnlyList<BackupDto>> GetAsync(
            Guid userId,
            CancellationToken cancellationToken)
        {
            return await _dbContext.Backups
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .OrderBy(x => x.Tag)
                .Select(x => new BackupDto
                {
                    Id = x.Id,
                    CreatedAt = x.CreatedAt,
                    SourceId = x.SourceId,
                    StorageId = x.StorageId,
                    Tag = x.Tag,
                    IgnoredPaths = x.IgnoredPaths,
                    DisableCompression = x.DisableCompression,
                    DisableEncryption = x.DisableEncryption,
                    Source = new ModuleDto
                    {
                        Id = x.Source.Id,
                        CreatedAt = x.Source.CreatedAt,
                        UserId = x.Source.UserId,
                        Tag = x.Source.Tag,
                        BackupModuleId = x.Source.BackupModuleId,
                        Destination = x.Source.Destination
                    },
                    Storage = new ModuleDto
                    {
                        Id = x.Storage.Id,
                        CreatedAt = x.Storage.CreatedAt,
                        UserId = x.Storage.UserId,
                        Tag = x.Storage.Tag,
                        BackupModuleId = x.Storage.BackupModuleId,
                        Destination = x.Storage.Destination
                    },
                    SnapshotCount = x.Snapshots.Count,
                    CompletedSnapshotCount = x.Snapshots.Count(snapshot =>
                        snapshot.CompletedAt != null),
                    ScheduleCount = x.Schedules.Count,
                    LatestSnapshot = x.Snapshots
                        .Where(snapshot => snapshot.CompletedAt != null)
                        .OrderByDescending(snapshot => snapshot.CompletedAt)
                        .ThenByDescending(snapshot => snapshot.Id)
                        .Select(snapshot => new SnapshotDto
                        {
                            Id = snapshot.Id,
                            CreatedAt = snapshot.CreatedAt,
                            BackupId = snapshot.BackupId,
                            CompletedAt = snapshot.CompletedAt,
                            FilesCount = snapshot.FilesCount,
                            TotalSize = snapshot.TotalSize
                        })
                        .FirstOrDefault(),
                    ActiveSchedule = x.Schedules
                        .Where(schedule =>
                            schedule.Status == ScheduleStatus.Running ||
                            (schedule.Status == ScheduleStatus.Created &&
                                schedule.FinishedAt == null))
                        .OrderByDescending(schedule => schedule.Status == ScheduleStatus.Running)
                        .ThenBy(schedule => schedule.StartAt)
                        .Select(schedule => new BackupScheduleDto
                        {
                            Id = schedule.Id,
                            CreatedAt = schedule.CreatedAt,
                            BackupId = schedule.BackupId,
                            FinishedAt = schedule.FinishedAt,
                            Status = schedule.Status,
                            StartAt = schedule.StartAt,
                            Interval = schedule.Interval,
                            ErrorMessage = schedule.ErrorMessage
                        })
                        .FirstOrDefault(),
                    LatestFinishedSchedule = x.Schedules
                        .Where(schedule => schedule.FinishedAt != null)
                        .OrderByDescending(schedule => schedule.FinishedAt)
                        .ThenByDescending(schedule => schedule.Id)
                        .Select(schedule => new BackupScheduleDto
                        {
                            Id = schedule.Id,
                            CreatedAt = schedule.CreatedAt,
                            BackupId = schedule.BackupId,
                            FinishedAt = schedule.FinishedAt,
                            Status = schedule.Status,
                            StartAt = schedule.StartAt,
                            Interval = schedule.Interval,
                            ErrorMessage = schedule.ErrorMessage
                        })
                        .FirstOrDefault()
                })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
