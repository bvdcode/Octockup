// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.Quartz.Attributes;
using Microsoft.Extensions.Options;
using Octockup.Server.Helpers;
using Octockup.Server.Models.Options;
using Octockup.Server.Services;
using Quartz;

namespace Octockup.Server.Jobs
{
    [JobTrigger(days: 365, startNow: false)]
    public class ImportBackupJob(
        ServerBackupImportService _importService,
        IOptions<ServerBackupTransferOptions> _transferOptions,
        ILogger<ImportBackupJob> _logger) : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            CancellationToken cancellationToken = context.CancellationToken;
            string importBaseDirectory = Path.GetFullPath(
                _transferOptions.Value.ImportDirectory);
            if (!Directory.Exists(importBaseDirectory))
            {
                return;
            }

            foreach (string userDirectory in Directory.EnumerateDirectories(importBaseDirectory))
            {
                if (!Guid.TryParse(
                    Path.GetFileName(userDirectory),
                    out Guid userId))
                {
                    _logger.LogWarning(
                        "Skipping import directory with invalid user ID {DirectoryName}.",
                        Path.GetFileName(userDirectory));
                    continue;
                }

                await ProcessUserDirectoryAsync(
                    userId,
                    userDirectory,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task ProcessUserDirectoryAsync(
            Guid userId,
            string userDirectory,
            CancellationToken cancellationToken)
        {
            string searchPattern = "*." + CompressionHelpers.Extension;
            foreach (string importFile in Directory.EnumerateFiles(
                userDirectory,
                searchPattern))
            {
                await ProcessFileAsync(
                    userId,
                    importFile,
                    cancellationToken).ConfigureAwait(false);
            }

            if (!Directory.EnumerateFileSystemEntries(userDirectory).Any())
            {
                Directory.Delete(userDirectory);
            }
        }

        private async Task ProcessFileAsync(
            Guid userId,
            string importFile,
            CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation(
                    "Processing server backup import for user {UserId}.",
                    userId);
                await _importService.ImportAsync(
                    userId,
                    importFile,
                    cancellationToken).ConfigureAwait(false);
                File.Delete(importFile);
                _logger.LogInformation(
                    "Server backup import completed for user {UserId}.",
                    userId);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Server backup import failed for user {UserId}.",
                    userId);
                string failedPath = importFile + ".failed";
                if (File.Exists(failedPath))
                {
                    File.Delete(failedPath);
                }

                File.Move(importFile, failedPath);
            }
        }
    }
}
