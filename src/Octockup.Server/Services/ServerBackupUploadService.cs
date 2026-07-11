// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.Extensions.Options;
using Octockup.Server.Helpers;
using Octockup.Server.Models.Enums;
using Octockup.Server.Models.Options;
using Octockup.Server.Models.Results;
using System.Buffers;

namespace Octockup.Server.Services
{
    public class ServerBackupUploadService(
        IOptions<ServerBackupTransferOptions> _options,
        ILogger<ServerBackupUploadService> _logger)
    {
        private const int BufferSize = 128 * 1024;

        public async Task<ServerBackupUploadResult> SaveAsync(
            Guid userId,
            Stream source,
            long? contentLength,
            CancellationToken cancellationToken)
        {
            ServerBackupTransferOptions options = _options.Value;
            if (contentLength == 0)
            {
                return new ServerBackupUploadResult(ServerBackupUploadStatus.Empty, 0);
            }

            if (contentLength > options.MaximumImportBytes)
            {
                LogRejectedUpload(userId, contentLength.Value, options.MaximumImportBytes);
                return new ServerBackupUploadResult(ServerBackupUploadStatus.TooLarge, 0);
            }

            string importRoot = Path.GetFullPath(options.ImportDirectory);
            string importDirectory = Path.Combine(importRoot, userId.ToString());
            Directory.CreateDirectory(importDirectory);
            string transferId = Guid.NewGuid().ToString("N");
            string fileName = $"import-{transferId}.{CompressionHelpers.Extension}";
            string filePath = Path.Combine(importDirectory, fileName);
            string uploadingPath = filePath + ".uploading";
            bool promoted = false;

            try
            {
                long bytesWritten = 0;
                bool tooLarge = false;
                byte[] buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
                try
                {
                    await using FileStream destination = new(
                        uploadingPath,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None,
                        BufferSize,
                        FileOptions.Asynchronous | FileOptions.SequentialScan);
                    while (true)
                    {
                        int bytesRead = await source
                            .ReadAsync(buffer.AsMemory(0, BufferSize), cancellationToken)
                            .ConfigureAwait(false);
                        if (bytesRead == 0)
                        {
                            break;
                        }

                        if (bytesRead > options.MaximumImportBytes - bytesWritten)
                        {
                            tooLarge = true;
                            break;
                        }

                        await destination
                            .WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken)
                            .ConfigureAwait(false);
                        bytesWritten += bytesRead;
                    }

                    if (!tooLarge && bytesWritten > 0)
                    {
                        await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
                    }
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }

                if (tooLarge)
                {
                    LogRejectedUpload(userId, bytesWritten, options.MaximumImportBytes);
                    return new ServerBackupUploadResult(
                        ServerBackupUploadStatus.TooLarge,
                        bytesWritten);
                }

                if (bytesWritten == 0)
                {
                    return new ServerBackupUploadResult(ServerBackupUploadStatus.Empty, 0);
                }

                File.Move(uploadingPath, filePath);
                promoted = true;
                return new ServerBackupUploadResult(
                    ServerBackupUploadStatus.Saved,
                    bytesWritten);
            }
            finally
            {
                if (!promoted)
                {
                    File.Delete(uploadingPath);
                }
            }
        }

        private void LogRejectedUpload(Guid userId, long bytesReceived, long maximumBytes)
        {
            _logger.LogWarning(
                "Rejected server backup import for user {UserId} after {BytesReceived} bytes; the configured limit is {MaximumBytes} bytes.",
                userId,
                bytesReceived,
                maximumBytes);
        }
    }
}
