// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Octockup.Server.Database;
using Octockup.Server.Models.Dto;
using Octockup.Server.Models.Enums;
using Octockup.Server.Models.Options;
using Octockup.Server.Models.Results;
using Octockup.Server.Services;

namespace Octockup.Tests
{
    public class DownloadTicketServiceTests
    {
        private SqliteConnection _connection = null!;
        private SqliteDbContext _dbContext = null!;
        private DownloadTicketService _service = null!;
        private Guid _firstUserId;
        private Guid _secondUserId;
        private Guid _snapshotId;
        private Guid _snapshotFileId;
        private Guid _archiveJobId;

        [SetUp]
        public async Task Setup()
        {
            _connection = new SqliteConnection("Data Source=:memory:");
            await _connection.OpenAsync();
            DbContextOptions<SqliteDbContext> dbOptions =
                new DbContextOptionsBuilder<SqliteDbContext>()
                    .UseSqlite(_connection)
                    .Options;
            _dbContext = new SqliteDbContext(dbOptions);
            await _dbContext.Database.EnsureCreatedAsync();

            (_firstUserId, _snapshotId, _snapshotFileId) = await SeedSnapshotAsync(
                "first",
                DateTime.UtcNow);
            (_secondUserId, _, _) = await SeedSnapshotAsync("second", DateTime.UtcNow);
            SnapshotArchiveJob archiveJob = new()
            {
                UserId = _firstUserId,
                SnapshotId = _snapshotId,
                ActiveSnapshotId = _snapshotId,
                Status = SnapshotArchiveStatus.Pending,
                Phase = SnapshotArchivePhase.Waiting,
                StartedAt = DateTime.UtcNow
            };
            await _dbContext.SnapshotArchiveJobs.AddAsync(archiveJob);
            await _dbContext.SaveChangesAsync();
            _archiveJobId = archiveJob.Id;
            _service = new DownloadTicketService(
                _dbContext,
                TimeProvider.System,
                Options.Create(new DownloadTicketOptions
                {
                    Lifetime = TimeSpan.FromMinutes(2)
                }));
        }

        [TearDown]
        public async Task TearDown()
        {
            await _dbContext.DisposeAsync();
            await _connection.DisposeAsync();
        }

        [Test]
        public async Task SnapshotArchiveJobTicket_IsHashedAndCanOnlyBeConsumedOnce()
        {
            DownloadTicketDto? issued = await _service.CreateSnapshotArchiveJobAsync(
                _firstUserId,
                _archiveJobId,
                CancellationToken.None);
            DownloadTicket persistedBefore = await _dbContext.DownloadTickets
                .AsNoTracking()
                .SingleAsync();

            DownloadTicketGrant? firstGrant = await _service.ConsumeSnapshotArchiveJobAsync(
                issued!.Ticket,
                _archiveJobId,
                CancellationToken.None);
            DownloadTicketGrant? secondGrant = await _service.ConsumeSnapshotArchiveJobAsync(
                issued.Ticket,
                _archiveJobId,
                CancellationToken.None);
            DownloadTicket persistedAfter = await _dbContext.DownloadTickets
                .AsNoTracking()
                .SingleAsync();

            Assert.Multiple(() =>
            {
                Assert.That(persistedBefore.TokenHash, Is.Not.EqualTo(issued.Ticket));
                Assert.That(persistedBefore.TokenHash, Has.Length.EqualTo(64));
                Assert.That(firstGrant?.UserId, Is.EqualTo(_firstUserId));
                Assert.That(secondGrant, Is.Null);
                Assert.That(persistedAfter.ConsumedAt, Is.Not.Null);
            });
        }

        [Test]
        public async Task SnapshotArchiveJobTicket_CannotBeIssuedForAnotherUser()
        {
            DownloadTicketDto? issued = await _service.CreateSnapshotArchiveJobAsync(
                _secondUserId,
                _archiveJobId,
                CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(issued, Is.Null);
                Assert.That(_dbContext.DownloadTickets, Is.Empty);
            });
        }

        [Test]
        public async Task SnapshotArchiveJobTicket_CannotBeIssuedAfterStreamingStarts()
        {
            await _dbContext.SnapshotArchiveJobs
                .Where(x => x.Id == _archiveJobId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Status, SnapshotArchiveStatus.Running)
                    .SetProperty(x => x.Phase, SnapshotArchivePhase.Streaming)
                    .SetProperty(x => x.RunId, Guid.NewGuid()));

            DownloadTicketDto? issued = await _service.CreateSnapshotArchiveJobAsync(
                _firstUserId,
                _archiveJobId,
                CancellationToken.None);

            Assert.That(issued, Is.Null);
        }

        [Test]
        public async Task SnapshotArchiveJobTicket_CannotAuthorizeFileDownload()
        {
            DownloadTicketDto? issued = await _service.CreateSnapshotArchiveJobAsync(
                _firstUserId,
                _archiveJobId,
                CancellationToken.None);

            DownloadTicketGrant? wrongPurposeGrant = await _service.ConsumeSnapshotFileAsync(
                issued!.Ticket,
                _snapshotId,
                _snapshotFileId,
                CancellationToken.None);
            DownloadTicketGrant? correctGrant = await _service.ConsumeSnapshotArchiveJobAsync(
                issued.Ticket,
                _archiveJobId,
                CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(wrongPurposeGrant, Is.Null);
                Assert.That(correctGrant?.UserId, Is.EqualTo(_firstUserId));
            });
        }

        [Test]
        public async Task SnapshotFileTicket_CannotAuthorizeAnotherFile()
        {
            DownloadTicketDto? issued = await _service.CreateSnapshotFileAsync(
                _firstUserId,
                _snapshotId,
                _snapshotFileId,
                CancellationToken.None);

            DownloadTicketGrant? wrongFileGrant = await _service.ConsumeSnapshotFileAsync(
                issued!.Ticket,
                _snapshotId,
                Guid.NewGuid(),
                CancellationToken.None);
            DownloadTicketGrant? correctGrant = await _service.ConsumeSnapshotFileAsync(
                issued.Ticket,
                _snapshotId,
                _snapshotFileId,
                CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(wrongFileGrant, Is.Null);
                Assert.That(correctGrant?.UserId, Is.EqualTo(_firstUserId));
            });
        }

        [Test]
        public async Task ExpiredTicket_CannotBeConsumed()
        {
            DownloadTicketDto? issued = await _service.CreateSnapshotArchiveJobAsync(
                _firstUserId,
                _archiveJobId,
                CancellationToken.None);
            DownloadTicket ticket = await _dbContext.DownloadTickets.SingleAsync();
            ticket.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
            await _dbContext.SaveChangesAsync();

            DownloadTicketGrant? grant = await _service.ConsumeSnapshotArchiveJobAsync(
                issued!.Ticket,
                _archiveJobId,
                CancellationToken.None);

            Assert.That(grant, Is.Null);
        }

        [Test]
        public async Task ServerBackupTicket_PreservesIncludeFilesGrant()
        {
            DownloadTicketDto issued = await _service.CreateServerBackupAsync(
                _firstUserId,
                true,
                CancellationToken.None);

            DownloadTicketGrant? grant = await _service.ConsumeServerBackupAsync(
                issued.Ticket,
                CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(grant?.UserId, Is.EqualTo(_firstUserId));
                Assert.That(grant?.IncludeFiles, Is.True);
            });
        }

        private async Task<(Guid UserId, Guid SnapshotId, Guid SnapshotFileId)> SeedSnapshotAsync(
            string prefix,
            DateTime? completedAt)
        {
            User user = new()
            {
                Username = prefix + "-user",
                PasswordPhc = "password"
            };
            Module source = new()
            {
                User = user,
                Tag = prefix + "-source",
                BackupModuleId = prefix + "-source-provider",
                Destination = ModuleDestination.Source
            };
            Module storage = new()
            {
                User = user,
                Tag = prefix + "-storage",
                BackupModuleId = prefix + "-storage-provider",
                Destination = ModuleDestination.Target
            };
            Backup backup = new()
            {
                Source = source,
                Storage = storage,
                Tag = prefix + "-backup"
            };
            Snapshot snapshot = new()
            {
                Backup = backup,
                CompletedAt = completedAt
            };
            SnapshotFile snapshotFile = new()
            {
                Snapshot = snapshot,
                Path = prefix + "/file.txt",
                Name = "file.txt",
                Size = 10,
                Hashsum = prefix + "-hash",
                ChunkHashes = []
            };

            await _dbContext.AddRangeAsync(
                user,
                source,
                storage,
                backup,
                snapshot,
                snapshotFile);
            await _dbContext.SaveChangesAsync();
            return (user.Id, snapshot.Id, snapshotFile.Id);
        }
    }
}
