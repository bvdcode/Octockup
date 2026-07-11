// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Octockup.Server.Controllers;
using Octockup.Server.Database;
using Octockup.Server.Models.Dto;
using Octockup.Server.Models.Enums;
using System.Security.Claims;

namespace Octockup.Tests
{
    public class StatsControllerTests
    {
        [Test]
        public async Task GetStats_UsesLatestCompletedCleanupAndScopesStoragesByUser()
        {
            await using SqliteConnection connection = new("Data Source=:memory:");
            await connection.OpenAsync();
            DbContextOptions<SqliteDbContext> options =
                new DbContextOptionsBuilder<SqliteDbContext>()
                    .UseSqlite(connection)
                    .Options;
            await using SqliteDbContext dbContext = new(options);
            await dbContext.Database.EnsureCreatedAsync();

            User user = CreateUser("stats-user");
            User otherUser = CreateUser("other-stats-user");
            Module source = CreateModule(user, "stats-source", ModuleDestination.Source);
            Module cachedStorage = CreateModule(user, "cached-storage", ModuleDestination.Target);
            Module uncachedStorage = CreateModule(user, "uncached-storage", ModuleDestination.Target);
            Module otherStorage = CreateModule(otherUser, "other-storage", ModuleDestination.Target);
            Backup backup = new()
            {
                UserId = user.Id,
                Source = source,
                Storage = cachedStorage,
                Tag = "stats-backup"
            };
            await dbContext.AddRangeAsync(
                user,
                otherUser,
                source,
                cachedStorage,
                uncachedStorage,
                otherStorage,
                backup);
            await dbContext.SaveChangesAsync();

            await dbContext.UploadedHashes.AddRangeAsync(
                CreateUploadedHash(cachedStorage.Id, "hash-1", 20, 12),
                CreateUploadedHash(cachedStorage.Id, "hash-2", 30, 18));
            DateTime completedAt = DateTime.UtcNow.AddMinutes(-1);
            await dbContext.StorageCleanupJobs.AddRangeAsync(
                CreateCleanupJob(
                    user.Id,
                    cachedStorage.Id,
                    cachedStorage.Tag,
                    StorageCleanupStatus.Completed,
                    completedAt,
                    10,
                    4),
                CreateCleanupJob(
                    user.Id,
                    cachedStorage.Id,
                    cachedStorage.Tag,
                    StorageCleanupStatus.Failed,
                    DateTime.UtcNow,
                    100,
                    1),
                CreateCleanupJob(
                    otherUser.Id,
                    otherStorage.Id,
                    otherStorage.Tag,
                    StorageCleanupStatus.Completed,
                    DateTime.UtcNow,
                    500,
                    1));
            await dbContext.SaveChangesAsync();

            StatsController controller = new(dbContext)
            {
                ControllerContext = CreateControllerContext(user.Id)
            };

            IActionResult actionResult = await controller.GetStats(CancellationToken.None);
            OkObjectResult okResult = actionResult as OkObjectResult
                ?? throw new AssertionException("Expected an OK result.");
            StatsDto result = okResult.Value as StatsDto
                ?? throw new AssertionException("Expected a StatsDto response.");
            StorageStatsDto cached = result.StorageStats.Single(x => x.Id == cachedStorage.Id);
            StorageStatsDto uncached = result.StorageStats.Single(x => x.Id == uncachedStorage.Id);

            Assert.Multiple(() =>
            {
                Assert.That(result.TotalUsers, Is.EqualTo(2));
                Assert.That(result.StorageStats, Has.Count.EqualTo(2));
                Assert.That(result.StorageStats.Any(x => x.Id == otherStorage.Id), Is.False);
                Assert.That(cached.TotalBackups, Is.EqualTo(1));
                Assert.That(cached.TotalOriginalSize, Is.EqualTo(50));
                Assert.That(cached.TotalStoredSize, Is.EqualTo(30));
                Assert.That(cached.DeduplicatedChunks, Is.EqualTo(6));
                Assert.That(uncached.DeduplicatedChunks, Is.Null);
            });
        }

        private static User CreateUser(string username)
        {
            return new User
            {
                Username = username,
                PasswordPhc = "password"
            };
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

        private static UploadedHash CreateUploadedHash(
            Guid storageId,
            string hash,
            long originalSize,
            long storedSize)
        {
            return new UploadedHash
            {
                ModuleId = storageId,
                Hash = hash,
                OriginalSize = originalSize,
                StoredSize = storedSize
            };
        }

        private static StorageCleanupJob CreateCleanupJob(
            Guid userId,
            Guid storageId,
            string storageTag,
            StorageCleanupStatus status,
            DateTime finishedAt,
            long referenceCount,
            long referencedChunks)
        {
            return new StorageCleanupJob
            {
                UserId = userId,
                StorageId = storageId,
                StorageTag = storageTag,
                Status = status,
                Phase = status == StorageCleanupStatus.Completed
                    ? StorageCleanupPhase.Completed
                    : StorageCleanupPhase.ScanningStorage,
                StartedAt = finishedAt.AddMinutes(-1),
                FinishedAt = finishedAt,
                ReferenceCount = referenceCount,
                ReferencedChunks = referencedChunks
            };
        }

        private static ControllerContext CreateControllerContext(Guid userId)
        {
            ClaimsIdentity identity = new(
                [new Claim("sub", userId.ToString())],
                "TestAuthentication");
            return new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(identity)
                }
            };
        }
    }
}
