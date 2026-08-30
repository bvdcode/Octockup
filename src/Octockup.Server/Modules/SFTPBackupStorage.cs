// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Octockup.Server.Abstractions;
using Octockup.Server.Helpers;
using Octockup.Server.Models;
using Renci.SshNet;
using Renci.SshNet.Common;
using Renci.SshNet.Sftp;
using System.Security.Cryptography;
using System.Text;

namespace Octockup.Server.Modules
{
    public partial class SFTPBackupStorage(ILogger<SFTPBackupStorage> _logger) : IBackupStorage, IDisposable
    {
        public char PathSeparator => '/';
        public string Id => GetType().FullName!;
        public string Name => "SFTP (SSH)";

        public IEnumerable<string> RequiredParameters => [
            "host", "port", "username", "password", "path", "skipPermissionDenied"
        ];

        private string? _path;
        private SftpClient? _sftp;
        private PrivateKeyFile? _privateKey;
        private bool _skipPermissionDenied = false;
        private ICollection<string>? _ignoredPaths;

        public void SetParameters(IReadOnlyDictionary<string, string> parameters)
        {
            string host = parameters["host"];
            int port = int.TryParse(parameters["port"], out int p) ? p : 22;
            string username = parameters["username"];
            string credential = parameters["password"];

            _path = parameters["path"].Trim().Trim('/');
            _skipPermissionDenied = parameters.TryGetValue("skipPermissionDenied", out string? skipStr) &&
                                    bool.TryParse(skipStr, out bool skip) && skip;
            SftpClient sftp = CreateSftpClient(
                host,
                port,
                username,
                credential,
                out PrivateKeyFile? privateKey
            );

            DisposeConnection();
            _sftp = sftp;
            _privateKey = privateKey;
        }

        internal static SftpClient CreateSftpClient(
            string host,
            int port,
            string username,
            string credential,
            out PrivateKeyFile? ownedPrivateKey
        )
        {
            ownedPrivateKey = null;
            SftpClient client;

            if (LooksLikePrivateKey(credential))
            {
                byte[] privateKeyBytes = Encoding.UTF8.GetBytes(credential);

                try
                {
                    using MemoryStream privateKeyStream = new(privateKeyBytes, writable: false);
                    ownedPrivateKey = new PrivateKeyFile(privateKeyStream);
                    client = new SftpClient(host, port, username, ownedPrivateKey);
                }
                catch
                {
                    ownedPrivateKey?.Dispose();
                    ownedPrivateKey = null;
                    throw;
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(privateKeyBytes);
                }
            }
            else
            {
                client = new SftpClient(host, port, username, credential);
            }

            client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(30);
            return client;
        }

        private static bool LooksLikePrivateKey(string credential)
        {
            ReadOnlySpan<char> trimmedCredential = credential.AsSpan().TrimStart();
            int firstLineEnd = trimmedCredential.IndexOfAny('\r', '\n');
            ReadOnlySpan<char> firstLine = firstLineEnd >= 0
                ? trimmedCredential[..firstLineEnd]
                : trimmedCredential;

            return (
                    firstLine.StartsWith("-----BEGIN ", StringComparison.Ordinal) &&
                    firstLine.EndsWith(" PRIVATE KEY-----", StringComparison.Ordinal)
                ) ||
                firstLine.StartsWith("PuTTY-User-Key-File-", StringComparison.Ordinal);
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
            string? basePath = _path?.Trim('/');

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

            string remote = NormalizeRemotePath(GetRemotePath(path));

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

            string remote = NormalizeRemotePath(GetRemotePath(path));

            try
            {
                bool exists = await _sftp.ExistsAsync(remote, cancellationToken);
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

            HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
            foreach (string directory in EnumerateDirectories(
                string.Empty,
                recursive,
                seen,
                cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return directory;
            }
        }

        public IEnumerable<BackupFileInfo> GetFiles(bool recursive = false, CancellationToken cancellationToken = default)
        {
            EnsureConnected();
            ArgumentNullException.ThrowIfNull(_sftp);

            Queue<string> queue = new();
            queue.Enqueue(string.Empty);

            HashSet<string> seenDirectories = new(StringComparer.OrdinalIgnoreCase);

            while (queue.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string currentRelative = queue.Dequeue();
                string full = NormalizeRemotePath(GetRemotePath(currentRelative));
                if (IsIgnoredDirectory(full))
                {
                    continue;
                }

                IEnumerable<ISftpFile>? entries = GetDirectoryEntries(full);
                if (entries is null)
                {
                    continue;
                }

                foreach (ISftpFile entry in entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    BackupFileInfo? file = ProcessEntry(
                        entry,
                        currentRelative,
                        full,
                        recursive,
                        seenDirectories,
                        queue);
                    if (file is not null)
                    {
                        yield return file;
                    }
                }
            }
        }

        public async Task<Stream> GetFileStreamAsync(BackupFileInfo file, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(_sftp);
            ArgumentNullException.ThrowIfNull(file);
            ArgumentException.ThrowIfNullOrEmpty(file.Path);
            EnsureConnected();
            string remote = NormalizeRemotePath(GetRemotePath(file.Path));

            try
            {
                // Return a streaming reader to avoid buffering whole file in memory
                SftpFileStream stream = _sftp.OpenRead(remote);
                return stream ?? Stream.Null;
            }
            catch (SftpPathNotFoundException ex)
            {
                // File was deleted/moved between enumeration and download - this is normal for temp files
                _logger.LogWarning(ex, "File no longer exists on SFTP server: {Path}", remote);
                return Stream.Null;
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

            string remote = NormalizeRemotePath(GetRemotePath(path));
            string dir = Path.GetDirectoryName(remote)?.Replace("\\", "/") ?? "/";

            EnsureDirectory(dir);

            return _sftp.UploadFileAsync(data, remote, cancellationToken);
        }

        private void EnsureDirectory(string remoteDir)
        {
            ArgumentNullException.ThrowIfNull(_sftp);

            string[] parts = remoteDir.Trim('/').Split(
                PathSeparator,
                StringSplitOptions.RemoveEmptyEntries
            );

            string current = "/";

            foreach (string part in parts)
            {
                current = (current.EndsWith('/') ? current : current + "/") + part;

                if (!_sftp.Exists(current))
                {
                    _sftp.CreateDirectory(current);
                }
            }
        }

        public async Task<BackupFileInfo?> GetFileInfoAsync(string path, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            EnsureConnected();
            ArgumentNullException.ThrowIfNull(_sftp);

            string remote = NormalizeRemotePath(GetRemotePath(path));

            try
            {
                if (!await _sftp.ExistsAsync(remote, cancellationToken))
                {
                    return null;
                }

                SftpFileAttributes attrs = await _sftp.GetAttributesAsync(remote, cancellationToken);

                if (attrs == null || attrs.IsDirectory)
                {
                    return null;
                }

                string fileName = Path.GetFileName(path);

                return new BackupFileInfo
                {
                    Path = path,
                    Name = fileName,
                    Size = attrs.Size,
                    LastModified = attrs.LastWriteTime.ToUniversalTime()
                };
            }
            catch (SftpPermissionDeniedException ex) when (_skipPermissionDenied)
            {
                _logger.LogWarning(ex, "Permission denied when accessing file info for SFTP path: {Path}", remote);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get file info for '{Path}' from SFTP", path);
                return null;
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            DisposeConnection();
        }

        private void DisposeConnection()
        {
            SftpClient? sftp = _sftp;
            PrivateKeyFile? privateKey = _privateKey;
            _sftp = null;
            _privateKey = null;

            try
            {
                sftp?.Dispose();
            }
            finally
            {
                privateKey?.Dispose();
            }
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
