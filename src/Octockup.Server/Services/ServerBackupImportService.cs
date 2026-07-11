// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Octockup.Server.Database;
using Octockup.Server.Helpers;
using System.IO.Pipelines;

namespace Octockup.Server.Services
{
    public class ServerBackupImportService(
        AppDbContext _dbContext,
        IStreamCipher _crypto,
        ServerBackupJsonStreamReader _jsonReader,
        ILogger<ServerBackupImportService> _logger)
    {
        private const long PipePauseWriterThreshold = 1024 * 1024;
        private const long PipeResumeWriterThreshold = 512 * 1024;

        public async Task ImportAsync(
            Guid userId,
            string filePath,
            CancellationToken cancellationToken)
        {
            bool userExists = await _dbContext.Users
                .AsNoTracking()
                .AnyAsync(x => x.Id == userId, cancellationToken)
                .ConfigureAwait(false);
            if (!userExists)
            {
                throw new InvalidOperationException(
                    $"Import target user {userId} does not exist.");
            }

            await using FileStream fileStream = new(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                128 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await using Stream decompressedStream =
                CompressionHelpers.CreateDecompressionStream(fileStream);
            Pipe pipe = new(new PipeOptions(
                pauseWriterThreshold: PipePauseWriterThreshold,
                resumeWriterThreshold: PipeResumeWriterThreshold,
                useSynchronizationContext: false));
            Task decryptionTask = DecryptAsync(
                decompressedStream,
                pipe.Writer,
                cancellationToken);
            ServerBackupImportBatchWriter batchWriter = new(
                _dbContext,
                _crypto,
                userId,
                _logger);

            await using IDbContextTransaction transaction = await _dbContext.Database
                .BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
            try
            {
                await _jsonReader.ReadAsync(
                    pipe.Reader,
                    batchWriter.ProcessAsync,
                    cancellationToken).ConfigureAwait(false);
                await decryptionTask.ConfigureAwait(false);
                await batchWriter.CompleteAsync(cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception importError)
            {
                await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
                try
                {
                    await decryptionTask.ConfigureAwait(false);
                }
                catch (Exception decryptionError)
                {
                    _logger.LogDebug(
                        decryptionError,
                        "Server backup decryption stopped after import failed.");
                }

                _logger.LogError(
                    importError,
                    "Server backup import failed for user {UserId}.",
                    userId);
                throw;
            }
            finally
            {
                _dbContext.ChangeTracker.Clear();
            }

            IReadOnlyList<long> counts = batchWriter.SectionCounts;
            _logger.LogInformation(
                "Imported server backup for user {UserId}: {ModuleCount} modules, {BackupCount} backups, {ScheduleCount} schedules, {SnapshotCount} snapshots, {SnapshotFileCount} snapshot files.",
                userId,
                counts[0],
                counts[1],
                counts[2],
                counts[3],
                counts[4]);
        }

        private async Task DecryptAsync(
            Stream decompressedStream,
            PipeWriter pipeWriter,
            CancellationToken cancellationToken)
        {
            Exception? error = null;
            try
            {
                await using Stream outputStream = pipeWriter.AsStream(leaveOpen: true);
                await _crypto.DecryptAsync(
                    decompressedStream,
                    outputStream,
                    ct: cancellationToken).ConfigureAwait(false);
                await outputStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                error = ex;
                throw;
            }
            finally
            {
                await pipeWriter.CompleteAsync(error).ConfigureAwait(false);
            }
        }
    }
}
