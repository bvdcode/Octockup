// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.Models.Enums;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Octockup.Server.Database;
using Octockup.Server.Models.Enums;
using Octockup.Server.Services;

namespace Octockup.Tests
{
    public class UploadedHashWriterTests
    {
        private SqliteConnection _anchorConnection = null!;
        private string _connectionString = string.Empty;
        private Guid _storageId;

        [SetUp]
        public async Task Setup()
        {
            string databaseName = "uploaded-hash-writer-" + Guid.NewGuid().ToString("N");
            _connectionString = $"Data Source={databaseName};Mode=Memory;Cache=Shared";
            _anchorConnection = new SqliteConnection(_connectionString);
            await _anchorConnection.OpenAsync();
            await using SqliteDbContext dbContext = CreateContext();
            await dbContext.Database.EnsureCreatedAsync();
            User user = new()
            {
                Username = "hash-writer-user",
                PasswordPhc = "password"
            };
            Module storage = new()
            {
                User = user,
                Tag = "hash-writer-storage",
                BackupModuleId = "storage-provider",
                Destination = ModuleDestination.Target
            };
            await dbContext.AddRangeAsync(user, storage);
            await dbContext.SaveChangesAsync();
            _storageId = storage.Id;
        }

        [TearDown]
        public async Task TearDown()
        {
            await _anchorConnection.DisposeAsync();
        }

        [Test]
        public async Task FlushAsync_WhenBatchContainsExistingAndRepeatedHashes_IsIdempotent()
        {
            await using SqliteDbContext dbContext = CreateContext();
            await dbContext.UploadedHashes.AddAsync(CreateHash("existing"));
            await dbContext.SaveChangesAsync();
            dbContext.ChangeTracker.Clear();
            UploadedHashWriter writer = CreateWriter(dbContext);
            UploadedHash[] batch =
            [
                CreateHash("existing"),
                CreateHash("new"),
                CreateHash("new")
            ];

            int inserted = await writer.FlushAsync(batch, CancellationToken.None);
            List<string> hashes = await dbContext.UploadedHashes
                .AsNoTracking()
                .OrderBy(x => x.Hash)
                .Select(x => x.Hash)
                .ToListAsync();

            Assert.Multiple(() =>
            {
                Assert.That(inserted, Is.EqualTo(1));
                Assert.That(hashes, Is.EqualTo(new[] { "existing", "new" }));
                Assert.That(dbContext.ChangeTracker.Entries<UploadedHash>(), Is.Empty);
            });
        }

        [Test]
        public async Task FlushAsync_WhenTwoWritersRegisterSameChunk_PersistsOneRow()
        {
            await using SqliteDbContext firstContext = CreateContext();
            await using SqliteDbContext secondContext = CreateContext();
            UploadedHashWriter firstWriter = CreateWriter(firstContext);
            UploadedHashWriter secondWriter = CreateWriter(secondContext);

            Task<int> first = firstWriter.FlushAsync(
                new[] { CreateHash("shared") },
                CancellationToken.None);
            Task<int> second = secondWriter.FlushAsync(
                new[] { CreateHash("shared") },
                CancellationToken.None);
            int[] inserted = await Task.WhenAll(first, second);

            await using SqliteDbContext verificationContext = CreateContext();
            int count = await verificationContext.UploadedHashes
                .CountAsync(x => x.ModuleId == _storageId && x.Hash == "shared");
            Assert.Multiple(() =>
            {
                Assert.That(inserted.Sum(), Is.EqualTo(1));
                Assert.That(count, Is.EqualTo(1));
            });
        }

        private SqliteDbContext CreateContext()
        {
            DbContextOptions<SqliteDbContext> options =
                new DbContextOptionsBuilder<SqliteDbContext>()
                    .UseSqlite(_connectionString)
                    .Options;
            return new SqliteDbContext(options);
        }

        private UploadedHashWriter CreateWriter(AppDbContext dbContext)
        {
            return new UploadedHashWriter(
                dbContext,
                NullLogger<UploadedHashWriter>.Instance);
        }

        private UploadedHash CreateHash(string hash)
        {
            return new UploadedHash
            {
                ModuleId = _storageId,
                Hash = hash,
                OriginalSize = 10,
                StoredSize = 10,
                CompressionAlgorithm = CompressionAlgorithm.None
            };
        }
    }
}
