// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.Abstractions;
using EasyExtensions.Models.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Octockup.Server.Abstractions;
using Octockup.Server.Controllers;
using Octockup.Server.Database;
using Octockup.Server.Helpers;
using Octockup.Server.Models.Dto;
using Octockup.Server.Models.Enums;
using Octockup.Server.Models.Options;
using Octockup.Server.Services;
using System.Security.Cryptography;

namespace Octockup.Tests
{
    public class SnapshotDownloadLeaseTests
    {
        private SqliteConnection _connection = null!;
        private SqliteDbContext _dbContext = null!;
        private DownloadTicketService _tickets = null!;
        private TestStorage _storageProvider = null!;
        private RecordingHttpResponseFeature _responseFeature = null!;
        private Guid _fileId;
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
            _storageProvider = new TestStorage();

            byte[] content = "leased-download"u8.ToArray();
            string contentHash = Convert
                .ToHexString(SHA256.HashData(content))
                .ToLowerInvariant();
            string chunkKey = ChunkStorageHelpers.CreateKey(
                contentHash,
                CompressionAlgorithm.None,
                false);
            string storagePath = ChunkStorageHelpers.GetStoragePath(
                chunkKey,
                _storageProvider.PathSeparator);
            _storageProvider.Files[storagePath] = new Octockup.Server.Models.BackupFileInfo
            {
                Path = storagePath,
                Name = chunkKey,
                Size = content.Length
            };
            _storageProvider.Contents[storagePath] = content;

            User user = new()
            {
                Username = "download-lease-user",
                PasswordPhc = "password"
            };
            Module source = CreateModule(
                user,
                "download-source",
                ModuleDestination.Source,
                "source-provider");
            Module storage = CreateModule(
                user,
                "download-storage",
                ModuleDestination.Target,
                _storageProvider.Id);
            Backup backup = new()
            {
                UserId = user.Id,
                Source = source,
                Storage = storage,
                Tag = "download-backup"
            };
            Snapshot snapshot = new()
            {
                Backup = backup,
                CompletedAt = DateTime.UtcNow
            };
            SnapshotFile file = new()
            {
                Snapshot = snapshot,
                Path = "folder/file.txt",
                Name = "file.txt",
                Size = content.Length,
                Hashsum = contentHash,
                ChunkHashes = [chunkKey]
            };
            UploadedHash uploadedHash = new()
            {
                Module = storage,
                Hash = chunkKey,
                StoredSize = content.Length,
                OriginalSize = content.Length,
                CompressionAlgorithm = CompressionAlgorithm.None
            };
            await _dbContext.AddRangeAsync(
                user,
                source,
                storage,
                backup,
                snapshot,
                file,
                uploadedHash);
            await _dbContext.SaveChangesAsync();

            _tickets = new DownloadTicketService(
                _dbContext,
                TimeProvider.System,
                Options.Create(new DownloadTicketOptions
                {
                    Lifetime = TimeSpan.FromMinutes(2)
                }));
            _fileId = file.Id;
            _snapshotId = snapshot.Id;
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
        public async Task DownloadSnapshotFile_HoldsRestoreLeaseUntilResponseCompletes()
        {
            RecordingOperationCoordinator coordinator = new();
            SnapshotController controller = CreateController(coordinator);
            DownloadTicketDto ticket = (await _tickets.CreateSnapshotFileAsync(
                _userId,
                _snapshotId,
                _fileId,
                CancellationToken.None))!;

            IActionResult result = await controller.DownloadSnapshotFile(
                _snapshotId,
                _fileId,
                ticket.Ticket);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.TypeOf<FileStreamResult>());
                Assert.That(coordinator.RequestedKind, Is.EqualTo(StorageOperationKind.Restore));
                Assert.That(coordinator.Lease?.Disposed, Is.False);
            });

            await _responseFeature.CompleteAsync();
            Assert.That(coordinator.Lease?.Disposed, Is.True);
        }

        [Test]
        public async Task DownloadSnapshotFile_WhenStorageIsBusy_ReturnsConflict()
        {
            RecordingOperationCoordinator coordinator = new()
            {
                RejectAcquisition = true
            };
            SnapshotController controller = CreateController(coordinator);
            DownloadTicketDto ticket = (await _tickets.CreateSnapshotFileAsync(
                _userId,
                _snapshotId,
                _fileId,
                CancellationToken.None))!;

            IActionResult result = await controller.DownloadSnapshotFile(
                _snapshotId,
                _fileId,
                ticket.Ticket);

            Assert.That(result, Is.TypeOf<ConflictObjectResult>());
        }

        private SnapshotController CreateController(
            RecordingOperationCoordinator coordinator)
        {
            SnapshotController controller = new(
                new TestCipher(),
                _dbContext,
                new SnapshotDeletionService(_dbContext, coordinator),
                new SnapshotPageService(_dbContext),
                new SnapshotFilePageService(_dbContext),
                _tickets,
                coordinator,
                NullLogger<SnapshotController>.Instance,
                new IBackupProvider[] { _storageProvider });
            DefaultHttpContext httpContext = new();
            _responseFeature = new RecordingHttpResponseFeature();
            httpContext.Features.Set<IHttpResponseFeature>(_responseFeature);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };
            return controller;
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
            public StorageOperationKind? RequestedKind { get; private set; }
            public RecordingLease? Lease { get; private set; }
            public bool RejectAcquisition { get; init; }

            public Task<IStorageOperationLease?> TryAcquireAsync(
                Guid storageId,
                StorageOperationKind kind,
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
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

        private class RecordingHttpResponseFeature : IHttpResponseFeature
        {
            private readonly List<(Func<object, Task> Callback, object State)> _completed = [];

            public int StatusCode { get; set; } = StatusCodes.Status200OK;
            public string? ReasonPhrase { get; set; }
            public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
            public Stream Body { get; set; } = new MemoryStream();
            public bool HasStarted => false;

            public void OnStarting(Func<object, Task> callback, object state)
            {
            }

            public void OnCompleted(Func<object, Task> callback, object state)
            {
                _completed.Add((callback, state));
            }

            public async Task CompleteAsync()
            {
                for (int index = _completed.Count - 1; index >= 0; index--)
                {
                    (Func<object, Task> callback, object state) = _completed[index];
                    await callback(state);
                }
            }
        }
    }
}
