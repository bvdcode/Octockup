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
    public class SnapshotFilePageServiceTests
    {
        private SqliteConnection _connection = null!;
        private SqliteDbContext _dbContext = null!;
        private Guid _snapshotId;
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
                Username = "snapshot-page-user",
                PasswordPhc = "password"
            };
            Module source = CreateModule(user, "snapshot-page-source", ModuleDestination.Source);
            Module storage = CreateModule(user, "snapshot-page-storage", ModuleDestination.Target);
            Backup backup = new()
            {
                UserId = user.Id,
                Source = source,
                Storage = storage,
                Tag = "snapshot-page-backup"
            };
            Snapshot snapshot = new()
            {
                Backup = backup,
                CompletedAt = DateTime.UtcNow,
                FilesCount = 125,
                TotalSize = 125
            };
            await _dbContext.AddRangeAsync(user, source, storage, backup, snapshot);
            await _dbContext.SaveChangesAsync();

            List<SnapshotFile> files = new(125);
            for (int index = 0; index < 125; index++)
            {
                string directory = index < 5 ? "images" : "documents";
                string path = $"{directory}/{index:D4}.bin";
                files.Add(new SnapshotFile
                {
                    SnapshotId = snapshot.Id,
                    Path = path,
                    Name = Path.GetFileName(path),
                    Size = index + 1,
                    Hashsum = index.ToString("x64")
                });
            }

            await _dbContext.SnapshotFiles.AddRangeAsync(files);
            await _dbContext.SaveChangesAsync();
            _dbContext.ChangeTracker.Clear();
            _snapshotId = snapshot.Id;
            _userId = user.Id;
        }

        [TearDown]
        public async Task TearDown()
        {
            await _dbContext.DisposeAsync();
            await _connection.DisposeAsync();
        }

        [Test]
        public async Task GetPageAsync_UsesStableCursorSearchAndOwnershipBoundaries()
        {
            SnapshotFilePageService service = new(_dbContext);
            SnapshotFilePageDto first = (await service.GetPageAsync(
                _userId,
                _snapshotId,
                new SnapshotFilePageRequest { PageSize = 50 },
                CancellationToken.None))!;
            SnapshotFilePageDto second = (await service.GetPageAsync(
                _userId,
                _snapshotId,
                new SnapshotFilePageRequest
                {
                    PageSize = 50,
                    Cursor = first.NextCursor
                },
                CancellationToken.None))!;
            SnapshotFilePageDto third = (await service.GetPageAsync(
                _userId,
                _snapshotId,
                new SnapshotFilePageRequest
                {
                    PageSize = 50,
                    Cursor = second.NextCursor
                },
                CancellationToken.None))!;
            List<string> paths = first.Items
                .Concat(second.Items)
                .Concat(third.Items)
                .Select(x => x.Path)
                .ToList();
            SnapshotFilePageDto search = (await service.GetPageAsync(
                _userId,
                _snapshotId,
                new SnapshotFilePageRequest
                {
                    PageSize = 20,
                    Search = "  IMAGES/  "
                },
                CancellationToken.None))!;
            SnapshotFilePageDto? forbidden = await service.GetPageAsync(
                Guid.NewGuid(),
                _snapshotId,
                new SnapshotFilePageRequest(),
                CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(first.Items, Has.Count.EqualTo(50));
                Assert.That(second.Items, Has.Count.EqualTo(50));
                Assert.That(third.Items, Has.Count.EqualTo(25));
                Assert.That(first.TotalCount, Is.EqualTo(125));
                Assert.That(second.TotalCount, Is.EqualTo(125));
                Assert.That(third.HasNextPage, Is.False);
                Assert.That(third.NextCursor, Is.Null);
                Assert.That(paths, Has.Count.EqualTo(125));
                Assert.That(paths, Is.Ordered);
                Assert.That(paths.Distinct().Count(), Is.EqualTo(125));
                Assert.That(search.TotalCount, Is.EqualTo(5));
                Assert.That(search.Items.All(x => x.Path.StartsWith("images/")), Is.True);
                Assert.That(forbidden, Is.Null);
            });

            Assert.ThrowsAsync<FormatException>(async () => await service.GetPageAsync(
                _userId,
                _snapshotId,
                new SnapshotFilePageRequest { Cursor = "%%%" },
                CancellationToken.None));
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
