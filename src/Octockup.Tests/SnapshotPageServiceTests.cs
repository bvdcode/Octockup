// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Octockup.Server.Database;
using Octockup.Server.Models.Dto;
using Octockup.Server.Models.Enums;
using Octockup.Server.Models.Requests;
using Octockup.Server.Services;

namespace Octockup.Tests
{
    public class SnapshotPageServiceTests
    {
        private SqliteConnection _connection = null!;
        private SqliteDbContext _dbContext = null!;
        private Guid _backupId;
        private Guid _userId;

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
                Username = "snapshot-list-user",
                PasswordPhc = "password"
            };
            Module source = CreateModule(user, "source", ModuleDestination.Source);
            Module storage = CreateModule(user, "storage", ModuleDestination.Target);
            await _dbContext.AddRangeAsync(user, source, storage);
            await _dbContext.SaveChangesAsync();
            Backup backup = new()
            {
                UserId = user.Id,
                Source = source,
                Storage = storage,
                Tag = "snapshot-list-backup"
            };
            await _dbContext.Backups.AddAsync(backup);
            await _dbContext.SaveChangesAsync();

            DateTime baseline = DateTime.UtcNow.AddDays(-2);
            List<Snapshot> snapshots = new(125);
            for (int index = 0; index < 125; index++)
            {
                snapshots.Add(new Snapshot
                {
                    BackupId = backup.Id,
                    CompletedAt = baseline.AddMinutes(index),
                    FilesCount = index + 1,
                    TotalSize = index + 10
                });
            }

            snapshots.Add(new Snapshot
            {
                BackupId = backup.Id,
                FilesCount = 126,
                TotalSize = 136
            });
            snapshots.Add(new Snapshot
            {
                BackupId = backup.Id,
                FilesCount = 127,
                TotalSize = 137
            });

            await _dbContext.Snapshots.AddRangeAsync(snapshots);
            await _dbContext.SaveChangesAsync();
            _dbContext.ChangeTracker.Clear();
            _backupId = backup.Id;
            _userId = user.Id;
        }

        [TearDown]
        public async Task TearDown()
        {
            await _dbContext.DisposeAsync();
            await _connection.DisposeAsync();
        }

        [Test]
        public async Task GetPageAsync_UsesStableDescendingCursorAndOwnershipBoundary()
        {
            SnapshotPageService service = new(_dbContext);
            SnapshotPageDto first = (await service.GetPageAsync(
                _userId,
                _backupId,
                new SnapshotPageRequest { PageSize = 50 },
                CancellationToken.None))!;
            SnapshotPageDto second = (await service.GetPageAsync(
                _userId,
                _backupId,
                new SnapshotPageRequest
                {
                    PageSize = 50,
                    Cursor = first.NextCursor
                },
                CancellationToken.None))!;
            SnapshotPageDto third = (await service.GetPageAsync(
                _userId,
                _backupId,
                new SnapshotPageRequest
                {
                    PageSize = 50,
                    Cursor = second.NextCursor
                },
                CancellationToken.None))!;
            List<SnapshotDto> rows = first.Items
                .Concat(second.Items)
                .Concat(third.Items)
                .ToList();
            SnapshotPageDto? forbidden = await service.GetPageAsync(
                Guid.NewGuid(),
                _backupId,
                new SnapshotPageRequest(),
                CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(first.Items, Has.Count.EqualTo(50));
                Assert.That(second.Items, Has.Count.EqualTo(50));
                Assert.That(third.Items, Has.Count.EqualTo(27));
                Assert.That(first.TotalCount, Is.EqualTo(127));
                Assert.That(second.TotalCount, Is.EqualTo(127));
                Assert.That(third.HasNextPage, Is.False);
                Assert.That(third.NextCursor, Is.Null);
                Assert.That(rows, Has.Count.EqualTo(127));
                Assert.That(rows.Select(x => x.Id).Distinct().Count(), Is.EqualTo(127));
                Assert.That(rows.Select(x => x.CompletedAt), Is.Ordered.Descending);
                Assert.That(forbidden, Is.Null);
            });

            Assert.ThrowsAsync<FormatException>(async () => await service.GetPageAsync(
                _userId,
                _backupId,
                new SnapshotPageRequest { Cursor = "%%%" },
                CancellationToken.None));
        }

        [Test]
        public async Task GetPageAsync_ContinuesAfterIncompleteSnapshotCursor()
        {
            SnapshotPageService service = new(_dbContext);
            SnapshotPageDto first = (await service.GetPageAsync(
                _userId,
                _backupId,
                new SnapshotPageRequest { PageSize = 126 },
                CancellationToken.None))!;
            SnapshotPageDto second = (await service.GetPageAsync(
                _userId,
                _backupId,
                new SnapshotPageRequest
                {
                    PageSize = 126,
                    Cursor = first.NextCursor
                },
                CancellationToken.None))!;

            Assert.Multiple(() =>
            {
                Assert.That(first.Items, Has.Count.EqualTo(126));
                Assert.That(first.Items[^1].CompletedAt, Is.Null);
                Assert.That(first.HasNextPage, Is.True);
                Assert.That(second.Items, Has.Count.EqualTo(1));
                Assert.That(second.Items[0].CompletedAt, Is.Null);
                Assert.That(second.HasNextPage, Is.False);
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
