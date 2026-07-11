// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Octockup.Server.Database;
using Octockup.Server.Models.Dto;
using Octockup.Server.Models.Enums;
using Octockup.Server.Services;

namespace Octockup.Tests
{
    public class BackupListServiceTests
    {
        [Test]
        public async Task GetAsync_ReturnsCountsAndOnlyBoundedHistorySummaries()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            DbContextOptions<SqliteDbContext> options =
                new DbContextOptionsBuilder<SqliteDbContext>()
                    .UseSqlite(connection)
                    .Options;
            await using SqliteDbContext dbContext = new(options);
            await dbContext.Database.EnsureCreatedAsync();
            User user = new()
            {
                Username = "backup-list-user",
                PasswordPhc = "password"
            };
            Module source = CreateModule(user, "source", ModuleDestination.Source);
            Module storage = CreateModule(user, "storage", ModuleDestination.Target);
            Backup backup = new()
            {
                Source = source,
                Storage = storage,
                Tag = "bounded"
            };
            await dbContext.AddRangeAsync(user, source, storage);
            await dbContext.SaveChangesAsync();
            backup.UserId = user.Id;
            await dbContext.Backups.AddAsync(backup);
            await dbContext.SaveChangesAsync();

            DateTime baseline = DateTime.UtcNow.AddDays(-2);
            List<Snapshot> snapshots = new(125);
            List<Schedule> schedules = new(125);
            for (int index = 0; index < 125; index++)
            {
                snapshots.Add(new Snapshot
                {
                    BackupId = backup.Id,
                    CompletedAt = baseline.AddMinutes(index),
                    FilesCount = index + 1,
                    TotalSize = index + 10
                });
                schedules.Add(new Schedule
                {
                    BackupId = backup.Id,
                    StartAt = baseline.AddMinutes(index),
                    FinishedAt = baseline.AddMinutes(index + 1),
                    Status = index == 124
                        ? ScheduleStatus.Failed
                        : ScheduleStatus.Completed,
                    ErrorMessage = index == 124 ? "latest failure" : null
                });
            }

            Schedule active = new()
            {
                BackupId = backup.Id,
                StartAt = DateTime.UtcNow,
                Status = ScheduleStatus.Running
            };
            await dbContext.Snapshots.AddRangeAsync(snapshots);
            await dbContext.Schedules.AddRangeAsync(schedules);
            await dbContext.Schedules.AddAsync(active);
            await dbContext.SaveChangesAsync();
            dbContext.ChangeTracker.Clear();
            BackupListService service = new(dbContext);

            IReadOnlyList<BackupDto> result = await service.GetAsync(
                user.Id,
                CancellationToken.None);
            IReadOnlyList<BackupDto> forbidden = await service.GetAsync(
                Guid.NewGuid(),
                CancellationToken.None);
            BackupDto item = result.Single();

            Assert.Multiple(() =>
            {
                Assert.That(forbidden, Is.Empty);
                Assert.That(item.SnapshotCount, Is.EqualTo(125));
                Assert.That(item.CompletedSnapshotCount, Is.EqualTo(125));
                Assert.That(item.ScheduleCount, Is.EqualTo(126));
                Assert.That(item.LatestSnapshot?.Id, Is.EqualTo(snapshots[^1].Id));
                Assert.That(item.LatestSnapshot?.FilesCount, Is.EqualTo(125));
                Assert.That(item.ActiveSchedule?.Id, Is.EqualTo(active.Id));
                Assert.That(item.LatestFinishedSchedule?.Id, Is.EqualTo(schedules[^1].Id));
                Assert.That(item.LatestFinishedSchedule?.ErrorMessage, Is.EqualTo("latest failure"));
            });
        }

        private static Module CreateModule(
            User user,
            string tag,
            ModuleDestination destination)
        {
            return new Module
            {
                User = user,
                Tag = tag,
                BackupModuleId = tag + "-provider",
                Destination = destination
            };
        }
    }
}
