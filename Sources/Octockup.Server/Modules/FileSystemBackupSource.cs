// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using Octockup.Server.Models;
using Octockup.Server.Abstractions;

namespace Octockup.Server.Modules
{
    public class FileSystemBackupSource(ILogger<FileSystemBackupSource> _logger) : IBackupStorage
    {
        public string Name => "File System";
        public string Id => GetType().FullName!;
        public IEnumerable<string> RequiredParameters => ["path"];
        public char PathSeparator => Path.DirectorySeparatorChar;

        private static readonly string _rootDirectory =
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "data", "mounts"));

        private string _baseDirectory = _rootDirectory;

        public void SetParameters(Dictionary<string, string> parameters)
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

            _baseDirectory = combined;
            Directory.CreateDirectory(_baseDirectory);
        }

        public IEnumerable<BackupFileInfo> GetFiles(bool recursive = false)
        {
            Directory.CreateDirectory(_rootDirectory);
            Directory.CreateDirectory(_baseDirectory);

            if (!IsSubPathOf(_baseDirectory, _rootDirectory))
            {
                throw new InvalidOperationException("Configured base directory is outside of root directory.");
            }

            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var files = Directory.EnumerateFiles(_baseDirectory, "*", searchOption);

            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                var relativePath = Path.GetRelativePath(_baseDirectory, file);
                
                yield return new BackupFileInfo
                {
                    Path = relativePath,
                    Name = fileInfo.Name,
                    Size = fileInfo.Length,
                    LastModified = fileInfo.LastWriteTimeUtc,
                };
            }
        }

        public IEnumerable<string> GetDirectories(bool recursive = false)
        {
            Directory.CreateDirectory(_rootDirectory);
            Directory.CreateDirectory(_baseDirectory);

            if (!IsSubPathOf(_baseDirectory, _rootDirectory))
            {
                throw new InvalidOperationException("Configured base directory is outside of root directory.");
            }

            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var directories = Directory.EnumerateDirectories(_baseDirectory, "*", searchOption);

            foreach (var dir in directories)
            {
                var relativePath = Path.GetRelativePath(_baseDirectory, dir);
                yield return relativePath;
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

        public Task<Stream> GetFileStreamAsync(BackupFileInfo file)
        {
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

        public Task<bool?> ExistsAsync(string path)
        {
            var fullPath = Path.GetFullPath(Path.Combine(_baseDirectory, path));
            if (!IsSubPathOf(fullPath, _baseDirectory))
            {
                throw new ArgumentException($"File path '{path}' escapes the base directory.");
            }
            bool exists = File.Exists(fullPath);
            return Task.FromResult<bool?>(exists);
        }

        public Task<bool?> DeleteAsync(string path)
        {
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

        public Task UploadAsync(string path, Stream data)
        {
            var fullPath = Path.GetFullPath(Path.Combine(_baseDirectory, path));
            if (!IsSubPathOf(fullPath, _baseDirectory))
            {
                throw new ArgumentException($"File path '{path}' escapes the base directory.");
            }
            string tempFile = fullPath + ".tmp";
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            using (var fileStream = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                data.CopyTo(fileStream);
            }
            File.Move(tempFile, fullPath, true);
            return Task.CompletedTask;
        }
    }
}
