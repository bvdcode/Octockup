// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.Abstractions;
using EasyExtensions.Crypto;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Octockup.Server.Abstractions;
using Octockup.Server.Database;
using Octockup.Server.Helpers;
using Octockup.Server.Hubs;
using Octockup.Server.Jobs;
using Octockup.Server.Models;
using Octockup.Server.Models.Enums;
using Octockup.Server.Services;
using System.Security.Cryptography;

namespace Octockup.Tests
{
    [Category("Integration")]
    public class BackupRunnerStateTests
    {
        private PostgresTestDatabase _database = null!;

        [OneTimeSetUp]
        public async Task CreateDatabaseAsync()
        {
            _database = await PostgresTestDatabase.CreateAsync();
        }

        [OneTimeTearDown]
        public async Task DropDatabaseAsync()
        {
            await _database.DisposeAsync();
        }

        [Test]
        public async Task RunAsync_WhenRetryCompletes_ClearsStaleError()
        {
            DateTime previousFinishedAt = DateTime.UtcNow.AddDays(-1);

            Schedule result = await RunBackupAsync(
                TestBackupStorage.EmptyMode,
                "Backup was canceled.",
                previousFinishedAt,
                CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(result.Status, Is.EqualTo(ScheduleStatus.Completed));
                Assert.That(result.ErrorMessage, Is.Null);
                Assert.That(result.FinishedAt, Is.GreaterThan(previousFinishedAt));
            });
        }

        [Test]
        public async Task RunAsync_WhenProviderThrowsOutOfMemory_ReportsFailureInsteadOfCancellation()
        {
            Schedule result = await RunBackupAsync(
                TestBackupStorage.OutOfMemoryMode,
                "Backup was canceled.",
                DateTime.UtcNow.AddDays(-1),
                CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(result.Status, Is.EqualTo(ScheduleStatus.Failed));
                Assert.That(result.ErrorMessage, Does.StartWith("Backup failed:"));
                Assert.That(result.ErrorMessage, Does.Contain("Synthetic out-of-memory failure."));
                Assert.That(result.ErrorMessage, Does.Not.Contain("canceled"));
            });
        }

        [Test]
        public async Task RunAsync_WhenCancellationIsRequested_ReportsCancellation()
        {
            using CancellationTokenSource cancellation = new();
            cancellation.Cancel();

            Schedule result = await RunBackupAsync(
                TestBackupStorage.EmptyMode,
                "Previous failure.",
                DateTime.UtcNow.AddDays(-1),
                cancellation.Token);

            Assert.Multiple(() =>
            {
                Assert.That(result.Status, Is.EqualTo(ScheduleStatus.Failed));
                Assert.That(result.ErrorMessage, Is.EqualTo("Backup was canceled."));
            });
        }

        private async Task<Schedule> RunBackupAsync(
            string sourceMode,
            string staleError,
            DateTime previousFinishedAt,
            CancellationToken cancellationToken)
        {
            DbContextOptions<PostgresDbContext> options = new DbContextOptionsBuilder<PostgresDbContext>()
                .UseNpgsql(_database.ConnectionString)
                .Options;
            await using PostgresDbContext dbContext = new(options);
            ServiceCollection services = new();
            services.AddLogging();
            services.AddSignalR();
            await using ServiceProvider serviceProvider = services.BuildServiceProvider();
            IStreamCipher crypto = new AesGcmStreamCipher(RandomNumberGenerator.GetBytes(32));
            string scenarioId = Guid.NewGuid().ToString("N");
            string providerId = typeof(TestBackupStorage).FullName!;
            User user = new()
            {
                Username = $"runner-state-{scenarioId}",
                PasswordPhc = "not-used",
            };
            Module source = new()
            {
                User = user,
                Tag = $"source-{scenarioId}",
                Destination = ModuleDestination.Source,
                BackupModuleId = providerId,
            };
            source.Params(crypto)["mode"] = sourceMode;
            Module storage = new()
            {
                User = user,
                Tag = $"storage-{scenarioId}",
                Destination = ModuleDestination.Target,
                BackupModuleId = providerId,
            };
            storage.Params(crypto)["mode"] = TestBackupStorage.EmptyMode;
            Backup backup = new()
            {
                Source = source,
                Storage = storage,
                Tag = $"backup-{scenarioId}",
                IgnoredPaths = [],
            };
            Schedule schedule = new()
            {
                Backup = backup,
                StartAt = DateTime.UtcNow,
                Status = ScheduleStatus.Failed,
                ErrorMessage = staleError,
                FinishedAt = previousFinishedAt,
            };

            dbContext.AddRange(user, source, storage, backup, schedule);
            await dbContext.SaveChangesAsync();

            BackupRunner runner = new(
                crypto,
                dbContext,
                serviceProvider,
                serviceProvider.GetRequiredService<ILogger<BackupRunner>>(),
                serviceProvider.GetRequiredService<IHubContext<EventHub>>(),
                [new TestBackupStorage()],
                new StorageOperationCoordinator());
            await runner.RunAsync(schedule, cancellationToken);
            dbContext.ChangeTracker.Clear();

            return await dbContext.Schedules
                .AsNoTracking()
                .SingleAsync(x => x.Id == schedule.Id);
        }
    }

    public sealed class TestBackupStorage : IBackupStorage
    {
        public const string EmptyMode = "empty";
        public const string OutOfMemoryMode = "out-of-memory";
        private string _mode = EmptyMode;

        public string Id => GetType().FullName!;
        public string Name => "Backup runner state test provider";
        public char PathSeparator => '/';
        public IEnumerable<string> RequiredParameters => ["mode"];

        public void SetParameters(IReadOnlyDictionary<string, string> parameters)
        {
            _mode = parameters["mode"];
        }

        public void SetIgnoredPaths(ICollection<string>? ignoredPaths)
        {
        }

        public Task<BackupFileInfo?> GetFileInfoAsync(string path, CancellationToken cancellationToken)
        {
            return Task.FromResult<BackupFileInfo?>(null);
        }

        public Task<Stream> GetFileStreamAsync(BackupFileInfo file, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Stream>(Stream.Null);
        }

        public IEnumerable<string> GetDirectories(bool recursive = false, CancellationToken cancellationToken = default)
        {
            return [];
        }

        public IEnumerable<BackupFileInfo> GetFiles(bool recursive = false, CancellationToken cancellationToken = default)
        {
            if (_mode == OutOfMemoryMode)
            {
                throw new OutOfMemoryException("Synthetic out-of-memory failure.");
            }

            return [];
        }

        public Task<bool?> ExistsAsync(string path, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<bool?>(false);
        }

        public Task<bool?> DeleteAsync(string path, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<bool?>(false);
        }

        public Task UploadAsync(string path, Stream data, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
