// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Octockup.Server.Database;
using Octockup.Server.Helpers;
using Octockup.Server.Models.Enums;
using Octockup.Server.Services;

namespace Octockup.Tests
{
    public class ScheduleSelectionTests
    {
        private SqliteConnection _connection = null!;
        private SqliteDbContext _dbContext = null!;
        private Guid _backupId;

        [SetUp]
        public async Task Setup()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            await _connection.OpenAsync();
            DbContextOptions<SqliteDbContext> options =
                new DbContextOptionsBuilder<SqliteDbContext>()
                    .UseSqlite(_connection)
                    .Options;
            _dbContext = new SqliteDbContext(options);
            await _dbContext.Database.EnsureCreatedAsync();
            User user = new()
            {
                Username = "schedule-user",
                PasswordPhc = "password"
            };
            Module source = CreateModule(user, "schedule-source", ModuleDestination.Source);
            Module storage = CreateModule(user, "schedule-storage", ModuleDestination.Target);
            await _dbContext.AddRangeAsync(user, source, storage);
            await _dbContext.SaveChangesAsync();
            Backup backup = new()
            {
                UserId = user.Id,
                SourceId = source.Id,
                StorageId = storage.Id,
                Tag = "schedule-backup"
            };
            await _dbContext.Backups.AddAsync(backup);
            await _dbContext.SaveChangesAsync();
            _backupId = backup.Id;
        }

        [TearDown]
        public async Task TearDown()
        {
            await _dbContext.DisposeAsync();
            await _connection.DisposeAsync();
        }

        [Test]
        public async Task GetReadySchedulesAsync_ReturnsBoundedDueSubsetInNextRunOrder()
        {
            DateTime now = DateTime.UtcNow;
            List<Schedule> futureSchedules = Enumerable.Range(0, 2_000)
                .Select(index => CreateSchedule(
                    now.AddDays(1).AddMinutes(index),
                    ScheduleStatus.Created,
                    null,
                    null))
                .ToList();
            Schedule firstDue = CreateSchedule(
                now.AddMinutes(-3),
                ScheduleStatus.Created,
                null,
                null);
            Schedule secondDue = CreateSchedule(
                now.AddMinutes(-2),
                ScheduleStatus.Created,
                null,
                null);
            Schedule thirdDue = CreateSchedule(
                now.AddMinutes(-1),
                ScheduleStatus.Created,
                null,
                null);
            await _dbContext.Schedules.AddRangeAsync(futureSchedules);
            await _dbContext.Schedules.AddRangeAsync(firstDue, secondDue, thirdDue);
            await _dbContext.SaveChangesAsync();
            _dbContext.ChangeTracker.Clear();

            IReadOnlyList<Schedule> ready = await ScheduleHelpers.GetReadySchedulesAsync(
                _dbContext.Schedules.AsNoTracking(),
                2,
                CancellationToken.None);

            Assert.That(
                ready.Select(x => x.Id),
                Is.EqualTo(new[] { firstDue.Id, secondDue.Id }));
        }

        [Test]
        public async Task ScheduleNextRunInitializer_RecoversOnlySchedulesThatCanRunAgain()
        {
            DateTime now = DateTime.UtcNow;
            Schedule created = CreateSchedule(
                null,
                ScheduleStatus.Created,
                null,
                null,
                now.AddHours(1));
            Schedule running = CreateSchedule(
                null,
                ScheduleStatus.Running,
                null,
                null,
                now.AddHours(-1));
            Schedule periodic = CreateSchedule(
                null,
                ScheduleStatus.Completed,
                now.AddMinutes(-30),
                TimeSpan.FromHours(1),
                now.AddHours(-2));
            Schedule completedOneShot = CreateSchedule(
                null,
                ScheduleStatus.Completed,
                now.AddMinutes(-10),
                null,
                now.AddHours(-2));
            await _dbContext.Schedules.AddRangeAsync(
                created,
                running,
                periodic,
                completedOneShot);
            await _dbContext.SaveChangesAsync();
            _dbContext.ChangeTracker.Clear();
            ScheduleNextRunInitializer initializer = new(
                _dbContext,
                NullLogger<ScheduleNextRunInitializer>.Instance);

            await initializer.InitializeAsync(CancellationToken.None);
            Dictionary<Guid, Schedule> schedules = await _dbContext.Schedules
                .AsNoTracking()
                .ToDictionaryAsync(x => x.Id);

            Assert.Multiple(() =>
            {
                Assert.That(schedules[created.Id].NextRunAt, Is.EqualTo(created.StartAt));
                Assert.That(schedules[running.Id].NextRunAt, Is.Not.Null);
                Assert.That(schedules[periodic.Id].NextRunAt,
                    Is.EqualTo(periodic.FinishedAt!.Value.Add(periodic.Interval!.Value)));
                Assert.That(schedules[completedOneShot.Id].NextRunAt, Is.Null);
            });
        }

        private Schedule CreateSchedule(
            DateTime? nextRunAt,
            ScheduleStatus status,
            DateTime? finishedAt,
            TimeSpan? interval,
            DateTime? startAt = null)
        {
            return new Schedule
            {
                BackupId = _backupId,
                StartAt = startAt ?? nextRunAt ?? DateTime.UtcNow,
                NextRunAt = nextRunAt,
                Status = status,
                FinishedAt = finishedAt,
                Interval = interval
            };
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
