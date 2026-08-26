// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Octockup.Server.Streams
{
    internal class PrefixStream(byte[] prefix, int prefixLength, Stream inner) : Stream
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

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
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
