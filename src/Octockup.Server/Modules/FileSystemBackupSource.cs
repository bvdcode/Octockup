// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov | bvdcode | belov.us

using Octockup.Server.Abstractions;
using Octockup.Server.Helpers;
using Octockup.Server.Models;

namespace Octockup.Server.Modules
{
    public class FileSystemBackupSource(ILogger<FileSystemBackupSource> _logger) : IBackupStorage
    {
        public string Name => "File System";
        public string Id => GetType().FullName!;
        public IEnumerable<string> RequiredParameters => GetRequiredParameters();

        public char PathSeparator => Path.DirectorySeparatorChar;

        private static readonly string _rootDirectory =
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "data", "mounts"));

        private string? _password;
        private string _baseDirectory = _rootDirectory;
        private ICollection<string>? _ignoredPaths;
        private const string PasswordFileName = ".password";

        public void SetParameters(IReadOnlyDictionary<string, string> parameters)
        {
            if (!parameters.TryGetValue("path", out var path))
            {
                throw new ArgumentException("Missing required parameter: path");
            }

            path = path.Trim();

            if (string.IsNullOrEmpty(path) || path == "." || path == "/")
            {
                _baseDirectory = _rootDirectory;
                Directory.CreateDirectory(_baseDirectory);
                return;
            }

            if (Path.IsPathRooted(path))
            {
                path = path.TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }

            var combined = Path.GetFullPath(Path.Combine(_rootDirectory, path));

            if (!IsSubPathOf(combined, _rootDirectory))
            {
                throw new ArgumentException($"Path '{path}' escapes the base directory.");
            }
            if (parameters.TryGetValue("password", out var password))
            {
                _password = password;
            }

            _baseDirectory = combined;
            Directory.CreateDirectory(_baseDirectory);
        }

        public void SetIgnoredPaths(ICollection<string>? ignoredPaths)
        {
            _ignoredPaths = ignoredPaths;
        }

        public IEnumerable<BackupFileInfo> GetFiles(bool recursive = false, CancellationToken cancellationToken = default)
        {
            CheckPassword();
            Directory.CreateDirectory(_rootDirectory);
            Directory.CreateDirectory(_baseDirectory);

            if (!IsSubPathOf(_baseDirectory, _rootDirectory))
            {
                throw new InvalidOperationException("Configured base directory is outside of root directory.");
            }

            if (recursive)
            {
                // Use manual traversal to skip ignored directories entirely
                foreach (var file in EnumerateFilesRecursive(_baseDirectory, cancellationToken))
                {
                    yield return file;
                }
            }
            else
            {
                // Non-recursive: just enumerate files in base directory
                foreach (var file in Directory.EnumerateFiles(_baseDirectory, "*", SearchOption.TopDirectoryOnly))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var fileInfo = new FileInfo(file);
                    var relativePath = Path.GetRelativePath(_baseDirectory, file);

                    // Check if file is ignored
                    if (_ignoredPaths != null && ScheduleHelpers.IsPathIgnored(PathSeparator + relativePath, fileInfo.Name, _ignoredPaths))
                    {
                        _logger.LogDebug("Skipping ignored file: {Name}", relativePath);
                        continue;
                    }

                    yield return new BackupFileInfo
                    {
                        Path = relativePath,
                        Name = fileInfo.Name,
                        Size = fileInfo.Length,
                        LastModified = fileInfo.LastWriteTimeUtc,
                    };
                }
            }
        }

        private IEnumerable<BackupFileInfo> EnumerateFilesRecursive(string directory, CancellationToken cancellationToken)
        {
            // First, enumerate files in current directory
            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Access denied to directory: {Directory}", directory);
                yield break;
            }
            catch (DirectoryNotFoundException)
            {
                yield break;
            }

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var fileInfo = new FileInfo(file);
                var relativePath = Path.GetRelativePath(_baseDirectory, file);

                // Check if file is ignored
                if (_ignoredPaths != null && ScheduleHelpers.IsPathIgnored(PathSeparator + relativePath, fileInfo.Name, _ignoredPaths))
                {
                    _logger.LogDebug("Skipping ignored file: {Name}", relativePath);
                    continue;
                }

                yield return new BackupFileInfo
                {
                    Path = relativePath,
                    Name = fileInfo.Name,
                    Size = fileInfo.Length,
                    LastModified = fileInfo.LastWriteTimeUtc,
                };
            }

            // Then, recursively enumerate subdirectories (skipping ignored ones)
            IEnumerable<string> subdirs;
            try
            {
                subdirs = Directory.EnumerateDirectories(directory, "*", SearchOption.TopDirectoryOnly);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Access denied to enumerate subdirectories of: {Directory}", directory);
                yield break;
            }
            catch (DirectoryNotFoundException)
            {
                yield break;
            }

            foreach (var subdir in subdirs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relativePath = Path.GetRelativePath(_baseDirectory, subdir);

                // Check if directory is ignored - skip entire subtree if so
                if (_ignoredPaths != null && ScheduleHelpers.IsDirectoryIgnored(relativePath, _ignoredPaths, PathSeparator))
                {
                    _logger.LogDebug("Skipping ignored directory: {Name}", relativePath);
                    continue;
                }

                // Recurse into non-ignored directories
                foreach (var file in EnumerateFilesRecursive(subdir, cancellationToken))
                {
                    yield return file;
                }
            }
        }

        public IEnumerable<string> GetDirectories(bool recursive = false, CancellationToken cancellationToken = default)
        {
            CheckPassword();
            Directory.CreateDirectory(_rootDirectory);
            Directory.CreateDirectory(_baseDirectory);

            if (!IsSubPathOf(_baseDirectory, _rootDirectory))
            {
                throw new InvalidOperationException("Configured base directory is outside of root directory.");
            }

            if (recursive)
            {
                // Use manual traversal to skip ignored directories entirely
                foreach (var dir in EnumerateDirectoriesRecursive(_baseDirectory, cancellationToken))
                {
                    yield return dir;
                }
            }
            else
            {
                // Non-recursive: just enumerate directories in base directory
                foreach (var dir in Directory.EnumerateDirectories(_baseDirectory, "*", SearchOption.TopDirectoryOnly))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var relativePath = Path.GetRelativePath(_baseDirectory, dir);

                    // Check if directory is ignored
                    if (_ignoredPaths != null && ScheduleHelpers.IsDirectoryIgnored(relativePath, _ignoredPaths, PathSeparator))
                    {
                        _logger.LogDebug("Skipping ignored directory: {Name}", relativePath);
                        continue;
                    }

                    yield return relativePath;
                }
            }
        }

        private IEnumerable<string> EnumerateDirectoriesRecursive(string directory, CancellationToken cancellationToken)
        {
            IEnumerable<string> subdirs;
            try
            {
                subdirs = Directory.EnumerateDirectories(directory, "*", SearchOption.TopDirectoryOnly);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Access denied to enumerate subdirectories of: {Directory}", directory);
                yield break;
            }
            catch (DirectoryNotFoundException)
            {
                yield break;
            }

            foreach (var subdir in subdirs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var relativePath = Path.GetRelativePath(_baseDirectory, subdir);

                // Check if directory is ignored - skip entire subtree if so
                if (_ignoredPaths != null && ScheduleHelpers.IsDirectoryIgnored(relativePath, _ignoredPaths, PathSeparator))
                {
                    _logger.LogDebug("Skipping ignored directory: {Name}", relativePath);
                    continue;
                }

                // Return this directory
                yield return relativePath;

                // Recurse into non-ignored directories
                foreach (var nestedDir in EnumerateDirectoriesRecursive(subdir, cancellationToken))
                {
                    yield return nestedDir;
                }
            }
        }

        private static bool IsSubPathOf(string path, string baseDir)
        {
            var normalizedPath = Path.GetFullPath(
                path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            var normalizedBase = Path.GetFullPath(
                baseDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            return normalizedPath.Equals(normalizedBase, StringComparison.OrdinalIgnoreCase)
                   || normalizedPath.StartsWith(
                       normalizedBase + Path.DirectorySeparatorChar,
                       StringComparison.OrdinalIgnoreCase);
        }

        public Task<Stream> GetFileStreamAsync(BackupFileInfo file, CancellationToken cancellationToken = default)
        {
            CheckPassword();
            var fullPath = Path.GetFullPath(Path.Combine(_baseDirectory, file.Path));
            if (!IsSubPathOf(fullPath, _baseDirectory))
            {
                throw new ArgumentException($"File path '{file.Path}' escapes the base directory.");
            }

            try
            {
                Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                return Task.FromResult(stream);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to open file stream for '{FilePath}'", fullPath);
                return Task.FromResult(Stream.Null);
            }
        }

        public Task<bool?> ExistsAsync(string path, CancellationToken cancellationToken = default)
        {
            CheckPassword();
            var fullPath = Path.GetFullPath(Path.Combine(_baseDirectory, path));
            if (!IsSubPathOf(fullPath, _baseDirectory))
            {
                throw new ArgumentException($"File path '{path}' escapes the base directory.");
            }
            bool exists = File.Exists(fullPath);
            return Task.FromResult<bool?>(exists);
        }

        public Task<bool?> DeleteAsync(string path, CancellationToken cancellationToken = default)
        {
            CheckPassword();
            var fullPath = Path.GetFullPath(Path.Combine(_baseDirectory, path));
            if (!IsSubPathOf(fullPath, _baseDirectory))
            {
                throw new ArgumentException($"File path '{path}' escapes the base directory.");
            }
            if (!File.Exists(fullPath))
            {
                return Task.FromResult<bool?>(null);
            }
            try
            {
                File.Delete(fullPath);
                return Task.FromResult<bool?>(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete file '{FilePath}'", fullPath);
                return Task.FromResult<bool?>(false);
            }
        }

        public async Task UploadAsync(string path, Stream data, CancellationToken cancellationToken = default)
        {
            CheckPassword();
            var fullPath = Path.GetFullPath(Path.Combine(_baseDirectory, path));
            if (!IsSubPathOf(fullPath, _baseDirectory))
            {
                throw new ArgumentException($"File path '{path}' escapes the base directory.");
            }
            string tempFile = fullPath + ".tmp";
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            using (var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await data.CopyToAsync(fileStream, cancellationToken);
            }
            File.Move(tempFile, fullPath, true);
        }

        private IEnumerable<string> GetRequiredParameters()
        {
            bool hasPasswordFile = File.Exists(Path.Combine(_baseDirectory, PasswordFileName));
            if (hasPasswordFile)
            {
                return ["path", "password"];
            }
            return ["path"];
        }

        private void CheckPassword()
        {
            string pathToPasswordFile = Path.Combine(_baseDirectory, PasswordFileName);
            if (!File.Exists(pathToPasswordFile))
            {
                // No password file means no password protection
                return;
            }
            var storedPassword = File.ReadAllText(pathToPasswordFile).Trim();
            if (string.IsNullOrEmpty(storedPassword))
            {
                // Empty password file means no password protection
                return;
            }
            if (_password != storedPassword)
            {
                throw new UnauthorizedAccessException("Invalid password for accessing the file system backup source.");
            }
        }

        public Task<BackupFileInfo?> GetFileInfoAsync(string path, CancellationToken cancellationToken)
        {
            CheckPassword();
            var fullPath = Path.GetFullPath(Path.Combine(_baseDirectory, path));
            if (!IsSubPathOf(fullPath, _baseDirectory))
            {
                throw new ArgumentException($"File path '{path}' escapes the base directory.");
            }

            if (!File.Exists(fullPath))
            {
                return Task.FromResult<BackupFileInfo?>(null);
            }

            try
            {
                var fileInfo = new FileInfo(fullPath);
                var relativePath = Path.GetRelativePath(_baseDirectory, fullPath);

                var result = new BackupFileInfo
                {
                    Path = relativePath,
                    Name = fileInfo.Name,
                    Size = fileInfo.Length,
                    LastModified = fileInfo.LastWriteTimeUtc,
                };

                return Task.FromResult<BackupFileInfo?>(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get file info for '{FilePath}'", fullPath);
                return Task.FromResult<BackupFileInfo?>(null);
            }
        }
    }
}
