using Octockup.Server.Abstractions;
using Octockup.Server.Database;
using Octockup.Server.Helpers;
using Octockup.Server.Models;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Octockup.Server.Streams
{
    public sealed class SnapshotConcatStream : Stream
    {
        private readonly IBackupStorage _storage;
        private readonly IReadOnlyList<string> _hashes;
        private readonly SnapshotFile _snapshotFile;
        private readonly CancellationToken _cancellationToken;

        private int _currentIndex = -1;
        private Stream? _currentChunkStream;
        private long _position;

        public SnapshotConcatStream(
            IBackupStorage storage,
            IReadOnlyList<string> hashes,
            SnapshotFile snapshotFile,
            CancellationToken cancellationToken = default)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _hashes = hashes ?? throw new ArgumentNullException(nameof(hashes));
            _snapshotFile = snapshotFile ?? throw new ArgumentNullException(nameof(snapshotFile));
            _cancellationToken = cancellationToken;
        }

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
            if (_currentIndex >= _hashes.Count)
            {
                _currentChunkStream = null;
                return false;
            }

            string hash = _hashes[_currentIndex];
            string path = PathHelpers.GetPath(hash);

            bool? exists = await _storage.ExistsAsync(path).ConfigureAwait(false);
            if (exists != true)
            {
                throw new IOException($"Chunk '{hash}' not found in storage.");
            }

            var fileInfo = new BackupFileInfo
            {
                Path = path,
                Name = _snapshotFile.Name,
                Size = _snapshotFile.Size,
                LastModified = _snapshotFile.LastModified,
            };

            _currentChunkStream = await _storage
                .GetFileStreamAsync(fileInfo)
                .ConfigureAwait(false);

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
            if (_position >= Length)
            {
                return 0;
            }

            int totalRead = 0;
            using var linkedCts = CancellationTokenSource
                .CreateLinkedTokenSource(_cancellationToken, cancellationToken);
            var ct = linkedCts.Token;

            while (!buffer.IsEmpty)
            {
                if (_currentChunkStream == null)
                {
                    bool moved = await MoveToNextChunkAsync().ConfigureAwait(false);
                    if (!moved)
                    {
                        break;
                    }
                }

                int read = await _currentChunkStream
                    .ReadAsync(buffer, ct)
                    .ConfigureAwait(false);

                if (read == 0)
                {
                    await DisposeCurrentChunkAsync().ConfigureAwait(false);
                    continue;
                }

                totalRead += read;
                _position += read;
                buffer = buffer.Slice(read);

                break;
            }

            return totalRead;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return ReadAsync(buffer.AsMemory(offset, count)).GetAwaiter().GetResult();
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
