// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Octockup.Server.Helpers;
using Octockup.Server.Models;
using Renci.SshNet.Common;
using Renci.SshNet.Sftp;

namespace Octockup.Server.Modules
{
    public partial class SFTPBackupStorage
    {
        private IEnumerable<string> EnumerateDirectories(
            string currentRelative,
            bool recursive,
            ISet<string> seen,
            CancellationToken cancellationToken)
        {
            string full = NormalizeRemotePath(GetRemotePath(currentRelative));
            if (IsIgnoredDirectory(full))
            {
                yield break;
            }

            IEnumerable<ISftpFile>? entries = GetDirectoryEntries(full);
            if (entries is null)
            {
                yield break;
            }

            foreach (ISftpFile entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string? relativePath = GetDirectoryRelativePath(entry, currentRelative, full);
                if (relativePath is null)
                {
                    continue;
                }

                if (seen.Add(relativePath))
                {
                    yield return relativePath;
                }

                if (!recursive)
                {
                    continue;
                }

                foreach (string child in EnumerateDirectories(
                    relativePath,
                    recursive: true,
                    seen,
                    cancellationToken))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    yield return child;
                }
            }
        }

        private IEnumerable<ISftpFile>? GetDirectoryEntries(string fullPath)
        {
            ArgumentNullException.ThrowIfNull(_sftp);
            try
            {
                return _sftp.ListDirectory(fullPath);
            }
            catch (SftpPermissionDeniedException exception) when (_skipPermissionDenied)
            {
                _logger.LogWarning(
                    exception,
                    "Permission denied when accessing SFTP directory: {Path}",
                    fullPath);
                return null;
            }
        }

        private string? GetDirectoryRelativePath(
            ISftpFile entry,
            string currentRelative,
            string currentFullPath)
        {
            if (ShouldSkipEntry(entry) || !entry.IsDirectory)
            {
                return null;
            }

            string fullPath = GetEntryFullPath(currentFullPath, entry.Name);
            if (IsIgnoredDirectory(fullPath))
            {
                return null;
            }

            return GetEntryRelativePath(currentRelative, entry.Name);
        }

        private BackupFileInfo? ProcessEntry(
            ISftpFile entry,
            string currentRelative,
            string currentFullPath,
            bool recursive,
            ISet<string> seenDirectories,
            Queue<string> queue)
        {
            if (ShouldSkipEntry(entry))
            {
                return null;
            }

            string relativePath = GetEntryRelativePath(currentRelative, entry.Name);
            string fullPath = GetEntryFullPath(currentFullPath, entry.Name);
            if (entry.IsDirectory)
            {
                QueueDirectory(relativePath, fullPath, recursive, seenDirectories, queue);
                return null;
            }

            return CreateFileInfo(entry, relativePath, fullPath, recursive);
        }

        private void QueueDirectory(
            string relativePath,
            string fullPath,
            bool recursive,
            ISet<string> seenDirectories,
            Queue<string> queue)
        {
            if (IsIgnoredDirectory(fullPath))
            {
                return;
            }

            if (recursive && seenDirectories.Add(relativePath))
            {
                queue.Enqueue(relativePath);
            }
        }

        private BackupFileInfo? CreateFileInfo(
            ISftpFile entry,
            string relativePath,
            string fullPath,
            bool recursive)
        {
            if (_skipPermissionDenied && IsClearlyInaccessible(entry))
            {
                _logger.LogDebug("Skipping likely inaccessible file: {Name}", entry.FullName);
                return null;
            }

            if (!recursive && relativePath.Contains(PathSeparator))
            {
                return null;
            }

            if (_ignoredPaths is not null
                && ScheduleHelpers.IsPathIgnored(fullPath, entry.Name, _ignoredPaths))
            {
                _logger.LogDebug("Skipping ignored file: {Name}", fullPath);
                return null;
            }

            return new BackupFileInfo
            {
                Path = relativePath,
                Name = entry.Name,
                Size = entry.Attributes.Size,
                LastModified = entry.LastWriteTime.ToUniversalTime(),
            };
        }

        private bool IsIgnoredDirectory(string fullPath)
        {
            if (_ignoredPaths is null
                || !ScheduleHelpers.IsPathIgnored(fullPath, fileName: null, _ignoredPaths))
            {
                return false;
            }

            _logger.LogDebug("Skipping ignored SFTP directory: {Path}", fullPath);
            return true;
        }

        private static bool ShouldSkipEntry(ISftpFile entry)
        {
            return entry.Name is "." or ".." || entry.IsSymbolicLink;
        }

        private string GetEntryRelativePath(string currentRelative, string entryName)
        {
            return string.IsNullOrEmpty(currentRelative)
                ? entryName
                : currentRelative + PathSeparator + entryName;
        }

        private string GetEntryFullPath(string currentFullPath, string entryName)
        {
            return currentFullPath.TrimEnd(PathSeparator) + PathSeparator + entryName;
        }
    }
}
