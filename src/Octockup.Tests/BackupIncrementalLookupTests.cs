// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Octockup.Server.Database;
using Octockup.Server.Models.Enums;
using System.Collections.Concurrent;
using System.Data.Common;

namespace Octockup.Tests
{
    [Category("Integration")]
    public class BackupIncrementalLookupTests
    {
        private const int PreviousFilesBatchSize = 4_096;
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
        public async Task IncrementalBackup_OverlaysPartialSnapshotOnLastCompletedSnapshot()
        {
            await using BackupIntegrationScenario scenario = await BackupIntegrationScenario.CreateAsync(
                _database.ConnectionString);
            const string partialPath = "partial.txt";
            const string fallbackPath = "fallback.txt";
            byte[] partialContent = [1, 2, 3, 4];
            byte[] fallbackContent = [5, 6, 7, 8];
            DateTime lastModified = DateTime.UtcNow.AddMinutes(-5);
            await scenario.WriteSourceFileAsync(
                partialPath,
                partialContent,
                lastModified);
            await scenario.WriteSourceFileAsync(
                fallbackPath,
                fallbackContent,
                lastModified);

            Schedule firstSchedule = await scenario.RunBackupAsync();
            Assert.That(firstSchedule.Status, Is.EqualTo(ScheduleStatus.Completed), firstSchedule.ErrorMessage);
            Snapshot completedSnapshot = await scenario.GetLatestSnapshotAsync();
            SnapshotFile completedFallbackFile = await scenario.GetSnapshotFileAsync(
                completedSnapshot.Id,
                fallbackPath);

            Snapshot incompleteSnapshot = new()
            {
                BackupId = scenario.BackupId,
            };
            await scenario.DbContext.Snapshots.AddAsync(incompleteSnapshot);
            await scenario.DbContext.SaveChangesAsync();
            string partialHash = new('f', 64);
            string partialChunkHash = new('e', 64);
            SnapshotFile partialFile = new()
            {
                SnapshotId = incompleteSnapshot.Id,
                Path = partialPath,
                Name = partialPath,
                Size = partialContent.Length,
                LastModified = scenario.GetSourceLastWriteTimeUtc(partialPath),
                Hashsum = partialHash,
                ChunkHashes = [partialChunkHash],
            };
            await scenario.DbContext.SnapshotFiles.AddAsync(partialFile);
            await scenario.DbContext.SaveChangesAsync();
            scenario.DbContext.ChangeTracker.Clear();

            Schedule secondSchedule = await scenario.RunBackupAsync();
            Assert.That(secondSchedule.Status, Is.EqualTo(ScheduleStatus.Completed), secondSchedule.ErrorMessage);
            Snapshot secondSnapshot = await scenario.GetLatestSnapshotAsync();
            SnapshotFile secondPartialFile = await scenario.GetSnapshotFileAsync(
                secondSnapshot.Id,
                partialPath);
            SnapshotFile secondFallbackFile = await scenario.GetSnapshotFileAsync(
                secondSnapshot.Id,
                fallbackPath);

            Assert.Multiple(() =>
            {
                Assert.That(secondPartialFile.Hashsum, Is.EqualTo(partialHash));
                Assert.That(secondPartialFile.ChunkHashes, Is.EqualTo(new[] { partialChunkHash }));
                Assert.That(secondFallbackFile.Hashsum, Is.EqualTo(completedFallbackFile.Hashsum));
                Assert.That(secondFallbackFile.ChunkHashes, Is.EqualTo(completedFallbackFile.ChunkHashes));
            });
        }

        [Test]
        public async Task IncrementalBackup_LoadsPreviousFilesInBoundedBatches()
        {
            PreviousFileLookupInterceptor interceptor = new();
            await using BackupIntegrationScenario scenario = await BackupIntegrationScenario.CreateAsync(
                _database.ConnectionString,
                interceptor);
            int fileCount = PreviousFilesBatchSize + 1;
            DateTime lastModified = DateTime.UtcNow.AddMinutes(-5);
            List<SnapshotFile> previousFiles = new(fileCount);
            Snapshot previousSnapshot = new()
            {
                BackupId = scenario.BackupId,
                CompletedAt = DateTime.UtcNow,
            };
            await scenario.DbContext.Snapshots.AddAsync(previousSnapshot);
            await scenario.DbContext.SaveChangesAsync();

            for (int index = 0; index < fileCount; index++)
            {
                string relativePath = Path.Combine("many", $"{index:D5}.txt");
                await scenario.WriteSourceFileAsync(relativePath, [], lastModified);
                previousFiles.Add(new SnapshotFile
                {
                    SnapshotId = previousSnapshot.Id,
                    Path = relativePath,
                    Name = Path.GetFileName(relativePath),
                    Size = 0,
                    LastModified = scenario.GetSourceLastWriteTimeUtc(relativePath),
                    Hashsum = index.ToString("x64"),
                    ChunkHashes = [],
                });
            }

            await scenario.DbContext.SnapshotFiles.AddRangeAsync(previousFiles);
            await scenario.DbContext.SaveChangesAsync();
            scenario.DbContext.ChangeTracker.Clear();
            interceptor.Reset();

            Schedule schedule = await scenario.RunBackupAsync();
            Snapshot currentSnapshot = await scenario.GetLatestSnapshotAsync();
            SnapshotFile firstFile = await scenario.GetSnapshotFileAsync(
                currentSnapshot.Id,
                Path.Combine("many", "00000.txt"));
            SnapshotFile boundaryFile = await scenario.GetSnapshotFileAsync(
                currentSnapshot.Id,
                Path.Combine("many", $"{PreviousFilesBatchSize - 1:D5}.txt"));
            SnapshotFile lastFile = await scenario.GetSnapshotFileAsync(
                currentSnapshot.Id,
                Path.Combine("many", $"{PreviousFilesBatchSize:D5}.txt"));

            Assert.Multiple(() =>
            {
                Assert.That(schedule.Status, Is.EqualTo(ScheduleStatus.Completed), schedule.ErrorMessage);
                Assert.That(interceptor.PathBatchSizes, Has.Count.EqualTo(2));
                Assert.That(interceptor.PathBatchSizes, Is.All.LessThanOrEqualTo(PreviousFilesBatchSize));
                Assert.That(interceptor.PathBatchSizes.Sum(), Is.EqualTo(fileCount));
                Assert.That(firstFile.Hashsum, Is.EqualTo(0.ToString("x64")));
                Assert.That(boundaryFile.Hashsum, Is.EqualTo((PreviousFilesBatchSize - 1).ToString("x64")));
                Assert.That(lastFile.Hashsum, Is.EqualTo(PreviousFilesBatchSize.ToString("x64")));
            });
        }

        private sealed class PreviousFileLookupInterceptor : DbCommandInterceptor
        {
            private readonly ConcurrentQueue<int> _pathBatchSizes = new();

            public IReadOnlyCollection<int> PathBatchSizes => _pathBatchSizes.ToArray();

            public void Reset()
            {
                _pathBatchSizes.Clear();
            }

            public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
                DbCommand command,
                CommandEventData eventData,
                InterceptionResult<DbDataReader> result,
                CancellationToken cancellationToken = default)
            {
                if (!command.CommandText.Contains("FROM snapshot_files AS", StringComparison.OrdinalIgnoreCase) &&
                    !command.CommandText.Contains("FROM \"snapshot_files\" AS", StringComparison.OrdinalIgnoreCase))
                {
                    return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
                }

                foreach (DbParameter parameter in command.Parameters)
                {
                    if (parameter.Value is string[] paths)
                    {
                        _pathBatchSizes.Enqueue(paths.Length);
                    }
                }

                return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
            }
        }
    }
}
