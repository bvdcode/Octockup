using Octockup.Server.Abstractions;
using Octockup.Server.Helpers;
using Octockup.Server.Models;
using Renci.SshNet;
using Renci.SshNet.Common;
using Renci.SshNet.Sftp;

namespace Octockup.Server.Modules
{
    public class SFTPBackupStorage(ILogger<SFTPBackupStorage> _logger) : IBackupStorage, IDisposable
    {
        public char PathSeparator => '/';
        public string Id => GetType().FullName!;
        public string Name => "SFTP (SSH)";

        public IEnumerable<string> RequiredParameters => [
            "host", "port", "username", "password", "path", "skipPermissionDenied"
        ];

        private string? _path;
        private SftpClient? _sftp;
        private bool _skipPermissionDenied = false;
        private ICollection<string>? _ignoredPaths;

        public void SetParameters(IReadOnlyDictionary<string, string> parameters)
        {
            string host = parameters["host"];
            int port = int.TryParse(parameters["port"], out var p) ? p : 22;
            string username = parameters["username"];
            string password = parameters["password"];

            _path = parameters["path"].Trim().Trim('/');
            _skipPermissionDenied = parameters.TryGetValue("skipPermissionDenied", out var skipStr) &&
                                    bool.TryParse(skipStr, out var skip) && skip;
            _sftp = new SftpClient(host, port, username, password)
            {
                ConnectionInfo = { Timeout = TimeSpan.FromSeconds(30) }
            };
        }

        public void SetIgnoredPaths(ICollection<string>? ignoredPaths)
        {
            _ignoredPaths = ignoredPaths;
        }

        private void EnsureConnected()
        {
            ArgumentNullException.ThrowIfNull(_sftp);

            if (!_sftp.IsConnected)
            {
                _sftp.Connect();
            }
        }

        private string GetRemotePath(string? relative)
        {
            var basePath = _path?.Trim('/');

            if (string.IsNullOrWhiteSpace(basePath))
            {
                if (string.IsNullOrWhiteSpace(relative))
                {
                    return "/";
                }

                return "/" + relative.Trim(PathSeparator);
            }

            if (string.IsNullOrWhiteSpace(relative))
            {
                return "/" + basePath;
            }

            return "/" + basePath + PathSeparator + relative.Trim(PathSeparator);
        }

        private static string NormalizeRemotePath(string path) => path.Replace("\\", "/");

        public async Task<bool?> DeleteAsync(string path, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            EnsureConnected();
            ArgumentNullException.ThrowIfNull(_sftp);

            var remote = NormalizeRemotePath(GetRemotePath(path));

            try
            {
                if (await _sftp.ExistsAsync(remote, cancellationToken))
                {
                    await _sftp.DeleteFileAsync(remote, cancellationToken);
                    return true;
                }

                return false;
            }
            catch
            {
                return null;
            }
        }

        public async Task<bool?> ExistsAsync(string path, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(_sftp);
            ArgumentException.ThrowIfNullOrEmpty(path);
            EnsureConnected();

            var remote = NormalizeRemotePath(GetRemotePath(path));

            try
            {
                var exists = await _sftp.ExistsAsync(remote, cancellationToken);
                return exists;
            }
            catch
            {
                return null;
            }
        }
        public IEnumerable<string> GetDirectories(bool recursive = false, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            ArgumentNullException.ThrowIfNull(_sftp);

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            IEnumerable<string> Walk(string currentRelative)
            {
                string full = NormalizeRemotePath(GetRemotePath(currentRelative));

                // Check if current path is ignored before listing (use full absolute path)
                if (_ignoredPaths != null && ScheduleHelpers.IsPathIgnored(full, null, _ignoredPaths))
                {
                    _logger.LogDebug("Skipping ignored directory: {Path}", full);
                    yield break;
                }

                IEnumerable<ISftpFile> entries;
                try
                {
                    entries = _sftp.ListDirectory(full);
                }
                catch (SftpPermissionDeniedException) when (_skipPermissionDenied)
                {
                    _logger.LogWarning("Permission denied when accessing SFTP directory: {Path}", full);
                    yield break;
                }

                foreach (var entry in entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (entry.Name == "." || entry.Name == "..")
                    {
                        continue;
                    }

                    if (entry.IsSymbolicLink)
                    {
                        continue;
                    }

                    if (!entry.IsDirectory)
                    {
                        continue;
                    }

                    var rel = string.IsNullOrEmpty(currentRelative)
                        ? entry.Name
                        : currentRelative + PathSeparator + entry.Name;

                    var fullEntryPath = full.TrimEnd(PathSeparator) + PathSeparator + entry.Name;

                    // Check if this subdirectory is ignored (use full absolute path)
                    if (_ignoredPaths != null && ScheduleHelpers.IsPathIgnored(fullEntryPath, null, _ignoredPaths))
                    {
                        _logger.LogDebug("Skipping ignored subdirectory: {Name}", fullEntryPath);
                        continue;
                    }

                    if (seen.Add(rel))
                    {
                        yield return rel;
                    }

                    if (recursive)
                    {
                        foreach (var sub in Walk(rel))
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            yield return sub;
                        }
                    }
                }
            }

            foreach (var d in Walk(string.Empty))
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return d;
            }
        }

        public IEnumerable<BackupFileInfo> GetFiles(bool recursive = false, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            ArgumentNullException.ThrowIfNull(_sftp);

            var queue = new Queue<string>();
            queue.Enqueue(string.Empty);

            var seenDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            while (queue.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var currentRelative = queue.Dequeue();
                var full = NormalizeRemotePath(GetRemotePath(currentRelative));

                // Check if current directory is ignored before listing
                // Use FULL path for ignore check, not relative to _path
                if (!string.IsNullOrEmpty(full) && _ignoredPaths != null &&
                    ScheduleHelpers.IsPathIgnored(full, null, _ignoredPaths))
                {
                    _logger.LogDebug("Skipping ignored directory during file enumeration: {Path}", full);
                    continue;
                }

                IEnumerable<ISftpFile> entries;
                try
                {
                    entries = _sftp.ListDirectory(full);
                }
                catch (SftpPermissionDeniedException) when (_skipPermissionDenied)
                {
                    _logger.LogWarning("Permission denied when accessing SFTP directory: {Path}", full);
                    continue;
                }

                foreach (var entry in entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (entry.Name == "." || entry.Name == "..")
                    {
                        continue;
                    }

                    if (entry.IsSymbolicLink)
                    {
                        continue;
                    }

                    var rel = string.IsNullOrEmpty(currentRelative)
                        ? entry.Name
                        : currentRelative + PathSeparator + entry.Name;

                    var fullEntryPath = full.TrimEnd(PathSeparator) + PathSeparator + entry.Name;

                    if (entry.IsDirectory)
                    {
                        // Check if subdirectory is ignored before recursing (use full path)
                        if (_ignoredPaths != null && ScheduleHelpers.IsPathIgnored(fullEntryPath, null, _ignoredPaths))
                        {
                            _logger.LogDebug("Skipping ignored subdirectory during file enumeration: {Name}", fullEntryPath);
                            continue;
                        }

                        if (recursive && seenDirs.Add(rel))
                        {
                            queue.Enqueue(rel);
                        }

                        continue;
                    }

                    if (_skipPermissionDenied && IsClearlyInaccessible(entry))
                    {
                        _logger.LogDebug("Skipping likely inaccessible file: {Name}", entry.FullName);
                        continue;
                    }

                    if (!recursive && rel.Contains(PathSeparator))
                    {
                        continue;
                    }

                    // Check if file itself is ignored (use full path)
                    if (_ignoredPaths != null && ScheduleHelpers.IsPathIgnored(fullEntryPath, entry.Name, _ignoredPaths))
                    {
                        _logger.LogDebug("Skipping ignored file: {Name}", fullEntryPath);
                        continue;
                    }

                    yield return new BackupFileInfo
                    {
                        Path = rel,
                        Name = entry.Name,
                        Size = entry.Attributes.Size,
                        LastModified = entry.LastWriteTime.ToUniversalTime()
                    };
                }
            }
        }

        public async Task<Stream> GetFileStreamAsync(BackupFileInfo file, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(_sftp);
            ArgumentNullException.ThrowIfNull(file);
            ArgumentException.ThrowIfNullOrEmpty(file.Path);
            EnsureConnected();
            var remote = NormalizeRemotePath(GetRemotePath(file.Path));

            try
            {
                // Return a streaming reader to avoid buffering whole file in memory
                var stream = _sftp.OpenRead(remote);
                return stream ?? Stream.Null;
            }
            catch (SftpPermissionDeniedException ex) when (_skipPermissionDenied)
            {
                _logger.LogWarning(ex, "Permission denied when downloading file from SFTP: {Path}", remote);
                return Stream.Null;
            }
        }

        public Task UploadAsync(string path, Stream data, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(_sftp);
            ArgumentException.ThrowIfNullOrEmpty(path);
            EnsureConnected();

            var remote = NormalizeRemotePath(GetRemotePath(path));
            var dir = Path.GetDirectoryName(remote)?.Replace("\\", "/") ?? "/";

            EnsureDirectory(dir);

            return _sftp.UploadFileAsync(data, remote, cancellationToken);
        }

        private void EnsureDirectory(string remoteDir)
        {
            ArgumentNullException.ThrowIfNull(_sftp);

            var parts = remoteDir.Trim('/').Split(
                PathSeparator,
                StringSplitOptions.RemoveEmptyEntries
            );

            string current = "/";

            foreach (var part in parts)
            {
                current = (current.EndsWith('/') ? current : current + "/") + part;

                if (!_sftp.Exists(current))
                {
                    _sftp.CreateDirectory(current);
                }
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            _sftp?.Dispose();
        }

        private static bool IsClearlyInaccessible(ISftpFile entry)
        {
            if (entry.IsSymbolicLink)
            {
                return true;
            }

            // Typical root-only file like /swap.img:
            // - owner is root
            // - only owner can read
            // - group and others cannot read
            if (entry.UserId == 0 &&
                entry.OwnerCanRead &&
                !entry.GroupCanRead &&
                !entry.OthersCanRead)
            {
                return true;
            }

            return false;
        }
    }
}
