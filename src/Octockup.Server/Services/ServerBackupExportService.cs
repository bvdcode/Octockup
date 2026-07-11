// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.Abstractions;
using Microsoft.EntityFrameworkCore;
using Octockup.Server.Database;
using Octockup.Server.Helpers;
using System.IO.Pipelines;
using System.Runtime.ExceptionServices;
using System.Text.Json;

namespace Octockup.Server.Services
{
    public class ServerBackupExportService(
        AppDbContext _dbContext,
        IStreamCipher _streamCipher,
        ILogger<ServerBackupExportService> _logger)
    {
        private const int FlushItemCount = 100;
        private const long PipePauseWriterThreshold = 1024 * 1024;
        private const long PipeResumeWriterThreshold = 512 * 1024;

        public async Task WriteAsync(
            Guid userId,
            bool includeFiles,
            Stream output,
            CancellationToken cancellationToken)
        {
            await using Stream compressedStream =
                CompressionHelpers.CreateCompressionStream(output);
            Pipe pipe = new(new PipeOptions(
                pauseWriterThreshold: PipePauseWriterThreshold,
                resumeWriterThreshold: PipeResumeWriterThreshold,
                useSynchronizationContext: false));
            Task encryptionTask = EncryptAsync(
                pipe.Reader,
                compressedStream,
                cancellationToken);

            Exception? serializationError = null;
            try
            {
                await WriteJsonAsync(
                    pipe.Writer,
                    userId,
                    includeFiles,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                serializationError = ex;
            }
            finally
            {
                await pipe.Writer.CompleteAsync(serializationError).ConfigureAwait(false);
            }

            if (serializationError is not null)
            {
                try
                {
                    await encryptionTask.ConfigureAwait(false);
                }
                catch (Exception encryptionError)
                {
                    _logger.LogDebug(
                        encryptionError,
                        "Server backup encryption stopped after JSON export failed.");
                }

                ExceptionDispatchInfo.Capture(serializationError).Throw();
            }

            await encryptionTask.ConfigureAwait(false);
            await compressedStream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        private async Task WriteJsonAsync(
            PipeWriter pipeWriter,
            Guid userId,
            bool includeFiles,
            CancellationToken cancellationToken)
        {
            using Utf8JsonWriter jsonWriter = new(pipeWriter);
            jsonWriter.WriteStartObject();

            long moduleCount = await WriteModulesAsync(
                jsonWriter,
                pipeWriter,
                userId,
                cancellationToken).ConfigureAwait(false);
            long backupCount = await WriteArrayAsync(
                jsonWriter,
                pipeWriter,
                "Backups",
                _dbContext.Backups
                    .AsNoTracking()
                    .Where(x => x.UserId == userId)
                    .OrderBy(x => x.Id)
                    .AsAsyncEnumerable(),
                cancellationToken).ConfigureAwait(false);
            long scheduleCount = await WriteArrayAsync(
                jsonWriter,
                pipeWriter,
                "Schedules",
                _dbContext.Schedules
                    .AsNoTracking()
                    .Where(x => x.Backup.UserId == userId)
                    .OrderBy(x => x.Id)
                    .AsAsyncEnumerable(),
                cancellationToken).ConfigureAwait(false);
            long snapshotCount = await WriteArrayAsync(
                jsonWriter,
                pipeWriter,
                "Snapshots",
                _dbContext.Snapshots
                    .AsNoTracking()
                    .Where(x => x.Backup.UserId == userId)
                    .OrderBy(x => x.Id)
                    .AsAsyncEnumerable(),
                cancellationToken).ConfigureAwait(false);
            long snapshotFileCount = includeFiles
                ? await WriteArrayAsync(
                    jsonWriter,
                    pipeWriter,
                    "SnapshotFiles",
                    _dbContext.SnapshotFiles
                        .AsNoTracking()
                        .Where(x => x.Snapshot.Backup.UserId == userId)
                        .OrderBy(x => x.Id)
                        .AsAsyncEnumerable(),
                    cancellationToken).ConfigureAwait(false)
                : await WriteEmptyArrayAsync(
                    jsonWriter,
                    pipeWriter,
                    "SnapshotFiles",
                    cancellationToken).ConfigureAwait(false);

            jsonWriter.WriteEndObject();
            await FlushAsync(
                jsonWriter,
                pipeWriter,
                cancellationToken).ConfigureAwait(false);
            _logger.LogInformation(
                "Exported server backup for user {UserId}: {ModuleCount} modules, {BackupCount} backups, {ScheduleCount} schedules, {SnapshotCount} snapshots, {SnapshotFileCount} snapshot files.",
                userId,
                moduleCount,
                backupCount,
                scheduleCount,
                snapshotCount,
                snapshotFileCount);
        }

        private async Task<long> WriteModulesAsync(
            Utf8JsonWriter jsonWriter,
            PipeWriter pipeWriter,
            Guid userId,
            CancellationToken cancellationToken)
        {
            jsonWriter.WritePropertyName("Modules");
            jsonWriter.WriteStartArray();
            long count = 0;
            await foreach (Module module in _dbContext.Modules
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .OrderBy(x => x.Id)
                .AsAsyncEnumerable()
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
            {
#pragma warning disable CS0618 // The transfer format uses decrypted Parameters.
                module.Parameters.Clear();
                foreach (KeyValuePair<string, string> parameter in module
                    .Params(_streamCipher)
                    .Snapshot())
                {
                    module.Parameters[parameter.Key] = parameter.Value;
                }
#pragma warning restore CS0618
                JsonSerializer.Serialize(jsonWriter, module);
                count++;
                if (count % FlushItemCount == 0)
                {
                    await FlushAsync(
                        jsonWriter,
                        pipeWriter,
                        cancellationToken).ConfigureAwait(false);
                }
            }

            jsonWriter.WriteEndArray();
            return count;
        }

        private static async Task<long> WriteArrayAsync<T>(
            Utf8JsonWriter jsonWriter,
            PipeWriter pipeWriter,
            string propertyName,
            IAsyncEnumerable<T> items,
            CancellationToken cancellationToken)
        {
            jsonWriter.WritePropertyName(propertyName);
            jsonWriter.WriteStartArray();
            long count = 0;
            await foreach (T item in items
                .WithCancellation(cancellationToken)
                .ConfigureAwait(false))
            {
                JsonSerializer.Serialize(jsonWriter, item);
                count++;
                if (count % FlushItemCount == 0)
                {
                    await FlushAsync(
                        jsonWriter,
                        pipeWriter,
                        cancellationToken).ConfigureAwait(false);
                }
            }

            jsonWriter.WriteEndArray();
            return count;
        }

        private static async Task<long> WriteEmptyArrayAsync(
            Utf8JsonWriter jsonWriter,
            PipeWriter pipeWriter,
            string propertyName,
            CancellationToken cancellationToken)
        {
            jsonWriter.WritePropertyName(propertyName);
            jsonWriter.WriteStartArray();
            jsonWriter.WriteEndArray();
            await FlushAsync(
                jsonWriter,
                pipeWriter,
                cancellationToken).ConfigureAwait(false);
            return 0;
        }

        private static async Task FlushAsync(
            Utf8JsonWriter jsonWriter,
            PipeWriter pipeWriter,
            CancellationToken cancellationToken)
        {
            jsonWriter.Flush();
            FlushResult result = await pipeWriter
                .FlushAsync(cancellationToken)
                .ConfigureAwait(false);
            if (result.IsCanceled)
            {
                throw new OperationCanceledException(cancellationToken);
            }
            if (result.IsCompleted)
            {
                throw new IOException("Server backup output pipeline closed early.");
            }
        }

        private async Task EncryptAsync(
            PipeReader pipeReader,
            Stream compressedStream,
            CancellationToken cancellationToken)
        {
            Exception? error = null;
            try
            {
                await using Stream inputStream = pipeReader.AsStream(leaveOpen: true);
                await _streamCipher.EncryptAsync(
                    inputStream,
                    compressedStream,
                    ct: cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                error = ex;
                throw;
            }
            finally
            {
                await pipeReader.CompleteAsync(error).ConfigureAwait(false);
            }
        }
    }
}
