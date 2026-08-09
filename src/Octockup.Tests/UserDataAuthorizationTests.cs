// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.Abstractions;
using EasyExtensions.Crypto;
using EasyExtensions.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Octockup.Server.Abstractions;
using Octockup.Server.Controllers;
using Octockup.Server.Database;
using Octockup.Server.Helpers;
using Octockup.Server.Models;
using Octockup.Server.Models.Dto;
using Octockup.Server.Models.Enums;
using Octockup.Server.Models.Requests;
using Quartz;
using System.Security.Claims;
using System.Security.Cryptography;

namespace Octockup.Tests
{
    [Category("Integration")]
    public class UserDataAuthorizationTests
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
        public async Task RenameModule_WhenOwnedByAnotherUser_ReturnsNotFoundAndDoesNotRename()
        {
            await using PostgresDbContext dbContext = CreateDbContext();
            Module module = await SeedStandaloneModuleAsync(dbContext);
            ModuleController controller = AsUser(
                new ModuleController(CreateCipher(), dbContext, NullLogger<ModuleController>.Instance, []),
                Guid.NewGuid());
            string originalTag = module.Tag;

            IActionResult result = await controller.RenameModule(
                module.Id,
                new RenameModuleRequest { NewTag = $"renamed-{Guid.NewGuid():N}" });

            AssertNotFound(result);
            dbContext.ChangeTracker.Clear();
            Module persisted = await dbContext.Modules.SingleAsync(x => x.Id == module.Id);
            Assert.That(persisted.Tag, Is.EqualTo(originalTag));
        }

        [Test]
        public async Task DeleteModule_WhenOwnedByAnotherUser_ReturnsNotFoundAndDoesNotDelete()
        {
            await using PostgresDbContext dbContext = CreateDbContext();
            Module module = await SeedStandaloneModuleAsync(dbContext);
            ModuleController controller = AsUser(
                new ModuleController(CreateCipher(), dbContext, NullLogger<ModuleController>.Instance, []),
                Guid.NewGuid());

            IActionResult result = await controller.DeleteUserBackupStorage(module.Id);

            AssertNotFound(result);
            dbContext.ChangeTracker.Clear();
            Assert.That(await dbContext.Modules.AnyAsync(x => x.Id == module.Id), Is.True);
        }

        [Test]
        public async Task RenameModule_WhenOwnedByCurrentUser_RenamesModule()
        {
            await using PostgresDbContext dbContext = CreateDbContext();
            Module module = await SeedStandaloneModuleAsync(dbContext);
            ModuleController controller = AsUser(
                new ModuleController(CreateCipher(), dbContext, NullLogger<ModuleController>.Instance, []),
                module.UserId);
            string newTag = $"renamed-{Guid.NewGuid():N}";

            IActionResult result = await controller.RenameModule(
                module.Id,
                new RenameModuleRequest { NewTag = newTag });

            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            dbContext.ChangeTracker.Clear();
            Module persisted = await dbContext.Modules.SingleAsync(x => x.Id == module.Id);
            Assert.That(persisted.Tag, Is.EqualTo(newTag));
        }

        [Test]
        public async Task DeleteModule_WhenCleanupCompleted_RemovesCleanupState()
        {
            await using PostgresDbContext dbContext = CreateDbContext();
            Module module = await SeedStandaloneModuleAsync(dbContext);
            StorageCleanup cleanup = new()
            {
                ModuleId = module.Id,
                Status = StorageCleanupStatus.Completed,
            };
            StorageCleanupRun run = new()
            {
                ModuleId = module.Id,
                Status = StorageCleanupStatus.Completed,
                StartedAt = DateTime.UtcNow.AddMinutes(-1),
                CompletedAt = DateTime.UtcNow,
            };
            await dbContext.AddRangeAsync(cleanup, run);
            await dbContext.SaveChangesAsync();
            ModuleController controller = AsUser(
                new ModuleController(CreateCipher(), dbContext, NullLogger<ModuleController>.Instance, []),
                module.UserId);

            IActionResult result = await controller.DeleteUserBackupStorage(module.Id);

            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            dbContext.ChangeTracker.Clear();
            bool moduleExists = await dbContext.Modules.AnyAsync(x => x.Id == module.Id);
            bool cleanupExists = await dbContext.StorageCleanups.AnyAsync(x => x.ModuleId == module.Id);
            bool cleanupRunExists = await dbContext.StorageCleanupRuns.AnyAsync(x => x.ModuleId == module.Id);
            Assert.Multiple(() =>
            {
                Assert.That(moduleExists, Is.False);
                Assert.That(cleanupExists, Is.False);
                Assert.That(cleanupRunExists, Is.False);
            });
        }

        [Test]
        public async Task DeleteModule_WhenCleanupRunning_ReturnsConflict()
        {
            await using PostgresDbContext dbContext = CreateDbContext();
            Module module = await SeedStandaloneModuleAsync(dbContext);
            StorageCleanup cleanup = new()
            {
                ModuleId = module.Id,
                Status = StorageCleanupStatus.Running,
            };
            await dbContext.StorageCleanups.AddAsync(cleanup);
            await dbContext.SaveChangesAsync();
            ModuleController controller = AsUser(
                new ModuleController(CreateCipher(), dbContext, NullLogger<ModuleController>.Instance, []),
                module.UserId);

            IActionResult result = await controller.DeleteUserBackupStorage(module.Id);

            ObjectResult conflict = result as ObjectResult
                ?? throw new AssertionException("Expected an object result.");
            Assert.That(conflict.StatusCode, Is.EqualTo(StatusCodes.Status409Conflict));
            dbContext.ChangeTracker.Clear();
            Assert.That(await dbContext.Modules.AnyAsync(x => x.Id == module.Id), Is.True);
        }

        [Test]
        public async Task DeleteBackup_WhenOwnedByAnotherUser_ReturnsNotFoundAndDoesNotDelete()
        {
            await using PostgresDbContext dbContext = CreateDbContext();
            OwnedGraph graph = await SeedOwnedGraphAsync(dbContext);
            BackupController controller = AsUser(
                new BackupController(
                    dbContext,
                    CreateCipher(),
                    new UnexpectedSchedulerFactory(),
                    NullLogger<BackupController>.Instance),
                Guid.NewGuid());

            IActionResult result = await controller.DeleteBackup(graph.Backup.Id);

            AssertNotFound(result);
            dbContext.ChangeTracker.Clear();
            Assert.That(await dbContext.Backups.AnyAsync(x => x.Id == graph.Backup.Id), Is.True);
        }

        [Test]
        public async Task UpdateBackup_WhenOwnedByAnotherUser_ReturnsNotFoundAndDoesNotMutate()
        {
            await using PostgresDbContext dbContext = CreateDbContext();
            OwnedGraph graph = await SeedOwnedGraphAsync(dbContext);
            BackupController controller = AsUser(
                new BackupController(
                    dbContext,
                    CreateCipher(),
                    new UnexpectedSchedulerFactory(),
                    NullLogger<BackupController>.Instance),
                Guid.NewGuid());
            string originalTag = graph.Backup.Tag;

            IActionResult renameResult = await controller.RenameBackup(
                graph.Backup.Id,
                new RenameModuleRequest { NewTag = $"renamed-{Guid.NewGuid():N}" });
            IActionResult ignoredPathsResult = await controller.UpdateIgnoredPaths(
                graph.Backup.Id,
                ["private/path"]);

            AssertNotFound(renameResult);
            AssertNotFound(ignoredPathsResult);
            dbContext.ChangeTracker.Clear();
            Backup persisted = await dbContext.Backups.SingleAsync(x => x.Id == graph.Backup.Id);
            Assert.Multiple(() =>
            {
                Assert.That(persisted.Tag, Is.EqualTo(originalTag));
                Assert.That(persisted.IgnoredPaths, Is.Empty);
            });
        }

        [Test]
        public async Task CancelSchedule_WhenOwnedByAnotherUser_ReturnsNotFoundBeforeUsingScheduler()
        {
            await using PostgresDbContext dbContext = CreateDbContext();
            OwnedGraph graph = await SeedOwnedGraphAsync(dbContext, includeSchedule: true);
            ScheduleController controller = AsUser(
                new ScheduleController(dbContext, new UnexpectedSchedulerFactory()),
                Guid.NewGuid());

            IActionResult result = await controller.CancelSchedule(graph.Schedule!.Id);

            AssertNotFound(result);
        }

        [Test]
        public async Task DeleteBackup_WhenOwnedByCurrentUser_DeletesDependentMetadata()
        {
            await using PostgresDbContext dbContext = CreateDbContext();
            OwnedGraph graph = await SeedOwnedGraphAsync(
                dbContext,
                includeSchedule: true,
                includeSnapshot: true);
            BackupController controller = AsUser(
                new BackupController(
                    dbContext,
                    CreateCipher(),
                    new UnexpectedSchedulerFactory(),
                    NullLogger<BackupController>.Instance),
                graph.Source.UserId);

            IActionResult result = await controller.DeleteBackup(graph.Backup.Id);

            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            dbContext.ChangeTracker.Clear();
            bool backupExists = await dbContext.Backups.AnyAsync(x => x.Id == graph.Backup.Id);
            bool scheduleExists = await dbContext.Schedules.AnyAsync(x => x.Id == graph.Schedule!.Id);
            bool snapshotExists = await dbContext.Snapshots.AnyAsync(x => x.Id == graph.Snapshot!.Id);
            bool snapshotFileExists = await dbContext.SnapshotFiles.AnyAsync(x => x.Id == graph.SnapshotFile!.Id);
            bool sourceExists = await dbContext.Modules.AnyAsync(x => x.Id == graph.Source.Id);
            bool storageExists = await dbContext.Modules.AnyAsync(x => x.Id == graph.Storage.Id);

            Assert.Multiple(() =>
            {
                Assert.That(backupExists, Is.False);
                Assert.That(scheduleExists, Is.False);
                Assert.That(snapshotExists, Is.False);
                Assert.That(snapshotFileExists, Is.False);
                Assert.That(sourceExists, Is.True);
                Assert.That(storageExists, Is.True);
            });
        }

        [Test]
        public async Task DownloadSnapshotFile_WhenOwnedByAnotherUser_ReturnsNotFoundBeforeAccessingStorage()
        {
            await using PostgresDbContext dbContext = CreateDbContext();
            OwnedGraph graph = await SeedOwnedGraphAsync(dbContext, includeSnapshot: true);
            TestStorage storage = new(graph.Storage.BackupModuleId, failOnAccess: true);
            SnapshotController controller = AsUser(
                new SnapshotController(
                    CreateCipher(),
                    dbContext,
                    NullLogger<SnapshotController>.Instance,
                    [storage]),
                Guid.NewGuid());

            IActionResult result = await controller.DownloadSnapshotFile(
                graph.Snapshot!.Id,
                graph.SnapshotFile!.Id);

            AssertNotFound(result);
            Assert.That(storage.WasAccessed, Is.False);
        }

        [Test]
        public async Task DownloadSnapshotArchive_WhenOwnedByAnotherUser_ReturnsNotFoundBeforeAccessingStorage()
        {
            await using PostgresDbContext dbContext = CreateDbContext();
            OwnedGraph graph = await SeedOwnedGraphAsync(dbContext, includeSnapshot: true);
            TestStorage storage = new(graph.Storage.BackupModuleId, failOnAccess: true);
            SnapshotController controller = AsUser(
                new SnapshotController(
                    CreateCipher(),
                    dbContext,
                    NullLogger<SnapshotController>.Instance,
                    [storage]),
                Guid.NewGuid());

            IActionResult result = await controller.DownloadSnapshotArchive(
                graph.Snapshot!.Id,
                CancellationToken.None);

            AssertNotFound(result);
            Assert.That(storage.WasAccessed, Is.False);
        }

        [Test]
        public async Task DownloadSnapshotFile_PropagatesValidationModeToRestoreStream()
        {
            await using PostgresDbContext dbContext = CreateDbContext();
            OwnedGraph graph = await SeedOwnedGraphAsync(dbContext, includeSnapshot: true);
            byte[] storedContent = [1, 2, 3, 4, 5];
            byte[] expectedContent = [5, 4, 3, 2, 1];
            await ConfigureDownloadAsync(dbContext, graph, storedContent, expectedContent);
            TestStorage storage = new(graph.Storage.BackupModuleId, storedContent);
            SnapshotController controller = AsUser(
                new SnapshotController(
                    CreateCipher(),
                    dbContext,
                    NullLogger<SnapshotController>.Instance,
                    [storage]),
                graph.Source.UserId);

            IActionResult fastAction = await controller.DownloadSnapshotFile(
                graph.Snapshot!.Id,
                graph.SnapshotFile!.Id,
                validate: false);
            Assert.That(fastAction, Is.InstanceOf<FileStreamResult>());
            FileStreamResult fastResult = (FileStreamResult)fastAction;
            await using MemoryStream restored = new();
            await fastResult.FileStream.CopyToAsync(restored);
            await fastResult.FileStream.DisposeAsync();
            Assert.That(restored.ToArray(), Is.EqualTo(storedContent));

            IActionResult validatedAction = await controller.DownloadSnapshotFile(
                graph.Snapshot.Id,
                graph.SnapshotFile.Id,
                validate: true);
            Assert.That(validatedAction, Is.InstanceOf<FileStreamResult>());
            FileStreamResult validatedResult = (FileStreamResult)validatedAction;
            Assert.That(async () =>
            {
                await using Stream validatedStream = validatedResult.FileStream;
                await validatedStream.CopyToAsync(Stream.Null);
            }, Throws.TypeOf<InvalidDataException>());
        }

        [Test]
        public async Task DownloadSnapshotArchive_PropagatesValidationModeToEveryEntry()
        {
            await using PostgresDbContext dbContext = CreateDbContext();
            OwnedGraph graph = await SeedOwnedGraphAsync(dbContext, includeSnapshot: true);
            byte[] storedContent = [1, 2, 3, 4, 5];
            byte[] expectedContent = [5, 4, 3, 2, 1];
            await ConfigureDownloadAsync(dbContext, graph, storedContent, expectedContent);
            TestStorage storage = new(graph.Storage.BackupModuleId, storedContent);
            SnapshotController fastController = AsUser(
                new SnapshotController(
                    CreateCipher(),
                    dbContext,
                    NullLogger<SnapshotController>.Instance,
                    [storage]),
                graph.Source.UserId);
            fastController.Response.Body = new MemoryStream();

            IActionResult fastResult = await fastController.DownloadSnapshotArchive(
                graph.Snapshot!.Id,
                CancellationToken.None,
                validate: false);

            Assert.That(fastResult, Is.InstanceOf<EmptyResult>());

            SnapshotController validatedController = AsUser(
                new SnapshotController(
                    CreateCipher(),
                    dbContext,
                    NullLogger<SnapshotController>.Instance,
                    [storage]),
                graph.Source.UserId);
            validatedController.Response.Body = new MemoryStream();
            Assert.That(async () =>
            {
                await validatedController.DownloadSnapshotArchive(
                    graph.Snapshot.Id,
                    CancellationToken.None,
                    validate: true);
            }, Throws.TypeOf<InvalidDataException>());
        }

        [Test]
        public async Task GetSnapshotFiles_WhenOwnedByAnotherUser_ReturnsNotFound()
        {
            await using PostgresDbContext dbContext = CreateDbContext();
            OwnedGraph graph = await SeedOwnedGraphAsync(dbContext, includeSnapshot: true);
            SnapshotController controller = AsUser(
                new SnapshotController(
                    CreateCipher(),
                    dbContext,
                    NullLogger<SnapshotController>.Instance,
                    []),
                Guid.NewGuid());

            IActionResult result = controller.GetSnapshot(graph.Snapshot!.Id);

            AssertNotFound(result);
        }

        [Test]
        public async Task SnapshotLists_WhenOwnedByCurrentUser_ReturnOwnedData()
        {
            await using PostgresDbContext dbContext = CreateDbContext();
            OwnedGraph graph = await SeedOwnedGraphAsync(dbContext, includeSnapshot: true);
            SnapshotController controller = AsUser(
                new SnapshotController(
                    CreateCipher(),
                    dbContext,
                    NullLogger<SnapshotController>.Instance,
                    []),
                graph.Source.UserId);

            IActionResult filesResult = controller.GetSnapshot(graph.Snapshot!.Id);
            IActionResult snapshotsResult = controller.GetSnapshots(graph.Backup.Id);

            Assert.Multiple(() =>
            {
                Assert.That(filesResult, Is.InstanceOf<OkObjectResult>());
                Assert.That(snapshotsResult, Is.InstanceOf<OkObjectResult>());
            });
            OkObjectResult filesOk = (OkObjectResult)filesResult;
            OkObjectResult snapshotsOk = (OkObjectResult)snapshotsResult;
            Assert.Multiple(() =>
            {
                Assert.That(filesOk.Value, Is.InstanceOf<List<SnapshotFileDto>>());
                Assert.That((List<SnapshotFileDto>)filesOk.Value!, Has.Count.EqualTo(1));
                Assert.That(snapshotsOk.Value, Is.InstanceOf<List<SnapshotDto>>());
                Assert.That((List<SnapshotDto>)snapshotsOk.Value!, Has.Count.EqualTo(1));
            });
        }

        [Test]
        public async Task DeleteSnapshot_WhenOwnedByAnotherUser_ReturnsNotFoundAndDoesNotDelete()
        {
            await using PostgresDbContext dbContext = CreateDbContext();
            OwnedGraph graph = await SeedOwnedGraphAsync(dbContext, includeSnapshot: true);
            SnapshotController controller = AsUser(
                new SnapshotController(
                    CreateCipher(),
                    dbContext,
                    NullLogger<SnapshotController>.Instance,
                    []),
                Guid.NewGuid());

            IActionResult result = await controller.DeleteSnapshot(graph.Snapshot!.Id);

            AssertNotFound(result);
            dbContext.ChangeTracker.Clear();
            Assert.That(await dbContext.Snapshots.AnyAsync(x => x.Id == graph.Snapshot.Id), Is.True);
            Assert.That(await dbContext.SnapshotFiles.AnyAsync(x => x.Id == graph.SnapshotFile!.Id), Is.True);
        }

        [Test]
        public async Task GetSnapshots_WhenBackupIsOwnedByAnotherUser_ReturnsNotFound()
        {
            await using PostgresDbContext dbContext = CreateDbContext();
            OwnedGraph graph = await SeedOwnedGraphAsync(dbContext, includeSnapshot: true);
            SnapshotController controller = AsUser(
                new SnapshotController(
                    CreateCipher(),
                    dbContext,
                    NullLogger<SnapshotController>.Instance,
                    []),
                Guid.NewGuid());

            IActionResult result = controller.GetSnapshots(graph.Backup.Id);

            AssertNotFound(result);
        }

        [TestCase(typeof(ModuleController), nameof(ModuleController.RenameModule))]
        [TestCase(typeof(ModuleController), nameof(ModuleController.DeleteUserBackupStorage))]
        [TestCase(typeof(BackupController), nameof(BackupController.UpdateIgnoredPaths))]
        [TestCase(typeof(BackupController), nameof(BackupController.RenameBackup))]
        [TestCase(typeof(BackupController), nameof(BackupController.DeleteBackup))]
        [TestCase(typeof(ScheduleController), nameof(ScheduleController.CancelSchedule))]
        [TestCase(typeof(SnapshotController), nameof(SnapshotController.DownloadSnapshotArchive))]
        [TestCase(typeof(SnapshotController), nameof(SnapshotController.DownloadSnapshotFile))]
        [TestCase(typeof(SnapshotController), nameof(SnapshotController.GetSnapshot))]
        [TestCase(typeof(SnapshotController), nameof(SnapshotController.DeleteSnapshot))]
        [TestCase(typeof(SnapshotController), nameof(SnapshotController.GetSnapshots))]
        public void UserDataEndpoint_RequiresAuthorization(Type controllerType, string actionName)
        {
            System.Reflection.MethodInfo action = controllerType
                .GetMethods()
                .Single(x => x.Name == actionName);

            Assert.That(
                action.GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true),
                Is.Not.Empty);
        }

        private PostgresDbContext CreateDbContext()
        {
            DbContextOptions<PostgresDbContext> options = new DbContextOptionsBuilder<PostgresDbContext>()
                .UseNpgsql(_database.ConnectionString)
                .Options;
            return new PostgresDbContext(options);
        }

        private static IStreamCipher CreateCipher()
        {
            return new AesGcmStreamCipher(RandomNumberGenerator.GetBytes(32));
        }

        private static async Task ConfigureDownloadAsync(
            AppDbContext dbContext,
            OwnedGraph graph,
            byte[] storedContent,
            byte[] expectedContent)
        {
            string contentHash = CalculateHash(storedContent);
            string chunkKey = ChunkStorageHelpers.CreateKey(
                contentHash,
                CompressionAlgorithm.None,
                isEncrypted: false);
            SnapshotFile snapshotFile = graph.SnapshotFile!;
            snapshotFile.Size = storedContent.Length;
            snapshotFile.Hashsum = CalculateHash(expectedContent);
            snapshotFile.ChunkHashes = [chunkKey];
            UploadedHash uploadedHash = new()
            {
                ModuleId = graph.Storage.Id,
                Hash = chunkKey,
                StoredSize = storedContent.Length,
                OriginalSize = storedContent.Length,
                CompressionAlgorithm = CompressionAlgorithm.None,
            };
            dbContext.UploadedHashes.Add(uploadedHash);
            await dbContext.SaveChangesAsync();
        }

        private static string CalculateHash(byte[] content)
        {
            return Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        }

        private static async Task<Module> SeedStandaloneModuleAsync(AppDbContext dbContext)
        {
            string suffix = Guid.NewGuid().ToString("N");
            User user = new()
            {
                Username = $"owner-{suffix}",
                PasswordPhc = "not-used",
            };
            Module module = new()
            {
                User = user,
                Tag = $"module-{suffix}",
                BackupModuleId = "test-storage",
                Destination = ModuleDestination.Target,
            };

            await dbContext.Modules.AddAsync(module);
            await dbContext.SaveChangesAsync();
            return module;
        }

        private static async Task<OwnedGraph> SeedOwnedGraphAsync(
            AppDbContext dbContext,
            bool includeSchedule = false,
            bool includeSnapshot = false)
        {
            string suffix = Guid.NewGuid().ToString("N");
            User user = new()
            {
                Username = $"owner-{suffix}",
                PasswordPhc = "not-used",
            };
            Module source = new()
            {
                User = user,
                Tag = $"source-{suffix}",
                BackupModuleId = "test-source",
                Destination = ModuleDestination.Source,
            };
            Module storage = new()
            {
                User = user,
                Tag = $"storage-{suffix}",
                BackupModuleId = "test-storage",
                Destination = ModuleDestination.Target,
            };
            Backup backup = new()
            {
                Source = source,
                Storage = storage,
                Tag = $"backup-{suffix}",
            };
            OwnedGraph graph = new()
            {
                Source = source,
                Storage = storage,
                Backup = backup,
            };

            dbContext.Backups.Add(backup);

            if (includeSchedule)
            {
                graph.Schedule = new Schedule
                {
                    Backup = backup,
                    StartAt = DateTime.UtcNow,
                    Status = ScheduleStatus.Running,
                };
                dbContext.Schedules.Add(graph.Schedule);
            }

            if (includeSnapshot)
            {
                graph.Snapshot = new Snapshot
                {
                    Backup = backup,
                    CompletedAt = DateTime.UtcNow,
                    FilesCount = 1,
                    TotalSize = 1,
                };
                graph.SnapshotFile = new SnapshotFile
                {
                    Snapshot = graph.Snapshot,
                    Name = "file.bin",
                    Path = "file.bin",
                    Size = 1,
                    Hashsum = "hash",
                    ChunkHashes = [],
                };
                dbContext.SnapshotFiles.Add(graph.SnapshotFile);
            }

            await dbContext.SaveChangesAsync();
            return graph;
        }

        private static TController AsUser<TController>(TController controller, Guid userId)
            where TController : ControllerBase
        {
            ClaimsIdentity identity = new(
                [new Claim("sub", userId.ToString("D"))],
                "Test");
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(identity),
                },
            };
            return controller;
        }

        private static void AssertNotFound(IActionResult result)
        {
            Assert.That(result, Is.AssignableTo<IStatusCodeActionResult>());
            IStatusCodeActionResult statusCodeResult = (IStatusCodeActionResult)result;
            Assert.That(statusCodeResult.StatusCode, Is.EqualTo(StatusCodes.Status404NotFound));
        }

        private class OwnedGraph
        {
            public required Module Source { get; init; }
            public required Module Storage { get; init; }
            public required Backup Backup { get; init; }
            public Schedule? Schedule { get; set; }
            public Snapshot? Snapshot { get; set; }
            public SnapshotFile? SnapshotFile { get; set; }
        }

        private class TestStorage(
            string id,
            byte[]? content = null,
            bool failOnAccess = false) : IBackupStorage
        {
            public string Id => id;
            public string Name => id;
            public char PathSeparator => '/';
            public IEnumerable<string> RequiredParameters => [];
            public bool WasAccessed { get; private set; }

            public void SetParameters(IReadOnlyDictionary<string, string> parameters)
            {
                WasAccessed = true;
                if (failOnAccess)
                {
                    throw new InvalidOperationException("Storage must not be accessed for another user's file.");
                }
            }

            public void SetIgnoredPaths(ICollection<string>? ignoredPaths)
            {
            }

            public Task<BackupFileInfo?> GetFileInfoAsync(string path, CancellationToken cancellationToken) =>
                throw new NotSupportedException();

            public Task<Stream> GetFileStreamAsync(BackupFileInfo file, CancellationToken cancellationToken = default)
            {
                if (content == null)
                {
                    throw new InvalidOperationException("No test content was configured.");
                }

                return Task.FromResult<Stream>(new MemoryStream(content));
            }

            public IEnumerable<string> GetDirectories(bool recursive = false, CancellationToken cancellationToken = default) => [];

            public IEnumerable<BackupFileInfo> GetFiles(bool recursive = false, CancellationToken cancellationToken = default) => [];

            public Task<bool?> ExistsAsync(string path, CancellationToken cancellationToken = default) =>
                Task.FromResult<bool?>(content != null);

            public Task<bool?> DeleteAsync(string path, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();

            public Task UploadAsync(string path, Stream data, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();
        }

        private class UnexpectedSchedulerFactory : ISchedulerFactory
        {
            public Task<IReadOnlyList<IScheduler>> GetAllSchedulers(
                CancellationToken cancellationToken = default) =>
                throw new InvalidOperationException("Scheduler must not be accessed for another user's schedule.");

            public Task<IScheduler> GetScheduler(CancellationToken cancellationToken = default) =>
                throw new InvalidOperationException("Scheduler must not be accessed for another user's schedule.");

            public Task<IScheduler?> GetScheduler(
                string schedName,
                CancellationToken cancellationToken = default) =>
                throw new InvalidOperationException("Scheduler must not be accessed for another user's schedule.");
        }
    }
}
