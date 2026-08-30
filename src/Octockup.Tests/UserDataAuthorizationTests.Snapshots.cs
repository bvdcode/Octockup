// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Octockup.Server.Controllers;
using Octockup.Server.Database;
using Octockup.Server.Models.Dto;

namespace Octockup.Tests
{
    public partial class UserDataAuthorizationTests
    {
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
            await Assert.ThatAsync(async () =>
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
            Assert.That(
                fastController.Response.ContentLength,
                Is.EqualTo(fastController.Response.Body.Length));

            SnapshotController validatedController = AsUser(
                new SnapshotController(
                    CreateCipher(),
                    dbContext,
                    NullLogger<SnapshotController>.Instance,
                    [storage]),
                graph.Source.UserId);
            validatedController.Response.Body = new MemoryStream();
            await Assert.ThatAsync(async () =>
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
    }
}
