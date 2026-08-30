// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.Abstractions;
using EasyExtensions.Models.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Octockup.Server.Abstractions;
using Octockup.Server.Database;
using Octockup.Server.Helpers;
using Octockup.Server.Models;
using Octockup.Server.Streams;
using System.Security.Cryptography;

namespace Octockup.Tests
{
    public class SnapshotConcatStreamTests
    {
        private const string Hash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        [Test]
        public async Task ReadAsync_WhenFileLengthIsReached_DrainsCurrentChunkBeforeDisposing()
        {
            byte[] content = [1, 2, 3, 4, 5];
            EofRequiredStream storageStream = new EofRequiredStream(content);
            InMemoryStorage storage = new InMemoryStorage(storageStream);
            SnapshotFile snapshotFile = new SnapshotFile
            {
                Path = "Bundles/Bundles",
                Name = "Bundles",
                Size = content.Length
            };
            ChunkStorageDescriptor chunk = new ChunkStorageDescriptor(
                Hash,
                Hash,
                CompressionAlgorithm.None,
                IsEncrypted: false);

            await using SnapshotConcatStream stream = new SnapshotConcatStream(
                NullLogger.Instance,
                storage,
                [chunk],
                snapshotFile,
                new PassThroughCipher());

            byte[] buffer = new byte[1024];
            int read = await stream.ReadAsync(buffer);
            int eof = await stream.ReadAsync(buffer);

            Assert.Multiple(() =>
            {
                Assert.That(read, Is.EqualTo(content.Length));
                Assert.That(buffer.Take(read), Is.EqualTo(content));
                Assert.That(eof, Is.Zero);
                Assert.That(storageStream.EofObserved, Is.True);
                Assert.That(storageStream.DisposeCalled, Is.True);
            });
        }

        [Test]
        public async Task ReadAsync_WhenLegacyChunkMetadataSaysZstdButPayloadIsPlain_ReadsPlainPayload()
        {
            byte[] content = [9, 8, 7, 6];
            InMemoryStorage storage = new InMemoryStorage(new MemoryStream(content));
            SnapshotFile snapshotFile = new SnapshotFile
            {
                Path = "Bundles/Bundles",
                Name = "Bundles",
                Size = content.Length
            };
            ChunkStorageDescriptor legacyChunkWithWrongMetadata = new ChunkStorageDescriptor(
                Hash,
                Hash,
                CompressionHelpers.Algorithm,
                IsEncrypted: true);

            await using SnapshotConcatStream stream = new SnapshotConcatStream(
                NullLogger.Instance,
                storage,
                [legacyChunkWithWrongMetadata],
                snapshotFile,
                new PassThroughCipher());

            byte[] buffer = new byte[1024];
            int read = await stream.ReadAsync(buffer);

            Assert.Multiple(() =>
            {
                Assert.That(read, Is.EqualTo(content.Length));
                Assert.That(buffer.Take(read), Is.EqualTo(content));
            });
        }

        [Test]
        public async Task ReadAsync_WhenSnapshotSizeIsSmallerThanChunkOriginalSize_UsesChunkOriginalSize()
        {
            byte[] content = [1, 1, 2, 3, 5, 8];
            InMemoryStorage storage = new InMemoryStorage(new MemoryStream(content));
            SnapshotFile snapshotFile = new SnapshotFile
            {
                Path = "oxide/data/Boxlooters/box_data.json",
                Name = "box_data.json",
                Size = 3
            };
            ChunkStorageDescriptor chunk = new ChunkStorageDescriptor(
                Hash,
                Hash,
                CompressionAlgorithm.None,
                IsEncrypted: false,
                OriginalSize: content.Length);

            await using SnapshotConcatStream stream = new SnapshotConcatStream(
                NullLogger.Instance,
                storage,
                [chunk],
                snapshotFile,
                new PassThroughCipher());

            byte[] buffer = new byte[1024];
            int read = await stream.ReadAsync(buffer);

            Assert.Multiple(() =>
            {
                Assert.That(stream.Length, Is.EqualTo(content.Length));
                Assert.That(read, Is.EqualTo(content.Length));
                Assert.That(buffer.Take(read), Is.EqualTo(content));
            });
        }

        [Test]
        public async Task CopyToAsync_WhenValidationIsDisabled_DoesNotCompareFileChecksum()
        {
            byte[] content = [1, 2, 3, 4, 5];
            InMemoryStorage storage = new(new MemoryStream(content));
            SnapshotFile snapshotFile = CreateSnapshotFile(content.Length, "different-checksum");
            ChunkStorageDescriptor chunk = CreateChunk(content.Length);

            await using SnapshotConcatStream stream = new(
                NullLogger.Instance,
                storage,
                [chunk],
                snapshotFile,
                new PassThroughCipher());
            await using MemoryStream restored = new();

            await stream.CopyToAsync(restored);

            Assert.That(restored.ToArray(), Is.EqualTo(content));
        }

        [Test]
        public async Task CopyToAsync_WhenValidationIsEnabledAndChecksumMatches_Completes()
        {
            byte[] content = [1, 2, 3, 4, 5];
            InMemoryStorage storage = new(new MemoryStream(content));
            SnapshotFile snapshotFile = CreateSnapshotFile(content.Length, CalculateHash(content));
            ChunkStorageDescriptor chunk = CreateChunk(content.Length);

            await using SnapshotConcatStream stream = new(
                NullLogger.Instance,
                storage,
                [chunk],
                snapshotFile,
                new PassThroughCipher(),
                validate: true);
            await using MemoryStream restored = new();

            await stream.CopyToAsync(restored);

            Assert.That(restored.ToArray(), Is.EqualTo(content));
        }

        [Test]
        public async Task ReadAsync_WhenValidationIsEnabledAcrossChunks_ValidatesCombinedChecksum()
        {
            byte[] firstChunk = [1, 2, 3];
            byte[] secondChunk = [4, 5, 6, 7];
            byte[] content = [.. firstChunk, .. secondChunk];
            InMemoryStorage storage = new(
                new MemoryStream(firstChunk),
                new MemoryStream(secondChunk));
            SnapshotFile snapshotFile = CreateSnapshotFile(content.Length, CalculateHash(content));
            ChunkStorageDescriptor[] chunks =
            [
                CreateChunk(firstChunk.Length),
                CreateChunk(secondChunk.Length),
            ];

            await using SnapshotConcatStream stream = new(
                NullLogger.Instance,
                storage,
                chunks,
                snapshotFile,
                new PassThroughCipher(),
                validate: true);
            await using MemoryStream restored = new();
            byte[] buffer = new byte[2];
            int read;
            while ((read = await stream.ReadAtLeastAsync(
                buffer,
                minimumBytes: 1,
                throwOnEndOfStream: false)) > 0)
            {
                await restored.WriteAsync(buffer.AsMemory(0, read));
            }

            Assert.That(restored.ToArray(), Is.EqualTo(content));
        }

        [Test]
        public void CopyToAsync_WhenValidationIsEnabledAndChecksumDiffers_ThrowsInvalidDataException()
        {
            byte[] storedContent = [1, 2, 3, 4, 5];
            byte[] expectedContent = [5, 4, 3, 2, 1];
            InMemoryStorage storage = new(new MemoryStream(storedContent));
            SnapshotFile snapshotFile = CreateSnapshotFile(storedContent.Length, CalculateHash(expectedContent));
            ChunkStorageDescriptor chunk = CreateChunk(storedContent.Length);

            Assert.That(async () =>
            {
                await using SnapshotConcatStream stream = new(
                    NullLogger.Instance,
                    storage,
                    [chunk],
                    snapshotFile,
                    new PassThroughCipher(),
                    validate: true);
                await stream.CopyToAsync(Stream.Null);
            }, Throws.TypeOf<InvalidDataException>());
        }

        [Test]
        public void CopyToAsync_WhenValidationIsEnabledAndContentIsTruncated_ThrowsInvalidDataException()
        {
            byte[] storedContent = [1, 2];
            byte[] expectedContent = [1, 2, 3, 4, 5];
            InMemoryStorage storage = new(new MemoryStream(storedContent));
            SnapshotFile snapshotFile = CreateSnapshotFile(expectedContent.Length, CalculateHash(expectedContent));
            ChunkStorageDescriptor chunk = CreateChunk(expectedContent.Length);

            Assert.That(async () =>
            {
                await using SnapshotConcatStream stream = new(
                    NullLogger.Instance,
                    storage,
                    [chunk],
                    snapshotFile,
                    new PassThroughCipher(),
                    validate: true);
                await stream.CopyToAsync(Stream.Null);
            }, Throws.TypeOf<InvalidDataException>());
        }

        [Test]
        public async Task ReadAsync_WhenValidationIsEnabledForEmptyFile_ValidatesEmptyChecksum()
        {
            byte[] content = [];
            InMemoryStorage storage = new(new MemoryStream(content));
            SnapshotFile snapshotFile = CreateSnapshotFile(0, CalculateHash(content));

            await using SnapshotConcatStream stream = new(
                NullLogger.Instance,
                storage,
                [],
                snapshotFile,
                new PassThroughCipher(),
                validate: true);

            int read = await stream.ReadAtLeastAsync(
                new byte[1],
                minimumBytes: 1,
                throwOnEndOfStream: false);

            Assert.That(read, Is.Zero);
        }

        [Test]
        public void ReadAsync_WhenValidationIsEnabledForEmptyFileAndChecksumDiffers_ThrowsInvalidDataException()
        {
            InMemoryStorage storage = new(new MemoryStream());
            SnapshotFile snapshotFile = CreateSnapshotFile(0, "different-checksum");

            Assert.That(async () =>
            {
                await using SnapshotConcatStream stream = new(
                    NullLogger.Instance,
                    storage,
                    [],
                    snapshotFile,
                    new PassThroughCipher(),
                    validate: true);
                await stream.ReadAtLeastAsync(
                    new byte[1],
                    minimumBytes: 1,
                    throwOnEndOfStream: false);
            }, Throws.TypeOf<InvalidDataException>());
        }

        [Test]
        public async Task DisposeAsync_WhenValidatedStreamIsOnlyPartiallyRead_DisposesStorageWithoutValidating()
        {
            byte[] content = [1, 2, 3, 4, 5];
            TrackingStream storageStream = new(content);
            InMemoryStorage storage = new(storageStream);
            SnapshotFile snapshotFile = CreateSnapshotFile(content.Length, "different-checksum");
            ChunkStorageDescriptor chunk = CreateChunk(content.Length);
            SnapshotConcatStream stream = new(
                NullLogger.Instance,
                storage,
                [chunk],
                snapshotFile,
                new PassThroughCipher(),
                validate: true);

            int read = await stream.ReadAtLeastAsync(
                new byte[1],
                minimumBytes: 1,
                throwOnEndOfStream: false);
            await stream.DisposeAsync();

            Assert.Multiple(() =>
            {
                Assert.That(read, Is.EqualTo(1));
                Assert.That(storageStream.DisposeCalled, Is.True);
            });
        }

        private static SnapshotFile CreateSnapshotFile(long size, string hashsum)
        {
            return new SnapshotFile
            {
                Path = "documents/file.bin",
                Name = "file.bin",
                Size = size,
                Hashsum = hashsum,
            };
        }

        private static ChunkStorageDescriptor CreateChunk(long originalSize)
        {
            return new ChunkStorageDescriptor(
                Hash,
                Hash,
                CompressionAlgorithm.None,
                IsEncrypted: false,
                OriginalSize: originalSize);
        }

        private static string CalculateHash(byte[] content)
        {
            return Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        }

        private class InMemoryStorage(params Stream[] streams) : IBackupStorage
        {
            private readonly Queue<Stream> _streams = new(streams);

            public string Id => nameof(InMemoryStorage);
            public string Name => nameof(InMemoryStorage);
            public char PathSeparator => '/';
            public IEnumerable<string> RequiredParameters => [];

            public void SetParameters(IReadOnlyDictionary<string, string> parameters)
            {
            }

            public void SetIgnoredPaths(ICollection<string>? ignoredPaths)
            {
            }

            public Task<BackupFileInfo?> GetFileInfoAsync(string path, CancellationToken cancellationToken) =>
                Task.FromResult<BackupFileInfo?>(null);

            public Task<Stream> GetFileStreamAsync(BackupFileInfo file, CancellationToken cancellationToken = default) =>
                Task.FromResult(_streams.Dequeue());

            public IEnumerable<string> GetDirectories(bool recursive = false, CancellationToken cancellationToken = default) => [];

            public IEnumerable<BackupFileInfo> GetFiles(bool recursive = false, CancellationToken cancellationToken = default) => [];

            public Task<bool?> ExistsAsync(string path, CancellationToken cancellationToken = default) =>
                Task.FromResult<bool?>(true);

            public Task<bool?> DeleteAsync(string path, CancellationToken cancellationToken = default) =>
                Task.FromResult<bool?>(null);

            public Task UploadAsync(string path, Stream data, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;
        }

        private class TrackingStream(byte[] content) : MemoryStream(content)
        {
            public bool DisposeCalled { get; private set; }

            protected override void Dispose(bool disposing)
            {
                DisposeCalled = true;
                base.Dispose(disposing);
            }

            public override async ValueTask DisposeAsync()
            {
                DisposeCalled = true;
                await base.DisposeAsync();
            }
        }

    }
}
