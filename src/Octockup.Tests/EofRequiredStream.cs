// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Octockup.Tests
{
    internal class EofRequiredStream(byte[] content) : Stream
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

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
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
}
