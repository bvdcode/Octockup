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

namespace Octockup.Tests
{
    public class SnapshotConcatStreamTests
    {
        private const string Hash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        [Test]
        public async Task ReadAsync_WhenFileLengthIsReached_DrainsCurrentChunkBeforeDisposing()
        {
            byte[] content = [1, 2, 3, 4, 5];
            var storageStream = new EofRequiredStream(content);
            var storage = new InMemoryStorage(storageStream);
            var snapshotFile = new SnapshotFile
            {
                Path = "Bundles/Bundles",
                Name = "Bundles",
                Size = content.Length
            };
            var chunk = new ChunkStorageDescriptor(
                Hash,
                Hash,
                CompressionAlgorithm.None,
                IsEncrypted: false);

            await using var stream = new SnapshotConcatStream(
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
            var storage = new InMemoryStorage(new MemoryStream(content));
            var snapshotFile = new SnapshotFile
            {
                Path = "Bundles/Bundles",
                Name = "Bundles",
                Size = content.Length
            };
            var legacyChunkWithWrongMetadata = new ChunkStorageDescriptor(
                Hash,
                Hash,
                CompressionHelpers.Algorithm,
                IsEncrypted: true);

            await using var stream = new SnapshotConcatStream(
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

        private sealed class InMemoryStorage(Stream stream) : IBackupStorage
        {
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
                Task.FromResult(stream);

            public IEnumerable<string> GetDirectories(bool recursive = false, CancellationToken cancellationToken = default) => [];

            public IEnumerable<BackupFileInfo> GetFiles(bool recursive = false, CancellationToken cancellationToken = default) => [];

            public Task<bool?> ExistsAsync(string path, CancellationToken cancellationToken = default) =>
                Task.FromResult<bool?>(path == ChunkStorageHelpers.GetStoragePath(Hash, PathSeparator));

            public Task<bool?> DeleteAsync(string path, CancellationToken cancellationToken = default) =>
                Task.FromResult<bool?>(null);

            public Task UploadAsync(string path, Stream data, CancellationToken cancellationToken = default) =>
                Task.CompletedTask;
        }

        private sealed class EofRequiredStream(byte[] content) : Stream
        {
            private int _position;

            public bool EofObserved { get; private set; }
            public bool DisposeCalled { get; private set; }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => content.Length;

            public override long Position
            {
                get => _position;
                set => throw new NotSupportedException();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (_position >= content.Length)
                {
                    EofObserved = true;
                    return 0;
                }

                int read = Math.Min(count, content.Length - _position);
                content.AsSpan(_position, read).CopyTo(buffer.AsSpan(offset, read));
                _position += read;
                return read;
            }

            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                if (_position >= content.Length)
                {
                    EofObserved = true;
                    return ValueTask.FromResult(0);
                }

                int read = Math.Min(buffer.Length, content.Length - _position);
                content.AsMemory(_position, read).CopyTo(buffer);
                _position += read;
                return ValueTask.FromResult(read);
            }

            protected override void Dispose(bool disposing)
            {
                DisposeCalled = true;
                if (!EofObserved)
                {
                    throw new InvalidOperationException("The stream was disposed before EOF was observed.");
                }

                base.Dispose(disposing);
            }

            public override ValueTask DisposeAsync()
            {
                DisposeCalled = true;
                if (!EofObserved)
                {
                    throw new InvalidOperationException("The stream was disposed before EOF was observed.");
                }

                return base.DisposeAsync();
            }

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override void Flush()
            {
            }
        }

        private sealed class PassThroughCipher : IStreamCipher
        {
            public async Task EncryptAsync(
                Stream input,
                Stream output,
                int chunkSize,
                bool leaveInputOpen,
                bool leaveOutputOpen,
                CancellationToken ct)
            {
                await input.CopyToAsync(output, ct);
            }

            public async Task DecryptAsync(
                Stream input,
                Stream output,
                bool leaveInputOpen,
                bool leaveOutputOpen,
                CancellationToken ct)
            {
                await input.CopyToAsync(output, ct);
            }

            public Task<Stream> EncryptAsync(Stream input, int chunkSize, bool leaveOpen, CancellationToken ct) =>
                Task.FromResult(input);

            public Task<Stream> DecryptAsync(Stream input, bool leaveOpen, CancellationToken ct) =>
                Task.FromResult(input);
        }
    }
}
