// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.Models.Enums;
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
using Octockup.Server.Modules;
using Octockup.Server.Services;
using System.Security.Cryptography;

namespace Octockup.Tests
{
    [NonParallelizable]
    public class S3BackupStorageIntegrationTests
    {
        private S3BackupStorage? _storage;
        private Dictionary<string, string> _parameters = null!;

        [SetUp]
        public void Setup()
        {
            string basePath = Environment.GetEnvironmentVariable("OCTOCKUP_TEST_S3_PATH")
                ?.Trim('/') ?? "octockup-tests";
            _parameters = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["accessKey"] = GetRequiredEnvironmentVariable(
                    "OCTOCKUP_TEST_S3_ACCESS_KEY"),
                ["secretKey"] = GetRequiredEnvironmentVariable(
                    "OCTOCKUP_TEST_S3_SECRET_KEY"),
                ["bucket"] = GetRequiredEnvironmentVariable(
                    "OCTOCKUP_TEST_S3_BUCKET"),
                ["region"] = Environment.GetEnvironmentVariable(
                    "OCTOCKUP_TEST_S3_REGION") ?? "us-east-1",
                ["httpEndpoint"] = GetRequiredEnvironmentVariable(
                    "OCTOCKUP_TEST_S3_ENDPOINT"),
                ["path"] = $"{basePath}/{Guid.NewGuid():N}",
                ["validateChecksums"] = "false",
                ["useChunkEncoding"] = "false"
            };
            _storage = new S3BackupStorage(
                NullLogger<S3BackupStorage>.Instance);
            _storage.SetParameters(_parameters);
        }

        [TearDown]
        public async Task TearDown()
        {
            if (_storage is null)
            {
                return;
            }

            List<BackupFileInfo> remaining = [];
            await foreach (BackupFileInfo file in _storage.GetFilesAsync(
                recursive: true,
                CancellationToken.None))
            {
                remaining.Add(file);
            }

            foreach (BackupFileInfo file in remaining)
            {
                await _storage.DeleteAsync(file.Path, CancellationToken.None);
            }

            _storage.Dispose();
        }

        [Test]
        public async Task RoundTrip_UsesPagedInventoryAndDeletesObject()
        {
            S3BackupStorage storage = _storage!;
            byte[] content = RandomNumberGenerator.GetBytes(1024 * 1024 + 17);
            const string path = "roundtrip/payload.bin";
            using MemoryStream upload = new(content, writable: false);

            await storage.UploadAsync(path, upload, CancellationToken.None);
            bool? exists = await storage.ExistsAsync(path, CancellationToken.None);
            BackupFileInfo? info = await storage.GetFileInfoAsync(
                path,
                CancellationToken.None);
            List<BackupFileInfo> inventory = [];
            await foreach (BackupFileInfo file in storage.GetFilesAfterAsync(
                null,
                recursive: true,
                CancellationToken.None))
            {
                inventory.Add(file);
            }

            await using Stream download = await storage.GetFileStreamAsync(
                info!,
                CancellationToken.None);
            using MemoryStream restored = new();
            await download.CopyToAsync(restored);
            bool? deleted = await storage.DeleteAsync(path, CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(exists, Is.True);
                Assert.That(info, Is.Not.Null);
                Assert.That(info?.Size, Is.EqualTo(content.Length));
                Assert.That(inventory.Select(x => x.Path), Does.Contain(path));
                Assert.That(restored.ToArray(), Is.EqualTo(content));
                Assert.That(deleted, Is.True);
            });
        }

        [Test]
        public async Task CleanupAndBackup_StaySerializedAgainstLiveStorage()
        {
            string databaseName = "s3-integration-" + Guid.NewGuid().ToString("N");
            string connectionString = $"Data Source={databaseName};Mode=Memory;Cache=Shared";
            await using SqliteConnection anchorConnection = new(connectionString);
            await anchorConnection.OpenAsync();
            ServiceCollection services = new();
            services.AddLogging();
            services.AddSingleton(TimeProvider.System);
            services.AddDbContext<AppDbContext, SqliteDbContext>(options =>
                options.UseSqlite(connectionString));
            services.AddSingleton<IStorageOperationCoordinator, StorageOperationCoordinator>();
            await using ServiceProvider serviceProvider = services.BuildServiceProvider();
            await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();
            AppDbContext dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await dbContext.Database.EnsureCreatedAsync();
            TestCipher cipher = new();
            User user = new()
            {
                Username = "s3-live-user",
                PasswordPhc = "password"
            };
            Module storageModule = new()
            {
                User = user,
                Tag = "s3-live-storage",
                BackupModuleId = _storage!.Id,
                Destination = ModuleDestination.Target
            };
            await dbContext.AddRangeAsync(user, storageModule);
            await dbContext.SaveChangesAsync();
            foreach ((string key, string value) in _parameters)
            {
                storageModule.Params(cipher)[key] = value;
            }
            await dbContext.SaveChangesAsync();

            byte[] content = "live-orphan"u8.ToArray();
            string contentHash = Convert
                .ToHexString(SHA256.HashData(content))
                .ToLowerInvariant();
            string chunkKey = ChunkStorageHelpers.CreateKey(
                contentHash,
                CompressionAlgorithm.None,
                false);
            string chunkPath = ChunkStorageHelpers.GetStoragePath(
                chunkKey,
                _storage.PathSeparator);
            IStorageOperationCoordinator coordinator = serviceProvider
                .GetRequiredService<IStorageOperationCoordinator>();
            IStorageOperationLease backupLease = (await coordinator.TryAcquireAsync(
                storageModule.Id,
                StorageOperationKind.Backup,
                CancellationToken.None))!;
            using MemoryStream upload = new(content, writable: false);
            Task uploadTask = _storage.UploadAsync(
                chunkPath,
                upload,
                CancellationToken.None);
            IStorageOperationLease? blockedCleanup = await coordinator.TryAcquireAsync(
                storageModule.Id,
                StorageOperationKind.Cleanup,
                CancellationToken.None);
            await uploadTask;
            await dbContext.UploadedHashes.AddAsync(new UploadedHash
            {
                ModuleId = storageModule.Id,
                Hash = chunkKey,
                StoredSize = content.Length,
                OriginalSize = content.Length,
                CompressionAlgorithm = CompressionAlgorithm.None
            });
            await dbContext.SaveChangesAsync();
            await backupLease.DisposeAsync();

            IStorageOperationLease cleanupLease = (await coordinator.TryAcquireAsync(
                storageModule.Id,
                StorageOperationKind.Cleanup,
                CancellationToken.None))!;
            IStorageOperationLease? blockedBackup = await coordinator.TryAcquireAsync(
                storageModule.Id,
                StorageOperationKind.Backup,
                CancellationToken.None);
            SnapshotChunkReferenceWriter referenceWriter = new(
                dbContext,
                NullLogger<SnapshotChunkReferenceWriter>.Instance);
            StorageCleanupRunner runner = new(
                cipher,
                dbContext,
                NullLogger<StorageCleanupRunner>.Instance,
                new IBackupProvider[] { _storage },
                new SnapshotChunkReferenceIndexer(dbContext, referenceWriter));
            StorageCleanupJobState state = new(
                Guid.NewGuid(),
                user.Id,
                storageModule.Id,
                storageModule.Tag,
                DateTime.UtcNow);

            await runner.RunAsync(
                state,
                (_, _) => Task.CompletedTask,
                (_, _) => Task.CompletedTask,
                cleanupLease,
                CancellationToken.None);
            await cleanupLease.DisposeAsync();
            bool? existsAfterCleanup = await _storage.ExistsAsync(
                chunkPath,
                CancellationToken.None);
            StorageCleanupJobDto result = state.Snapshot();

            Assert.Multiple(() =>
            {
                Assert.That(blockedCleanup, Is.Null);
                Assert.That(blockedBackup, Is.Null);
                Assert.That(result.OrphanObjects, Is.EqualTo(1));
                Assert.That(result.DeletedObjects, Is.EqualTo(1));
                Assert.That(result.FailedDeletes, Is.Zero);
                Assert.That(existsAfterCleanup, Is.False);
                Assert.That(dbContext.UploadedHashes.Count(), Is.Zero);
            });
        }

        private static string GetRequiredEnvironmentVariable(string name)
        {
            string? value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrWhiteSpace(value))
            {
                Assert.Ignore($"{name} is not configured.");
            }

            return value;
        }
    }
}
