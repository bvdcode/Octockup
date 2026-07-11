// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Octockup.Server.Abstractions;
using Octockup.Server.Database;
using Octockup.Server.Models.Dto;
using Octockup.Server.Models.Enums;
using Octockup.Server.Services;

namespace Octockup.Tests
{
    public class SnapshotArchiveExecutionServiceTests
    {
        private SqliteConnection _connection = null!;
        private SqliteDbContext _dbContext = null!;
        private SnapshotArchiveExecutionService _execution = null!;
        private RecordingOperationCoordinator _coordinator = null!;
        private Guid _jobId;
        private Guid _storageId;
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

            TestStorage storageProvider = new();
            User user = new()
            {
                Username = "archive-execution-user",
                PasswordPhc = "password"
            };
            Module source = CreateModule(
                user,
                "archive-execution-source",
                ModuleDestination.Source,
                "source-provider");
            Module storage = CreateModule(
                user,
                "archive-execution-storage",
                ModuleDestination.Target,
                storageProvider.Id);
            Backup backup = new()
            {
                UserId = user.Id,
                Source = source,
                Storage = storage,
                Tag = "archive-execution-backup"
            };
            Snapshot snapshot = new()
            {
                Backup = backup,
                CompletedAt = DateTime.UtcNow
            };
            await _dbContext.AddRangeAsync(user, source, storage, backup, snapshot);
            await _dbContext.SaveChangesAsync();

            SnapshotArchiveCancellationRegistry cancellations = new(
                NullLogger<SnapshotArchiveCancellationRegistry>.Instance);
            SnapshotArchiveJobService jobs = new(
                _dbContext,
                TimeProvider.System,
                cancellations,
                new RecordingSnapshotArchiveProgressPublisher(),
                NullLogger<SnapshotArchiveJobService>.Instance);
            SnapshotArchiveJobDto started = (await jobs.StartAsync(
                user.Id,
                snapshot.Id,
                CancellationToken.None))!;
            SnapshotChunkReferenceWriter referenceWriter = new(
                _dbContext,
                NullLogger<SnapshotChunkReferenceWriter>.Instance);
            SnapshotArchiveRunner runner = new(
                new TestCipher(),
                _dbContext,
                new SnapshotChunkReferenceIndexer(_dbContext, referenceWriter),
                NullLogger<SnapshotArchiveRunner>.Instance,
                new IBackupProvider[] { storageProvider });
            _coordinator = new RecordingOperationCoordinator();
            _execution = new SnapshotArchiveExecutionService(
                _dbContext,
                jobs,
                runner,
                cancellations,
                _coordinator,
                TimeProvider.System,
                NullLogger<SnapshotArchiveExecutionService>.Instance);
            _jobId = started.JobId;
            _storageId = storage.Id;
            _userId = user.Id;
            _dbContext.ChangeTracker.Clear();
        }

        [TearDown]
        public async Task TearDown()
        {
            await _dbContext.DisposeAsync();
            await _connection.DisposeAsync();
        }

        [Test]
        public async Task BeginAsync_HoldsRestoreLeaseUntilContextIsDisposed()
        {
            SnapshotArchiveRunContext? context = await _execution.BeginAsync(
                _userId,
                _jobId,
                CancellationToken.None);
            SnapshotArchiveJob persisted = await _dbContext.SnapshotArchiveJobs
                .AsNoTracking()
                .SingleAsync(x => x.Id == _jobId);

            Assert.Multiple(() =>
            {
                Assert.That(context, Is.Not.Null);
                Assert.That(_coordinator.RequestedStorageId, Is.EqualTo(_storageId));
                Assert.That(_coordinator.RequestedKind, Is.EqualTo(StorageOperationKind.Restore));
                Assert.That(_coordinator.Lease?.Disposed, Is.False);
                Assert.That(persisted.Status, Is.EqualTo(SnapshotArchiveStatus.Running));
            });

            await context!.DisposeAsync();
            Assert.That(_coordinator.Lease?.Disposed, Is.True);
        }

        [Test]
        public async Task BeginAsync_WhenStorageIsBusy_LeavesJobPending()
        {
            _coordinator.RejectAcquisition = true;

            SnapshotArchiveRunContext? context = await _execution.BeginAsync(
                _userId,
                _jobId,
                CancellationToken.None);
            SnapshotArchiveJob persisted = await _dbContext.SnapshotArchiveJobs
                .AsNoTracking()
                .SingleAsync(x => x.Id == _jobId);

            Assert.Multiple(() =>
            {
                Assert.That(context, Is.Null);
                Assert.That(persisted.Status, Is.EqualTo(SnapshotArchiveStatus.Pending));
                Assert.That(persisted.RunId, Is.Null);
                Assert.That(persisted.ActiveSnapshotId, Is.Not.Null);
            });
        }

        private static Module CreateModule(
            User user,
            string tag,
            ModuleDestination destination,
            string providerId)
        {
            return new Module
            {
                User = user,
                Tag = tag,
                BackupModuleId = providerId,
                Destination = destination
            };
        }

        private class RecordingOperationCoordinator : IStorageOperationCoordinator
        {
            public Guid? RequestedStorageId { get; private set; }
            public StorageOperationKind? RequestedKind { get; private set; }
            public RecordingLease? Lease { get; private set; }
            public bool RejectAcquisition { get; set; }

            public Task<IStorageOperationLease?> TryAcquireAsync(
                Guid storageId,
                StorageOperationKind kind,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                RequestedStorageId = storageId;
                RequestedKind = kind;
                if (RejectAcquisition)
                {
                    return Task.FromResult<IStorageOperationLease?>(null);
                }

                Lease = new RecordingLease(storageId);
                return Task.FromResult<IStorageOperationLease?>(Lease);
            }
        }

        private class RecordingLease(Guid storageId) : IStorageOperationLease
        {
            public Guid OperationId { get; } = Guid.NewGuid();
            public Guid StorageId { get; } = storageId;
            public CancellationToken LeaseLostToken => CancellationToken.None;
            public bool Disposed { get; private set; }

            public Task EnsureOwnedAsync(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            }

            public ValueTask DisposeAsync()
            {
                Disposed = true;
                return ValueTask.CompletedTask;
            }
        }
    }
}
