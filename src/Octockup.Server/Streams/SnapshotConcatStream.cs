// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.Abstractions;
using EasyExtensions.Models.Enums;
using Octockup.Server.Abstractions;
using Octockup.Server.Database;
using Octockup.Server.Helpers;
using Octockup.Server.Models;
using System.IO.Compression;

namespace Octockup.Server.Streams
{
    public sealed class SnapshotConcatStream(
        ILogger _logger,
        IBackupStorage _storage,
        IReadOnlyList<ChunkStorageDescriptor> _chunks,
        SnapshotFile _snapshotFile,
        IStreamCipher _crypto,
        CancellationToken _cancellationToken = default,
        long? _lengthOverride = null) : Stream
    {
        private int _currentIndex = -1;

        private Stream? _currentChunkStream;
        private readonly long _length = _lengthOverride
            ?? (_chunks.Count > 0 && _chunks.All(x => x.OriginalSize.HasValue)
                ? _chunks.Sum(x => x.OriginalSize!.Value)
                : _snapshotFile.Size);
        private long _position;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;

        public override long Length => _length;

        public override long Position
        {
            get => _position;
            set => throw new NotSupportedException();
        }

        private async Task<bool> MoveToNextChunkAsync()
        {
            await DisposeCurrentChunkAsync().ConfigureAwait(false);

            _currentIndex++;
            if (_currentIndex >= _chunks.Count)
            {
                _currentChunkStream = null;
                return false;
            }

            var chunk = _chunks[_currentIndex];
            _logger.LogInformation(
                "Loading chunk {Index}/{Total} with key {Key}",
                _currentIndex + 1,
                _chunks.Count,
                chunk.Key
            );

            string path = ChunkStorageHelpers.GetStoragePath(chunk.Key, _storage.PathSeparator);
            bool exists = await _storage.ExistsAsync(path).ConfigureAwait(false) ?? false;
            if (exists != true)
            {
                throw new IOException($"Chunk '{chunk.Key}' not found in storage.");
            }

            var fileInfo = new BackupFileInfo
            {
                Path = path,
                Name = chunk.Key,
                Size = null,
                LastModified = _snapshotFile.LastModified,
            };

            var storedChunkStream = await _storage
                .GetFileStreamAsync(fileInfo)
                .ConfigureAwait(false);

            Stream restored = chunk.IsEncrypted
                ? await _crypto.DecryptAsync(storedChunkStream).ConfigureAwait(false)
                : storedChunkStream;

            var (source, compressionAlgorithm) = await ResolveLegacyCompressionAsync(restored, chunk).ConfigureAwait(false);

            Stream decompressed = compressionAlgorithm switch
            {
                CompressionAlgorithm.None => source,
                CompressionHelpers.Algorithm => CompressionHelpers.CreateDecompressionStream(source, leaveOpen: false),
                _ => throw new NotSupportedException($"Unsupported compression algorithm: {compressionAlgorithm}"),
            };
            _currentChunkStream = decompressed;
            return true;
        }

        private async Task<(Stream Source, CompressionAlgorithm CompressionAlgorithm)> ResolveLegacyCompressionAsync(
            Stream restored,
            ChunkStorageDescriptor chunk)
        {
            if (chunk.Key != chunk.ContentHash || chunk.CompressionAlgorithm != CompressionHelpers.Algorithm)
            {
                return (restored, chunk.CompressionAlgorithm);
            }

            byte[] prefix = new byte[4];
            int read = 0;
            while (read < prefix.Length)
            {
                int current = await restored
                    .ReadAsync(prefix.AsMemory(read, prefix.Length - read))
                    .ConfigureAwait(false);

                if (current == 0)
                {
                    break;
                }

                read += current;
            }

            var source = new PrefixStream(prefix, read, restored);
            if (IsZstdFrame(prefix.AsSpan(0, read)))
            {
                return (source, chunk.CompressionAlgorithm);
            }

            _logger.LogWarning(
                "Legacy chunk {Key} is marked as {Algorithm}, but its restored payload is not a Zstd frame. Reading it as uncompressed data.",
                chunk.Key,
                chunk.CompressionAlgorithm);

            return (source, CompressionAlgorithm.None);
        }

        private static bool IsZstdFrame(ReadOnlySpan<byte> prefix)
        {
            if (prefix.Length < 4)
            {
                return false;
            }

            bool isStandardFrame =
                prefix[0] == 0x28 &&
                prefix[1] == 0xb5 &&
                prefix[2] == 0x2f &&
                prefix[3] == 0xfd;

            bool isSkippableFrame =
                prefix[0] >= 0x50 &&
                prefix[0] <= 0x5f &&
                prefix[1] == 0x2a &&
                prefix[2] == 0x4d &&
                prefix[3] == 0x18;

            return isStandardFrame || isSkippableFrame;
        }

        private async ValueTask DisposeCurrentChunkAsync()
        {
            if (_currentChunkStream != null)
            {
                await _currentChunkStream.DisposeAsync().ConfigureAwait(false);
                _currentChunkStream = null;
            }
        }

        private async Task FinishCurrentChunkAsync(CancellationToken cancellationToken)
        {
            if (_currentChunkStream == null)
            {
                return;
            }

            byte[] buffer = new byte[8192];
            try
            {
                while (true)
                {
                    int read = await _currentChunkStream
                        .ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)
                        .ConfigureAwait(false);

                    if (read == 0)
                    {
                        return;
                    }

                    throw new InvalidDataException(
                        $"Snapshot file '{_snapshotFile.Path}' produced more data than its recorded size.");
                }
            }
            finally
            {
                await DisposeCurrentChunkAsync().ConfigureAwait(false);
            }
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (_position >= Length || buffer.IsEmpty)
            {
                return 0;
            }

            int totalRead = 0;

            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                _cancellationToken,
                cancellationToken
            );
            var ct = linkedCts.Token;

            try
            {
                while (!buffer.IsEmpty)
                {
                    ct.ThrowIfCancellationRequested();

                    if (_currentChunkStream == null)
                    {
                        bool moved = await MoveToNextChunkAsync().ConfigureAwait(false);
                        if (!moved)
                        {
                            break; // реально конец файла
                        }
                    }

                    if (_currentChunkStream == null)
                    {
                        break;
                    }

                    long remaining = Length - _position;
                    if (remaining <= 0)
                    {
                        await FinishCurrentChunkAsync(ct).ConfigureAwait(false);
                        break;
                    }

                    Memory<byte> target = remaining < buffer.Length
                        ? buffer[..(int)remaining]
                        : buffer;

                    int read = await _currentChunkStream
                        .ReadAsync(target, ct)
                        .ConfigureAwait(false);

                    if (read == 0)
                    {
                        // этот чанк закончился — переходим к следующему
                        await DisposeCurrentChunkAsync().ConfigureAwait(false);
                        continue;
                    }

                    totalRead += read;
                    _position += read;
                    buffer = buffer[read..];

                    if (_position >= Length)
                    {
                        await FinishCurrentChunkAsync(ct).ConfigureAwait(false);
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation(
                    "Read canceled at position {Position} (chunk {Index}/{Total}).",
                    _position,
                    Math.Min(_currentIndex + 1, _chunks.Count),
                    _chunks.Count
                );
                throw;
            }

            return totalRead;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return ReadAsync(buffer.AsMemory(offset, count))
                .AsTask()
                .GetAwaiter()
                .GetResult();
        }

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) =>
            throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        public override void Flush()
        {
            // no-op
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _currentChunkStream?.Dispose();
                _currentChunkStream = null;
            }

            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            await DisposeCurrentChunkAsync().ConfigureAwait(false);
            await base.DisposeAsync().ConfigureAwait(false);
        }

        private sealed class PrefixStream(byte[] prefix, int prefixLength, Stream inner) : Stream
        {
            private int _prefixPosition;

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();

            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (_prefixPosition < prefixLength)
                {
                    int read = Math.Min(count, prefixLength - _prefixPosition);
                    prefix.AsSpan(_prefixPosition, read).CopyTo(buffer.AsSpan(offset, read));
                    _prefixPosition += read;
                    return read;
                }

                return inner.Read(buffer, offset, count);
            }

            public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                if (_prefixPosition < prefixLength)
                {
                    int read = Math.Min(buffer.Length, prefixLength - _prefixPosition);
                    prefix.AsMemory(_prefixPosition, read).CopyTo(buffer);
                    _prefixPosition += read;
                    return read;
                }

                return await inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    inner.Dispose();
                }

                base.Dispose(disposing);
            }

            public override async ValueTask DisposeAsync()
            {
                await inner.DisposeAsync().ConfigureAwait(false);
                await base.DisposeAsync().ConfigureAwait(false);
            }

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override void Flush()
            {
            }
        }
    }
}
