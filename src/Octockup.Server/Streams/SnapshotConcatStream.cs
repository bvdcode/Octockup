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
        CancellationToken _cancellationToken = default) : Stream
    {
        private int _currentIndex = -1;

        private Stream? _currentChunkStream;
        private long _position;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;

        public override long Length => _snapshotFile.Size;

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

            Stream decompressed = chunk.CompressionAlgorithm switch
            {
                CompressionAlgorithm.None => restored,
                CompressionHelpers.Algorithm => CompressionHelpers.CreateDecompressionStream(restored, leaveOpen: false),
                _ => throw new NotSupportedException($"Unsupported compression algorithm: {chunk.CompressionAlgorithm}"),
            };
            _currentChunkStream = decompressed;
            return true;
        }

        private async ValueTask DisposeCurrentChunkAsync()
        {
            if (_currentChunkStream != null)
            {
                await _currentChunkStream.DisposeAsync().ConfigureAwait(false);
                _currentChunkStream = null;
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

                    int read = await _currentChunkStream
                        .ReadAsync(buffer, ct)
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
    }
}
