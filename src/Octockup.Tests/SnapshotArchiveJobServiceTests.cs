// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Octockup.Server.Database;
using Octockup.Server.Models.Dto;
using Octockup.Server.Models.Enums;
using Octockup.Server.Services;

namespace Octockup.Tests
{
    public class SnapshotArchiveJobServiceTests
    {
        private SqliteConnection _connection = null!;
        private SqliteDbContext _dbContext = null!;
        private SnapshotArchiveCancellationRegistry _cancellations = null!;
        private RecordingSnapshotArchiveProgressPublisher _publisher = null!;
        private SnapshotArchiveJobService _service = null!;
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
                Username = "archive-job-user",
                PasswordPhc = "password"
            };
            Module source = CreateModule(
                user,
                "archive-job-source",
                ModuleDestination.Source);
            Module storage = CreateModule(
                user,
                "archive-job-storage",
                ModuleDestination.Target);
            Backup backup = new()
            {
                UserId = user.Id,
                Source = source,
                Storage = storage,
                Tag = "archive-job-backup"
            };
            Snapshot snapshot = new()
            {
                Backup = backup,
                CompletedAt = DateTime.UtcNow,
                FilesCount = 123,
                TotalSize = 456_789
            };
            await _dbContext.AddRangeAsync(user, source, storage, backup, snapshot);
            await _dbContext.SaveChangesAsync();
            _dbContext.ChangeTracker.Clear();

            _userId = user.Id;
            _snapshotId = snapshot.Id;
            _cancellations = new SnapshotArchiveCancellationRegistry(
                NullLogger<SnapshotArchiveCancellationRegistry>.Instance);
            _publisher = new RecordingSnapshotArchiveProgressPublisher();
            _service = new SnapshotArchiveJobService(
                _dbContext,
                TimeProvider.System,
                _cancellations,
                _publisher,
                NullLogger<SnapshotArchiveJobService>.Instance);
        }

        [TearDown]
        public async Task TearDown()
        {
            await _dbContext.DisposeAsync();
            await _connection.DisposeAsync();
        }

        [Test]
        public async Task StartAsync_IsTenantScopedIdempotentAndListedBySnapshot()
        {
            SnapshotArchiveJobDto? first = await _service.StartAsync(
                _userId,
                _snapshotId,
                CancellationToken.None);
            SnapshotArchiveJobDto? duplicate = await _service.StartAsync(
                _userId,
                _snapshotId,
                CancellationToken.None);
            SnapshotArchiveJobDto? forbidden = await _service.StartAsync(
                Guid.NewGuid(),
                _snapshotId,
                CancellationToken.None);
            IReadOnlyList<SnapshotArchiveJobDto> jobs = await _service.GetForSnapshotsAsync(
                _userId,
                [_snapshotId],
                CancellationToken.None);
            IReadOnlyList<SnapshotArchiveJobDto> missing = await _service.GetForSnapshotsAsync(
                Guid.NewGuid(),
                [_snapshotId],
                CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(first, Is.Not.Null);
                Assert.That(duplicate?.JobId, Is.EqualTo(first?.JobId));
                Assert.That(first?.Status, Is.EqualTo(SnapshotArchiveStatus.Pending));
                Assert.That(first?.Phase, Is.EqualTo(SnapshotArchivePhase.Waiting));
                Assert.That(first?.TotalFiles, Is.EqualTo(123));
                Assert.That(first?.TotalBytes, Is.EqualTo(456_789));
                Assert.That(forbidden, Is.Null);
                Assert.That(jobs, Has.Count.EqualTo(1));
                Assert.That(missing, Is.Empty);
                Assert.That(_dbContext.SnapshotArchiveJobs.Count(), Is.EqualTo(1));
            });
        }

        [Test]
        public async Task ClaimUpdateAndFinalize_RequireMatchingRunId()
        {
            SnapshotArchiveJobDto started = (await _service.StartAsync(
                _userId,
                _snapshotId,
                CancellationToken.None))!;
            Guid runId = Guid.NewGuid();
            SnapshotArchiveJob? wrongOwner = await _service.ClaimAsync(
                Guid.NewGuid(),
                started.JobId,
                runId,
                CancellationToken.None);
            SnapshotArchiveJob claimed = (await _service.ClaimAsync(
                _userId,
                started.JobId,
                runId,
                CancellationToken.None))!;
            SnapshotArchiveJobDto progress = claimed.ToDto();
            progress.Phase = SnapshotArchivePhase.Streaming;
            progress.ProcessedFiles = 12;
            progress.ProcessedBytes = 34_567;
            progress.PreparedChunkReferences = 89;
            bool staleUpdate = await _service.UpdateProgressAsync(
                progress,
                Guid.NewGuid(),
                CancellationToken.None);
            bool update = await _service.UpdateProgressAsync(
                progress,
                runId,
                CancellationToken.None);
            SnapshotArchiveJobDto? staleFinalize = await _service.FinalizeAsync(
                progress,
                Guid.NewGuid(),
                SnapshotArchiveStatus.Completed,
                null);
            SnapshotArchiveJobDto? completed = await _service.FinalizeAsync(
                progress,
                runId,
                SnapshotArchiveStatus.Completed,
                null);

            Assert.Multiple(() =>
            {
                Assert.That(wrongOwner, Is.Null);
                Assert.That(claimed.Status, Is.EqualTo(SnapshotArchiveStatus.Running));
                Assert.That(claimed.Phase, Is.EqualTo(SnapshotArchivePhase.Preparing));
                Assert.That(staleUpdate, Is.False);
                Assert.That(update, Is.True);
                Assert.That(staleFinalize, Is.Null);
                Assert.That(completed?.Status, Is.EqualTo(SnapshotArchiveStatus.Completed));
                Assert.That(completed?.ProcessedFiles, Is.EqualTo(12));
                Assert.That(completed?.ProcessedBytes, Is.EqualTo(34_567));
                Assert.That(completed?.PreparedChunkReferences, Is.EqualTo(89));
                Assert.That(completed?.FinishedAt, Is.Not.Null);
                Assert.That(_publisher.Updates, Has.Count.EqualTo(2));
            });
        }

        [Test]
        public async Task CancelAsync_ImmediatelyPublishesAndSignalsRunningExecution()
        {
            SnapshotArchiveJobDto pending = (await _service.StartAsync(
                _userId,
                _snapshotId,
                CancellationToken.None))!;
            bool pendingCanceled = await _service.CancelAsync(
                _userId,
                pending.JobId,
                CancellationToken.None);

            SnapshotArchiveJobDto running = (await _service.StartAsync(
                _userId,
                _snapshotId,
                CancellationToken.None))!;
            Guid runId = Guid.NewGuid();
            await _service.ClaimAsync(
                _userId,
                running.JobId,
                runId,
                CancellationToken.None);
            using CancellationTokenSource executionCancellation = new();
            Assert.That(
                _cancellations.TryRegister(running.JobId, executionCancellation),
                Is.True);
            bool runningCanceled = await _service.CancelAsync(
                _userId,
                running.JobId,
                CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(pendingCanceled, Is.True);
                Assert.That(runningCanceled, Is.True);
                Assert.That(executionCancellation.IsCancellationRequested, Is.True);
                Assert.That(_publisher.Updates, Has.Count.EqualTo(2));
                Assert.That(_publisher.Updates[0].Status, Is.EqualTo(SnapshotArchiveStatus.Canceled));
                Assert.That(_publisher.Updates[1].CancellationRequested, Is.True);
            });
            _cancellations.Unregister(running.JobId, executionCancellation);
        }

        [Test]
        public async Task RecoverInterruptedAsync_FailsOnlyRunningJobs()
        {
            SnapshotArchiveJobDto running = (await _service.StartAsync(
                _userId,
                _snapshotId,
                CancellationToken.None))!;
            await _service.ClaimAsync(
                _userId,
                running.JobId,
                Guid.NewGuid(),
                CancellationToken.None);

            await _service.RecoverInterruptedAsync(CancellationToken.None);
            SnapshotArchiveJob recovered = await _dbContext.SnapshotArchiveJobs
                .AsNoTracking()
                .SingleAsync(x => x.Id == running.JobId);

            Assert.Multiple(() =>
            {
                Assert.That(recovered.Status, Is.EqualTo(SnapshotArchiveStatus.Failed));
                Assert.That(recovered.ActiveSnapshotId, Is.Null);
                Assert.That(recovered.RunId, Is.Null);
                Assert.That(recovered.FinishedAt, Is.Not.Null);
                Assert.That(recovered.ErrorMessage, Does.Contain("server restart"));
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
