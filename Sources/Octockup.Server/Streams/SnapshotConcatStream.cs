using Octockup.Server.Models;
using Octockup.Server.Helpers;
using Octockup.Server.Database;
using Octockup.Server.Abstractions;

namespace Octockup.Server.Streams
{
    public sealed class SnapshotConcatStream(
        IBackupStorage storage,
        IReadOnlyList<string> hashes,
        SnapshotFile snapshotFile,
        CancellationToken cancellationToken = default) : Stream
    {
        private readonly IBackupStorage _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        private readonly IReadOnlyList<string> _hashes = hashes ?? throw new ArgumentNullException(nameof(hashes));
        private readonly SnapshotFile _snapshotFile = snapshotFile ?? throw new ArgumentNullException(nameof(snapshotFile));
        private readonly CancellationToken _cancellationToken = cancellationToken;

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
            if (_currentIndex >= _hashes.Count)
            {
                _currentChunkStream = null;
                return false;
            }

            string hash = _hashes[_currentIndex];
            string path = ScheduleHelpers.SplitHash(hash, storage.PathSeparator);

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

                if (_currentChunkStream == null)
                {
                    break;
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
                buffer = buffer[read..];

                break;
            }

            return totalRead;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();
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
