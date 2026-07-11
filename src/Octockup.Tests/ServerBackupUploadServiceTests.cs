// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Octockup.Server.Models.Enums;
using Octockup.Server.Models.Options;
using Octockup.Server.Models.Results;
using Octockup.Server.Services;

namespace Octockup.Tests
{
    public class ServerBackupUploadServiceTests
    {
        private string _importDirectory = null!;

        [SetUp]
        public void Setup()
        {
            _importDirectory = Path.Combine(
                Path.GetTempPath(),
                "octockup-upload-tests",
                Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_importDirectory))
            {
                Directory.Delete(_importDirectory, true);
            }
        }

        [Test]
        public async Task SaveAsync_StreamsToOneAtomicallyPromotedFile()
        {
            const long payloadLength = (1024 * 1024) + 17;
            GeneratedReadStream source = new(payloadLength);
            ServerBackupUploadService service = CreateService(2 * 1024 * 1024);

            ServerBackupUploadResult result = await service.SaveAsync(
                Guid.NewGuid(),
                source,
                payloadLength,
                CancellationToken.None);
            string[] completedFiles = Directory.GetFiles(
                _importDirectory,
                "*.oct",
                SearchOption.AllDirectories);
            string[] partialFiles = Directory.GetFiles(
                _importDirectory,
                "*.uploading",
                SearchOption.AllDirectories);

            Assert.Multiple(() =>
            {
                Assert.That(result.Status, Is.EqualTo(ServerBackupUploadStatus.Saved));
                Assert.That(result.BytesWritten, Is.EqualTo(payloadLength));
                Assert.That(completedFiles, Has.Length.EqualTo(1));
                Assert.That(new FileInfo(completedFiles[0]).Length, Is.EqualTo(payloadLength));
                Assert.That(partialFiles, Is.Empty);
                Assert.That(source.MaximumRequestedBufferSize, Is.EqualTo(128 * 1024));
                Assert.That(source.ReadCount, Is.GreaterThan(1));
            });
        }

        [Test]
        public async Task SaveAsync_WhenContentLengthExceedsLimit_RejectsBeforeReading()
        {
            GeneratedReadStream source = new(65);
            ServerBackupUploadService service = CreateService(64);

            ServerBackupUploadResult result = await service.SaveAsync(
                Guid.NewGuid(),
                source,
                65,
                CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(result.Status, Is.EqualTo(ServerBackupUploadStatus.TooLarge));
                Assert.That(result.BytesWritten, Is.Zero);
                Assert.That(source.ReadCount, Is.Zero);
                Assert.That(Directory.Exists(_importDirectory), Is.False);
            });
        }

        [Test]
        public async Task SaveAsync_WhenChunkedBodyExceedsLimit_RemovesPartialFile()
        {
            GeneratedReadStream source = new(65);
            ServerBackupUploadService service = CreateService(64);

            ServerBackupUploadResult result = await service.SaveAsync(
                Guid.NewGuid(),
                source,
                null,
                CancellationToken.None);
            string[] files = Directory.GetFiles(
                _importDirectory,
                "*",
                SearchOption.AllDirectories);

            Assert.Multiple(() =>
            {
                Assert.That(result.Status, Is.EqualTo(ServerBackupUploadStatus.TooLarge));
                Assert.That(result.BytesWritten, Is.Zero);
                Assert.That(source.ReadCount, Is.EqualTo(1));
                Assert.That(files, Is.Empty);
            });
        }

        [Test]
        public async Task SaveAsync_WhenBodyIsEmpty_DoesNotCreateImportFile()
        {
            GeneratedReadStream source = new(0);
            ServerBackupUploadService service = CreateService(64);

            ServerBackupUploadResult result = await service.SaveAsync(
                Guid.NewGuid(),
                source,
                null,
                CancellationToken.None);
            string[] files = Directory.GetFiles(
                _importDirectory,
                "*",
                SearchOption.AllDirectories);

            Assert.Multiple(() =>
            {
                Assert.That(result.Status, Is.EqualTo(ServerBackupUploadStatus.Empty));
                Assert.That(result.BytesWritten, Is.Zero);
                Assert.That(files, Is.Empty);
            });
        }

        private ServerBackupUploadService CreateService(long maximumImportBytes)
        {
            return new ServerBackupUploadService(
                Options.Create(new ServerBackupTransferOptions
                {
                    ImportDirectory = _importDirectory,
                    MaximumImportBytes = maximumImportBytes
                }),
                NullLogger<ServerBackupUploadService>.Instance);
        }

        private class GeneratedReadStream : Stream
        {
            private readonly long _length;
            private long _remaining;

            public GeneratedReadStream(long length)
            {
                _length = length;
                _remaining = length;
            }

            public int MaximumRequestedBufferSize { get; private set; }
            public int ReadCount { get; private set; }
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => _length;
            public override long Position
            {
                get => _length - _remaining;
                set => throw new NotSupportedException();
            }

            public override ValueTask<int> ReadAsync(
                Memory<byte> buffer,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                MaximumRequestedBufferSize = Math.Max(
                    MaximumRequestedBufferSize,
                    buffer.Length);
                ReadCount++;
                int bytesRead = (int)Math.Min(buffer.Length, _remaining);
                buffer.Span[..bytesRead].Fill(0x5A);
                _remaining -= bytesRead;
                return ValueTask.FromResult(bytesRead);
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                int bytesRead = (int)Math.Min(count, _remaining);
                buffer.AsSpan(offset, bytesRead).Fill(0x5A);
                _remaining -= bytesRead;
                return bytesRead;
            }

            public override void Flush()
            {
            }

            public override long Seek(long offset, SeekOrigin origin) =>
                throw new NotSupportedException();

            public override void SetLength(long value) =>
                throw new NotSupportedException();

            public override void Write(byte[] buffer, int offset, int count) =>
                throw new NotSupportedException();
        }
    }
}
