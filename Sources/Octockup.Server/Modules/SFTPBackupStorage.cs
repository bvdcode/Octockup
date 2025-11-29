using Renci.SshNet;
using Renci.SshNet.Sftp;
using Renci.SshNet.Common;
using Octockup.Server.Models;
using Octockup.Server.Abstractions;

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

        public void SetParameters(Dictionary<string, string> parameters)
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

        // Enumerate entries lazily without materializing the entire list
        private static IEnumerable<ISftpFile> EnumerateSync(IAsyncEnumerable<ISftpFile> source)
        {
            var e = source.GetAsyncEnumerator(CancellationToken.None);
            try
            {
                while (e.MoveNextAsync().AsTask().GetAwaiter().GetResult())
                {
                    yield return e.Current;
                }
            }
            finally
            {
                e.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
        }

        public async Task<bool?> DeleteAsync(string path)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            EnsureConnected();
            ArgumentNullException.ThrowIfNull(_sftp);

            var remote = NormalizeRemotePath(GetRemotePath(path));

            try
            {
                if (await _sftp.ExistsAsync(remote, CancellationToken.None))
                {
                    await _sftp.DeleteFileAsync(remote, CancellationToken.None);
                    return true;
                }

                return false;
            }
            catch
            {
                return null;
            }
        }

        public async Task<bool?> ExistsAsync(string path)
        {
            ArgumentNullException.ThrowIfNull(_sftp);
            ArgumentException.ThrowIfNullOrEmpty(path);
            EnsureConnected();

            var remote = NormalizeRemotePath(GetRemotePath(path));

            try
            {
                var exists = await _sftp.ExistsAsync(remote, CancellationToken.None);
                return exists;
            }
            catch
            {
                return null;
            }
        }
        public IEnumerable<string> GetDirectories(bool recursive = false)
        {
            EnsureConnected();
            ArgumentNullException.ThrowIfNull(_sftp);

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            IEnumerable<string> Walk(string currentRelative)
            {
                string full = NormalizeRemotePath(GetRemotePath(currentRelative));
                IEnumerable<ISftpFile> entries;
                try
                {
                    entries = EnumerateSync(_sftp.ListDirectoryAsync(full, CancellationToken.None));
                }
                catch (SftpPermissionDeniedException) when (_skipPermissionDenied)
                {
                    _logger.LogWarning("Permission denied when accessing SFTP directory: {Path}", full);
                    yield break;
                }

                foreach (var entry in entries)
                {
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

                    if (seen.Add(rel))
                    {
                        yield return rel;
                    }

                    if (recursive)
                    {
                        foreach (var sub in Walk(rel))
                        {
                            yield return sub;
                        }
                    }
                }
            }

            foreach (var d in Walk(string.Empty))
            {
                yield return d;
            }
        }

        public IEnumerable<BackupFileInfo> GetFiles(bool recursive = false)
        {
            EnsureConnected();
            ArgumentNullException.ThrowIfNull(_sftp);

            IEnumerable<BackupFileInfo> Walk(string currentRelative)
            {
                string full = NormalizeRemotePath(GetRemotePath(currentRelative));
                IEnumerable<ISftpFile> entries;
                try
                {
                    entries = EnumerateSync(_sftp.ListDirectoryAsync(full, CancellationToken.None));
                }
                catch (SftpPermissionDeniedException) when (_skipPermissionDenied)
                {
                    _logger.LogWarning("Permission denied when accessing SFTP directory: {Path}", full);
                    yield break;
                }

                foreach (var entry in entries)
                {
                    if (entry.Name == "." || entry.Name == "..")
                    {
                        continue;
                    }

                    if (entry.IsSymbolicLink)
                    {
                        continue;
                    }

                    if (entry.UserId == 0 &&
                        entry.OwnerCanRead &&
                        !entry.GroupCanRead &&
                        !entry.OthersCanRead)
                    {
                        continue;
                    }

                    if (entry.IsDirectory)
                    {
                        if (recursive)
                        {
                            var next = string.IsNullOrEmpty(currentRelative)
                                ? entry.Name
                                : currentRelative + PathSeparator + entry.Name;

                            foreach (var sub in Walk(next))
                            {
                                yield return sub;
                            }
                        }

                        continue;
                    }

                    var rel = string.IsNullOrEmpty(currentRelative)
                        ? entry.Name
                        : currentRelative + PathSeparator + entry.Name;

                    if (!recursive && rel.Contains(PathSeparator))
                    {
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

            foreach (var f in Walk(string.Empty))
            {
                yield return f;
            }
        }


        public async Task<Stream> GetFileStreamAsync(BackupFileInfo file)
        {
            ArgumentNullException.ThrowIfNull(_sftp);
            ArgumentNullException.ThrowIfNull(file);
            ArgumentException.ThrowIfNullOrEmpty(file.Path);
            EnsureConnected();
            var remote = NormalizeRemotePath(GetRemotePath(file.Path));

            var ms = new MemoryStream();
            try
            {
                await _sftp.DownloadFileAsync(remote, ms, CancellationToken.None);
                ms.Position = 0;
                return ms;
            }
            catch (SftpPermissionDeniedException ex) when (_skipPermissionDenied)
            {
                _logger.LogWarning(ex, "Permission denied when downloading file from SFTP: {Path}", remote);
                ms.Dispose();
                return Stream.Null;
            }
        }   

        public Task UploadAsync(string path, Stream data)
        {
            ArgumentNullException.ThrowIfNull(_sftp);
            ArgumentException.ThrowIfNullOrEmpty(path);
            EnsureConnected();

            var remote = NormalizeRemotePath(GetRemotePath(path));
            var dir = Path.GetDirectoryName(remote)?.Replace("\\", "/") ?? "/";

            EnsureDirectory(dir);

            return _sftp.UploadFileAsync(data, remote, CancellationToken.None);
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
    }
}
