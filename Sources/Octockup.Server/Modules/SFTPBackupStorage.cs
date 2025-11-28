using Renci.SshNet;
using Renci.SshNet.Sftp;
using Octockup.Server.Models;
using Octockup.Server.Abstractions;
using System.Threading;

namespace Octockup.Server.Modules
{
    public class SFTPBackupStorage : IBackupStorage, IDisposable
    {
        public char PathSeparator => '/';
        public string Id => GetType().FullName!;
        public string Name => "SFTP (SSH)";

        public IEnumerable<string> RequiredParameters => [
            "host", "port", "username", "password", "path"
        ];

        private string? _path;
        private SftpClient? _sftp;

        public void SetParameters(Dictionary<string, string> parameters)
        {
            string host = parameters["host"];
            int port = int.TryParse(parameters["port"], out var p) ? p : 22;
            string username = parameters["username"];
            string password = parameters["password"];

            _path = parameters["path"].Trim().Trim('/');

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

        /// <summary>
        /// Возвращает абсолютный путь (всегда с ведущим "/"),
        /// учитывая базовый _path и относительный путь внутри него.
        /// </summary>
        private string GetRemotePath(string? relative)
        {
            var basePath = _path?.Trim('/');

            if (string.IsNullOrWhiteSpace(basePath))
            {
                if (string.IsNullOrWhiteSpace(relative))
                    return "/";

                return "/" + relative.Trim(PathSeparator);
            }

            if (string.IsNullOrWhiteSpace(relative))
                return "/" + basePath;

            return "/" + basePath + PathSeparator + relative.Trim(PathSeparator);
        }

        private static string NormalizeRemotePath(string path)
            => path.Replace("\\", "/");

        private static List<ISftpFile> ToListSync(IAsyncEnumerable<ISftpFile> source)
        {
            var list = new List<ISftpFile>();
            var e = source.GetAsyncEnumerator(CancellationToken.None);

            try
            {
                while (e.MoveNextAsync().AsTask().GetAwaiter().GetResult())
                {
                    list.Add(e.Current);
                }
            }
            finally
            {
                e.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }

            return list;
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

            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void Walk(string currentRelative)
            {
                var full = NormalizeRemotePath(GetRemotePath(currentRelative));
                var entries = ToListSync(_sftp.ListDirectoryAsync(full, CancellationToken.None));

                foreach (var entry in entries)
                {
                    if (entry.Name == "." || entry.Name == "..")
                        continue;

                    if (!entry.IsDirectory)
                        continue;

                    var rel = string.IsNullOrEmpty(currentRelative)
                        ? entry.Name
                        : currentRelative + PathSeparator + entry.Name;

                    result.Add(rel);

                    if (recursive)
                    {
                        Walk(rel);
                    }
                }
            }

            Walk(string.Empty);
            return result;
        }

        public IEnumerable<BackupFileInfo> GetFiles(bool recursive = false)
        {
            EnsureConnected();
            ArgumentNullException.ThrowIfNull(_sftp);

            var files = new List<BackupFileInfo>();

            void Walk(string currentRelative)
            {
                var full = NormalizeRemotePath(GetRemotePath(currentRelative));
                var entries = ToListSync(_sftp.ListDirectoryAsync(full, CancellationToken.None));

                foreach (var entry in entries)
                {
                    if (entry.Name == "." || entry.Name == "..")
                        continue;

                    if (entry.IsDirectory)
                    {
                        if (recursive)
                        {
                            var next = string.IsNullOrEmpty(currentRelative)
                                ? entry.Name
                                : currentRelative + PathSeparator + entry.Name;
                            Walk(next);
                        }

                        continue;
                    }

                    var rel = string.IsNullOrEmpty(currentRelative)
                        ? entry.Name
                        : currentRelative + PathSeparator + entry.Name;

                    if (!recursive && rel.Contains(PathSeparator))
                    {
                        // в нерекурсивном режиме пропускаем вложенные
                        continue;
                    }

                    files.Add(new BackupFileInfo
                    {
                        Path = rel,
                        Name = entry.Name,
                        Size = entry.Attributes.Size,
                        LastModified = entry.LastWriteTime.ToUniversalTime()
                    });
                }
            }

            Walk(string.Empty);
            return files;
        }

        public async Task<Stream> GetFileStreamAsync(BackupFileInfo file)
        {
            ArgumentNullException.ThrowIfNull(_sftp);
            ArgumentNullException.ThrowIfNull(file);
            ArgumentException.ThrowIfNullOrEmpty(file.Path);
            EnsureConnected();

            var remote = NormalizeRemotePath(GetRemotePath(file.Path));

            var ms = new MemoryStream();
            await _sftp.DownloadFileAsync(remote, ms, CancellationToken.None);
            ms.Position = 0;
            return ms;
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
            _sftp?.Dispose();
        }
    }
}
