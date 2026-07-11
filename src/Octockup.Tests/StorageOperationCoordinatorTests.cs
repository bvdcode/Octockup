// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Octockup.Server.Abstractions;
using Octockup.Server.Database;
using Octockup.Server.Models.Enums;
using Octockup.Server.Services;

namespace Octockup.Tests
{
    public class StorageOperationCoordinatorTests
    {
        private SqliteConnection _anchorConnection = null!;
        private ServiceProvider _serviceProvider = null!;
        private Guid _storageId;

        [SetUp]
        public async Task Setup()
        {
            string databaseName = "storage-operation-" + Guid.NewGuid().ToString("N");
            string connectionString = $"Data Source={databaseName};Mode=Memory;Cache=Shared";
            _anchorConnection = new SqliteConnection(connectionString);
            await _anchorConnection.OpenAsync();

            ServiceCollection services = new();
            services.AddLogging();
            services.AddSingleton(TimeProvider.System);
            services.AddDbContext<AppDbContext, SqliteDbContext>(options =>
                options.UseSqlite(connectionString));
            services.AddSingleton<IStorageOperationCoordinator, StorageOperationCoordinator>();
            _serviceProvider = services.BuildServiceProvider();

            await using AsyncServiceScope scope = _serviceProvider.CreateAsyncScope();
            AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await dbContext.Database.EnsureCreatedAsync();

            User user = new()
            {
                Username = "lease-user",
                PasswordPhc = "password"
            };
            Module storage = new()
            {
                User = user,
                Tag = "lease-storage",
                BackupModuleId = "storage",
                Destination = ModuleDestination.Target
            };
            await dbContext.Modules.AddAsync(storage);
            await dbContext.SaveChangesAsync();
            _storageId = storage.Id;
        }

        [TearDown]
        public async Task TearDown()
        {
            await _serviceProvider.DisposeAsync();
            await _anchorConnection.DisposeAsync();
        }

        [Test]
        public async Task TryAcquireAsync_WhenStorageIsLeased_AllowsOnlyOneOperationUntilRelease()
        {
            IStorageOperationCoordinator coordinator = _serviceProvider
                .GetRequiredService<IStorageOperationCoordinator>();

            IStorageOperationLease? backupLease = await coordinator.TryAcquireAsync(
                _storageId,
                StorageOperationKind.Backup,
                CancellationToken.None);
            IStorageOperationLease? blockedCleanupLease = await coordinator.TryAcquireAsync(
                _storageId,
                StorageOperationKind.Cleanup,
                CancellationToken.None);

            Assert.That(backupLease, Is.Not.Null);
            Assert.That(blockedCleanupLease, Is.Null);

            await backupLease!.DisposeAsync();

            IStorageOperationLease? cleanupLease = await coordinator.TryAcquireAsync(
                _storageId,
                StorageOperationKind.Cleanup,
                CancellationToken.None);
            Assert.That(cleanupLease, Is.Not.Null);
            await cleanupLease!.DisposeAsync();

            Module storage = await LoadStorageAsync();
            Assert.Multiple(() =>
            {
                Assert.That(storage.ActiveStorageOperationId, Is.Null);
                Assert.That(storage.ActiveStorageOperationKind, Is.Null);
                Assert.That(storage.StorageOperationLeaseExpiresAt, Is.Null);
            });
        }

        [Test]
        public async Task TryAcquireAsync_WhenTwoBackupsTargetSameStorage_AllowsOnlyOneOwner()
        {
            IStorageOperationCoordinator coordinator = _serviceProvider
                .GetRequiredService<IStorageOperationCoordinator>();

            Task<IStorageOperationLease?> firstAttempt = coordinator.TryAcquireAsync(
                _storageId,
                StorageOperationKind.Backup,
                CancellationToken.None);
            Task<IStorageOperationLease?> secondAttempt = coordinator.TryAcquireAsync(
                _storageId,
                StorageOperationKind.Backup,
                CancellationToken.None);
            IStorageOperationLease?[] leases = await Task.WhenAll(
                firstAttempt,
                secondAttempt);

            Assert.That(leases.Count(x => x is not null), Is.EqualTo(1));
            foreach (IStorageOperationLease lease in leases.OfType<IStorageOperationLease>())
            {
                await lease.DisposeAsync();
            }
        }

        [Test]
        public async Task TryAcquireAsync_WhenLeaseExpired_FencesPreviousOwner()
        {
            IStorageOperationCoordinator coordinator = _serviceProvider
                .GetRequiredService<IStorageOperationCoordinator>();
            IStorageOperationLease firstLease = (await coordinator.TryAcquireAsync(
                _storageId,
                StorageOperationKind.Backup,
                CancellationToken.None))!;
            Guid firstOperationId = (await LoadStorageAsync()).ActiveStorageOperationId!.Value;

            await using (AsyncServiceScope scope = _serviceProvider.CreateAsyncScope())
            {
                AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await dbContext.Modules
                    .Where(x => x.Id == _storageId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(
                        x => x.StorageOperationLeaseExpiresAt,
                        DateTime.UtcNow.AddMinutes(-1)));
            }

            IStorageOperationLease secondLease = (await coordinator.TryAcquireAsync(
                _storageId,
                StorageOperationKind.Cleanup,
                CancellationToken.None))!;
            Guid secondOperationId = (await LoadStorageAsync()).ActiveStorageOperationId!.Value;

            Assert.That(secondOperationId, Is.Not.EqualTo(firstOperationId));
            Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await firstLease.EnsureOwnedAsync(CancellationToken.None));

            await firstLease.DisposeAsync();
            Assert.That(
                (await LoadStorageAsync()).ActiveStorageOperationId,
                Is.EqualTo(secondOperationId));

            await secondLease.DisposeAsync();
        }

        [Test]
        public async Task TryAcquireAsync_WhenCleanupJobAwaitsRecovery_BlocksBackupButAllowsCleanup()
        {
            await using (AsyncServiceScope scope = _serviceProvider.CreateAsyncScope())
            {
                AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                Module storage = await dbContext.Modules.SingleAsync(x => x.Id == _storageId);
                await dbContext.StorageCleanupJobs.AddAsync(new StorageCleanupJob
                {
                    UserId = storage.UserId,
                    StorageId = storage.Id,
                    ActiveStorageId = storage.Id,
                    StorageTag = storage.Tag,
                    Status = StorageCleanupStatus.Running,
                    Phase = StorageCleanupPhase.ScanningStorage,
                    StartedAt = DateTime.UtcNow
                });
                await dbContext.SaveChangesAsync();
            }

            IStorageOperationCoordinator coordinator = _serviceProvider
                .GetRequiredService<IStorageOperationCoordinator>();
            IStorageOperationLease? backupLease = await coordinator.TryAcquireAsync(
                _storageId,
                StorageOperationKind.Backup,
                CancellationToken.None);
            IStorageOperationLease? cleanupLease = await coordinator.TryAcquireAsync(
                _storageId,
                StorageOperationKind.Cleanup,
                CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(backupLease, Is.Null);
                Assert.That(cleanupLease, Is.Not.Null);
            });
            await cleanupLease!.DisposeAsync();
        }

        [Test]
        public async Task TryAcquireAsync_WhenArchiveIsStreaming_BlocksCleanupButAllowsBackup()
        {
            await using (AsyncServiceScope scope = _serviceProvider.CreateAsyncScope())
            {
                AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                Module storage = await dbContext.Modules.SingleAsync(x => x.Id == _storageId);
                Module source = new()
                {
                    UserId = storage.UserId,
                    Tag = "archive-source",
                    BackupModuleId = "archive-source-provider",
                    Destination = ModuleDestination.Source
                };
                Backup backup = new()
                {
                    UserId = storage.UserId,
                    Source = source,
                    StorageId = storage.Id,
                    Tag = "archive-backup"
                };
                Snapshot snapshot = new()
                {
                    Backup = backup,
                    CompletedAt = DateTime.UtcNow
                };
                await dbContext.AddRangeAsync(source, backup, snapshot);
                await dbContext.SaveChangesAsync();
                SnapshotArchiveJob archiveJob = new()
                {
                    UserId = storage.UserId,
                    SnapshotId = snapshot.Id,
                    ActiveSnapshotId = snapshot.Id,
                    RunId = Guid.NewGuid(),
                    Status = SnapshotArchiveStatus.Running,
                    Phase = SnapshotArchivePhase.Streaming,
                    StartedAt = DateTime.UtcNow
                };
                await dbContext.SnapshotArchiveJobs.AddAsync(archiveJob);
                await dbContext.SaveChangesAsync();
            }

            IStorageOperationCoordinator coordinator = _serviceProvider
                .GetRequiredService<IStorageOperationCoordinator>();
            IStorageOperationLease? cleanupLease = await coordinator.TryAcquireAsync(
                _storageId,
                StorageOperationKind.Cleanup,
                CancellationToken.None);
            IStorageOperationLease? backupLease = await coordinator.TryAcquireAsync(
                _storageId,
                StorageOperationKind.Backup,
                CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(cleanupLease, Is.Null);
                Assert.That(backupLease, Is.Not.Null);
            });
            await backupLease!.DisposeAsync();
        }

        private async Task<Module> LoadStorageAsync()
        {
            await using AsyncServiceScope scope = _serviceProvider.CreateAsyncScope();
            AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            return await dbContext.Modules
                .AsNoTracking()
                .SingleAsync(x => x.Id == _storageId);
        }
    }
}
