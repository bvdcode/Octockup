// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.Abstractions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Octockup.Server.Abstractions;
using Octockup.Server.Database;
using Octockup.Server.Helpers;
using Octockup.Server.Models;
using Octockup.Server.Models.Dto;
using Octockup.Server.Models.Enums;
using Octockup.Server.Services;

namespace Octockup.Tests
{
    public class StorageCleanupJobExecutorTests
    {
        private const string ReferencedHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        private const string OrphanHash = "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";

        private SqliteConnection _anchorConnection = null!;
        private ServiceProvider _serviceProvider = null!;
        private TestStorage _storage = null!;
        private RecordingProgressPublisher _publisher = null!;
        private Guid _jobId;
        private Guid _storageId;

        [SetUp]
        public async Task Setup()
        {
            string databaseName = "cleanup-job-" + Guid.NewGuid().ToString("N");
            string connectionString = $"Data Source={databaseName};Mode=Memory;Cache=Shared";
            _anchorConnection = new SqliteConnection(connectionString);
            await _anchorConnection.OpenAsync();

            _storage = new TestStorage();
            _publisher = new RecordingProgressPublisher();
            ServiceCollection services = new();
            services.AddLogging();
            services.AddSingleton(TimeProvider.System);
            services.AddDbContext<AppDbContext, SqliteDbContext>(options =>
                options.UseSqlite(connectionString));
            services.AddSingleton<IStorageOperationCoordinator, StorageOperationCoordinator>();
            services.AddSingleton<StorageCleanupJobStore>();
            services.AddSingleton<StorageCleanupCancellationRegistry>();
            services.AddSingleton<IStorageCleanupProgressPublisher>(_publisher);
            services.AddSingleton(_storage);
            services.AddScoped<IBackupProvider>(provider => provider.GetRequiredService<TestStorage>());
            services.AddScoped<IStreamCipher, TestCipher>();
            services.AddScoped<ChunkReferenceCollector>();
            services.AddScoped<StorageCleanupRunner>();
            services.AddSingleton<StorageCleanupJobExecutor>();
            _serviceProvider = services.BuildServiceProvider();

            await using AsyncServiceScope scope = _serviceProvider.CreateAsyncScope();
            AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await dbContext.Database.EnsureCreatedAsync();
            (_jobId, _storageId) = await SeedJobAsync(dbContext, StorageCleanupStatus.Pending, false, null);
        }

        [TearDown]
        public async Task TearDown()
        {
            await _serviceProvider.DisposeAsync();
            await _anchorConnection.DisposeAsync();
        }

        [Test]
        public async Task ExecutePendingAsync_WhenJobPending_CompletesAndPersistsProgress()
        {
            string referencedPath = ChunkStorageHelpers.GetStoragePath(ReferencedHash, '/');
            string orphanPath = ChunkStorageHelpers.GetStoragePath(OrphanHash, '/');
            _storage.Files[referencedPath] = CreateStorageFile(referencedPath, 12);
            _storage.Files[orphanPath] = CreateStorageFile(orphanPath, 10);

            StorageCleanupJobExecutor executor = _serviceProvider
                .GetRequiredService<StorageCleanupJobExecutor>();
            await executor.ExecutePendingAsync(CancellationToken.None);

            StorageCleanupJob job = await LoadJobAsync();
            Module storageModule = await LoadStorageAsync();
            StorageCleanupJobManager manager = new(
                _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
                new RecordingJobScheduler(),
                _serviceProvider.GetRequiredService<StorageCleanupCancellationRegistry>(),
                NullLogger<StorageCleanupJobManager>.Instance);
            IReadOnlyList<StorageCleanupJobDto> persistedJobs = await manager
                .GetJobsAsync(job.UserId, CancellationToken.None);
            Assert.Multiple(() =>
            {
                Assert.That(job.Status, Is.EqualTo(StorageCleanupStatus.Completed));
                Assert.That(job.Phase, Is.EqualTo(StorageCleanupPhase.Completed));
                Assert.That(job.ActiveStorageId, Is.Null);
                Assert.That(job.RunId, Is.Null);
                Assert.That(job.FinishedAt, Is.Not.Null);
                Assert.That(job.ReferencedChunks, Is.EqualTo(1));
                Assert.That(job.OrphanObjects, Is.EqualTo(1));
                Assert.That(job.DeletedObjects, Is.EqualTo(1));
                Assert.That(job.FreedBytes, Is.EqualTo(10));
                Assert.That(job.UploadedHashRowsDeleted, Is.EqualTo(1));
                Assert.That(_storage.Files.ContainsKey(referencedPath), Is.True);
                Assert.That(_storage.Files.ContainsKey(orphanPath), Is.False);
                Assert.That(storageModule.ActiveStorageOperationId, Is.Null);
                Assert.That(_publisher.Updates.Last().Status, Is.EqualTo(StorageCleanupStatus.Completed));
                Assert.That(persistedJobs, Has.Count.EqualTo(1));
                Assert.That(persistedJobs[0].Status, Is.EqualTo(StorageCleanupStatus.Completed));
            });
        }

        [Test]
        public async Task ExecutePendingAsync_WhenPreviousRunWasInterrupted_RestartsAndCompletesJob()
        {
            await using (AsyncServiceScope scope = _serviceProvider.CreateAsyncScope())
            {
                AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                StorageCleanupJob job = await dbContext.StorageCleanupJobs.SingleAsync(x => x.Id == _jobId);
                job.Status = StorageCleanupStatus.Running;
                job.RunId = Guid.NewGuid();
                job.StorageObjectsScanned = 500;
                await dbContext.SaveChangesAsync();
            }

            string referencedPath = ChunkStorageHelpers.GetStoragePath(ReferencedHash, '/');
            _storage.Files[referencedPath] = CreateStorageFile(referencedPath, 12);
            StorageCleanupJobExecutor executor = _serviceProvider
                .GetRequiredService<StorageCleanupJobExecutor>();

            await executor.ExecutePendingAsync(CancellationToken.None);

            StorageCleanupJob recovered = await LoadJobAsync();
            Assert.Multiple(() =>
            {
                Assert.That(recovered.Status, Is.EqualTo(StorageCleanupStatus.Completed));
                Assert.That(recovered.StorageObjectsScanned, Is.EqualTo(1));
                Assert.That(recovered.RunId, Is.Null);
                Assert.That(recovered.ActiveStorageId, Is.Null);
            });
        }

        [Test]
        public async Task ExecutePendingAsync_WhenPendingCancellationRequested_FinalizesWithoutStorageScan()
        {
            await using (AsyncServiceScope scope = _serviceProvider.CreateAsyncScope())
            {
                AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                StorageCleanupJob job = await dbContext.StorageCleanupJobs.SingleAsync(x => x.Id == _jobId);
                job.CancellationRequested = true;
                await dbContext.SaveChangesAsync();
            }

            StorageCleanupJobExecutor executor = _serviceProvider
                .GetRequiredService<StorageCleanupJobExecutor>();
            await executor.ExecutePendingAsync(CancellationToken.None);

            StorageCleanupJob canceled = await LoadJobAsync();
            Assert.Multiple(() =>
            {
                Assert.That(canceled.Status, Is.EqualTo(StorageCleanupStatus.Canceled));
                Assert.That(canceled.Phase, Is.EqualTo(StorageCleanupPhase.Completed));
                Assert.That(canceled.ActiveStorageId, Is.Null);
                Assert.That(canceled.RunId, Is.Null);
                Assert.That(canceled.StorageObjectsScanned, Is.Zero);
                Assert.That(_publisher.Updates.Last().Status, Is.EqualTo(StorageCleanupStatus.Canceled));
            });
        }

        [Test]
        public async Task StartAsync_WhenActiveJobExists_ReturnsSameDurableJobAndTriggersQuartz()
        {
            StorageCleanupJob existing = await LoadJobAsync();
            RecordingJobScheduler scheduler = new();
            StorageCleanupJobManager manager = CreateManager(scheduler);

            StorageCleanupJobDto result = await manager.StartAsync(
                existing.UserId,
                existing.StorageId,
                CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(result.JobId, Is.EqualTo(existing.Id));
                Assert.That(result.Status, Is.EqualTo(StorageCleanupStatus.Pending));
                Assert.That(scheduler.TriggerCount, Is.EqualTo(1));
            });
        }

        [Test]
        public async Task StartAsync_WhenPreviousJobFinished_CreatesNewDurableJob()
        {
            StorageCleanupJob existing = await LoadJobAsync();
            await using (AsyncServiceScope scope = _serviceProvider.CreateAsyncScope())
            {
                AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                StorageCleanupJob tracked = await dbContext.StorageCleanupJobs
                    .SingleAsync(x => x.Id == existing.Id);
                tracked.ActiveStorageId = null;
                tracked.Status = StorageCleanupStatus.Completed;
                tracked.FinishedAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync();
            }

            RecordingJobScheduler scheduler = new();
            StorageCleanupJobManager manager = CreateManager(scheduler);
            StorageCleanupJobDto result = await manager.StartAsync(
                existing.UserId,
                existing.StorageId,
                CancellationToken.None);

            await using AsyncServiceScope verificationScope = _serviceProvider.CreateAsyncScope();
            AppDbContext verificationContext = verificationScope.ServiceProvider
                .GetRequiredService<AppDbContext>();
            List<StorageCleanupJob> jobs = await verificationContext.StorageCleanupJobs
                .AsNoTracking()
                .OrderBy(x => x.StartedAt)
                .ToListAsync();

            Assert.Multiple(() =>
            {
                Assert.That(result.JobId, Is.Not.EqualTo(existing.Id));
                Assert.That(result.Status, Is.EqualTo(StorageCleanupStatus.Pending));
                Assert.That(jobs, Has.Count.EqualTo(2));
                Assert.That(jobs.Single(x => x.Id == result.JobId).ActiveStorageId,
                    Is.EqualTo(existing.StorageId));
                Assert.That(scheduler.TriggerCount, Is.EqualTo(1));
            });
        }

        [Test]
        public async Task CancelAsync_WhenUserOwnsActiveJob_PersistsRequestAndTriggersQuartz()
        {
            StorageCleanupJob existing = await LoadJobAsync();
            RecordingJobScheduler scheduler = new();
            StorageCleanupJobManager manager = CreateManager(scheduler);

            bool canceled = await manager.CancelAsync(
                existing.UserId,
                existing.Id,
                CancellationToken.None);
            StorageCleanupJob persisted = await LoadJobAsync();

            Assert.Multiple(() =>
            {
                Assert.That(canceled, Is.True);
                Assert.That(persisted.CancellationRequested, Is.True);
                Assert.That(scheduler.TriggerCount, Is.EqualTo(1));
            });
        }

        [Test]
        public async Task CancelAsync_WhenJobBelongsToAnotherUser_DoesNotChangeJob()
        {
            StorageCleanupJob existing = await LoadJobAsync();
            RecordingJobScheduler scheduler = new();
            StorageCleanupJobManager manager = CreateManager(scheduler);

            bool canceled = await manager.CancelAsync(
                Guid.NewGuid(),
                existing.Id,
                CancellationToken.None);
            StorageCleanupJob persisted = await LoadJobAsync();

            Assert.Multiple(() =>
            {
                Assert.That(canceled, Is.False);
                Assert.That(persisted.CancellationRequested, Is.False);
                Assert.That(scheduler.TriggerCount, Is.Zero);
            });
        }

        private static BackupFileInfo CreateStorageFile(string path, long size)
        {
            return new BackupFileInfo
            {
                Path = path,
                Name = Path.GetFileName(path),
                Size = size
            };
        }

        private static async Task<(Guid JobId, Guid StorageId)> SeedJobAsync(
            AppDbContext dbContext,
            StorageCleanupStatus status,
            bool cancellationRequested,
            Guid? runId)
        {
            User user = new()
            {
                Username = "cleanup-user",
                PasswordPhc = "password"
            };
            Module source = new()
            {
                User = user,
                Tag = "cleanup-source",
                BackupModuleId = "source",
                Destination = ModuleDestination.Source
            };
            Module storage = new()
            {
                User = user,
                Tag = "cleanup-storage",
                BackupModuleId = typeof(TestStorage).FullName!,
                Destination = ModuleDestination.Target
            };
            Backup backup = new()
            {
                Source = source,
                Storage = storage,
                Tag = "cleanup-backup"
            };
            Snapshot snapshot = new()
            {
                Backup = backup,
                CompletedAt = DateTime.UtcNow,
                FilesCount = 1,
                TotalSize = 20
            };
            SnapshotFile snapshotFile = new()
            {
                Snapshot = snapshot,
                Path = "file.txt",
                Name = "file.txt",
                Size = 20,
                Hashsum = ReferencedHash,
                ChunkHashes = [ReferencedHash]
            };
            UploadedHash referencedHash = new()
            {
                Module = storage,
                Hash = ReferencedHash,
                OriginalSize = 20,
                StoredSize = 12,
                CompressionAlgorithm = CompressionHelpers.Algorithm
            };
            UploadedHash orphanHash = new()
            {
                Module = storage,
                Hash = OrphanHash,
                OriginalSize = 20,
                StoredSize = 10,
                CompressionAlgorithm = CompressionHelpers.Algorithm
            };
            await dbContext.AddRangeAsync(
                user,
                source,
                storage,
                backup,
                snapshot,
                snapshotFile,
                referencedHash,
                orphanHash);
            await dbContext.SaveChangesAsync();

            StorageCleanupJob job = new()
            {
                UserId = user.Id,
                StorageId = storage.Id,
                ActiveStorageId = storage.Id,
                RunId = runId,
                StorageTag = storage.Tag,
                Status = status,
                Phase = StorageCleanupPhase.Preparing,
                StartedAt = DateTime.UtcNow,
                CancellationRequested = cancellationRequested
            };
            await dbContext.StorageCleanupJobs.AddAsync(job);
            await dbContext.SaveChangesAsync();
            return (job.Id, storage.Id);
        }

        private async Task<StorageCleanupJob> LoadJobAsync()
        {
            await using AsyncServiceScope scope = _serviceProvider.CreateAsyncScope();
            AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            return await dbContext.StorageCleanupJobs
                .AsNoTracking()
                .SingleAsync(x => x.Id == _jobId);
        }

        private async Task<Module> LoadStorageAsync()
        {
            await using AsyncServiceScope scope = _serviceProvider.CreateAsyncScope();
            AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            return await dbContext.Modules
                .AsNoTracking()
                .SingleAsync(x => x.Id == _storageId);
        }

        private StorageCleanupJobManager CreateManager(IStorageCleanupJobScheduler scheduler)
        {
            return new StorageCleanupJobManager(
                _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
                scheduler,
                _serviceProvider.GetRequiredService<StorageCleanupCancellationRegistry>(),
                NullLogger<StorageCleanupJobManager>.Instance);
        }
    }
}
