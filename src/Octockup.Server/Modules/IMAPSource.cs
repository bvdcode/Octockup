// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using MailKit;
using MailKit.Net.Imap;
using MimeKit;
using Octockup.Server.Abstractions;
using Octockup.Server.Helpers;
using Octockup.Server.Models;

namespace Octockup.Server.Modules
{
    public class IMAPSource(ILogger<IMAPSource> _logger) : IBackupSource, IDisposable
    {
        public string Id => typeof(IMAPSource).FullName!;
        public string Name => "IMAP Email";
        public char PathSeparator => '/';

        public IEnumerable<string> RequiredParameters => ["host", "port", "username", "password", "path", "useSsl"];

        private string? _host;
        private int _port;
        private string? _username;
        private string? _password;
        private bool _useSsl;
        private ICollection<string>? _ignoredPaths;
        private ImapClient? _client;
        private int _batchSize = 1000;
        private string? _rootPath;
        private readonly SemaphoreSlim _imapLock = new(1, 1);
        private char? _serverDirectorySeparator;

        public void SetParameters(IReadOnlyDictionary<string, string> parameters)
        {
            _host = parameters["host"];
            _port = int.TryParse(parameters["port"], out int p) ? p : 993;
            _username = parameters["username"];
            _password = parameters["password"];
            _useSsl = parameters.TryGetValue("useSsl", out string? sslStr) &&
                      bool.TryParse(sslStr, out bool ssl) && ssl;

            if (!parameters.ContainsKey("useSsl"))
            {
                _useSsl = true;
            }

            if (parameters.TryGetValue("batchSize", out string? batchStr) &&
                int.TryParse(batchStr, out int bs) &&
                bs > 0)
            {
                _batchSize = bs;
            }

            parameters.TryGetValue("path", out _rootPath);
        }

        public void SetIgnoredPaths(ICollection<string>? ignoredPaths)
        {
            _ignoredPaths = ignoredPaths;
        }

        private async Task EnsureConnectedAsync(CancellationToken cancellationToken = default)
        {
            await _imapLock.WaitAsync(cancellationToken);
            try
            {
                if (_client != null && _client.IsConnected && _client.IsAuthenticated)
                {
                    return;
                }

                _client?.Dispose();
                _client = new ImapClient
                {
                    Timeout = 60_000
                };

                try
                {
                    string host = _host ?? throw new InvalidOperationException("IMAP host is not configured.");
                    string username = _username ?? throw new InvalidOperationException("IMAP username is not configured.");
                    string password = _password ?? throw new InvalidOperationException("IMAP password is not configured.");

                    await _client.ConnectAsync(host, _port, _useSsl, cancellationToken);
                    await _client.AuthenticateAsync(username, password, cancellationToken);
                    _logger.LogInformation("Successfully connected to IMAP server {Host}:{Port}", host, _port);

                    // Cache the directory separator from the server
                    IMailFolder personalNamespace = _client.GetFolder(_client.PersonalNamespaces[0]);
                    _serverDirectorySeparator = personalNamespace.DirectorySeparator;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to connect to IMAP server {Host}:{Port}", _host, _port);
                    _client?.Dispose();
                    _client = null;
                    throw;
                }
            }
            finally
            {
                _imapLock.Release();
            }
        }

        public IEnumerable<string> GetDirectories(bool recursive = false, CancellationToken cancellationToken = default)
        {
            EnsureConnectedAsync(cancellationToken).GetAwaiter().GetResult();
            if (_client == null)
            {
                yield break;
            }

            IEnumerable<IMailFolder> folders;

            if (recursive)
            {
                IMailFolder root = GetRootFolder(cancellationToken);
                folders = GetAllFoldersRecursive(root, cancellationToken);
            }
            else
            {
                IMailFolder root = GetRootFolder(cancellationToken);

                IReadOnlyList<IMailFolder> subfolders;
                try
                {
                    _imapLock.Wait(cancellationToken);
                    try
                    {
                        IList<IMailFolder> fetched = root.GetSubfolders(cancellationToken: cancellationToken);
                        subfolders = fetched is IReadOnlyList<IMailFolder> ro
                            ? ro
                            : [.. fetched];
                    }
                    finally
                    {
                        _imapLock.Release();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to list IMAP root subfolders");
                    yield break;
                }

                folders = subfolders;
            }

            foreach (IMailFolder folder in folders)
            {
                string folderPath = "/" + folder.FullName;
                if (_ignoredPaths != null &&
                    ScheduleHelpers.IsPathIgnored(folderPath, folder.Name, _ignoredPaths))
                {
                    _logger.LogDebug("Skipping ignored IMAP folder: {Folder}", folderPath);
                    continue;
                }

                yield return folder.FullName;
            }
        }

        public IEnumerable<BackupFileInfo> GetFiles(bool recursive = false, CancellationToken cancellationToken = default)
        {
            EnsureConnectedAsync(cancellationToken).GetAwaiter().GetResult();
            if (_client is null)
            {
                yield break;
            }

            HashSet<string> visitedFolders = new(StringComparer.OrdinalIgnoreCase);
            IMailFolder root = GetRootFolder(cancellationToken);

            if (!string.IsNullOrWhiteSpace(_rootPath) && _rootPath != "/")
            {
                IEnumerable<IMailFolder> folders = recursive
                    ? GetAllFoldersRecursive(root, cancellationToken)
                    : [root];
                foreach (BackupFileInfo file in EnumerateUniqueFolderFiles(
                    folders,
                    visitedFolders,
                    excludedFolderName: null,
                    cancellationToken))
                {
                    yield return file;
                }
                yield break;
            }

            IMailFolder inboxFolder = GetInboxFolder(cancellationToken);
            string inboxFullName = inboxFolder.FullName;
            visitedFolders.Add(inboxFullName);
            foreach (BackupFileInfo file in EnumerateFolderFiles(inboxFolder, cancellationToken))
            {
                yield return file;
            }

            if (!recursive)
            {
                yield break;
            }

            foreach (BackupFileInfo file in EnumerateUniqueFolderFiles(
                GetAllFoldersRecursive(root, cancellationToken),
                visitedFolders,
                inboxFullName,
                cancellationToken))
            {
                yield return file;
            }
        }

        private IMailFolder GetInboxFolder(CancellationToken cancellationToken)
        {
            _imapLock.Wait(cancellationToken);
            try
            {
                ImapClient client = _client
                    ?? throw new InvalidOperationException("IMAP client is not connected.");
                return client.Inbox
                    ?? throw new InvalidOperationException("IMAP server did not provide an Inbox folder.");
            }
            finally
            {
                _imapLock.Release();
            }
        }

        private IEnumerable<BackupFileInfo> EnumerateUniqueFolderFiles(
            IEnumerable<IMailFolder> folders,
            ISet<string> visitedFolders,
            string? excludedFolderName,
            CancellationToken cancellationToken)
        {
            foreach (IMailFolder folder in folders)
            {
                if (!visitedFolders.Add(folder.FullName)
                    || string.Equals(
                        folder.FullName,
                        excludedFolderName,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (BackupFileInfo file in EnumerateFolderFiles(folder, cancellationToken))
                {
                    yield return file;
                }
            }
        }

        private IEnumerable<BackupFileInfo> EnumerateFolderFiles(IMailFolder folder, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ShouldSkipFolder(folder))
            {
                yield break;
            }

            using IEnumerator<BackupFileInfo> enumerator = ProcessFolderFiles(
                folder,
                cancellationToken).GetEnumerator();
            while (TryMoveNext(enumerator, folder))
            {
                yield return enumerator.Current;
            }
        }

        private bool ShouldSkipFolder(IMailFolder folder)
        {
            string folderPath = "/" + folder.FullName;
            if (_ignoredPaths is not null
                && ScheduleHelpers.IsPathIgnored(folderPath, folder.Name, _ignoredPaths))
            {
                _logger.LogDebug(
                    "Skipping ignored IMAP folder during file enumeration: {Folder}",
                    folderPath);
                return true;
            }

            if (string.IsNullOrWhiteSpace(folder.FullName))
            {
                _logger.LogDebug("Skipping folder with empty name");
                return true;
            }

            if ((folder.Attributes & FolderAttributes.NoSelect) == 0)
            {
                return false;
            }

            _logger.LogDebug(
                "Skipping NoSelect folder (namespace container): {Folder}",
                folder.FullName);
            return true;
        }

        private IEnumerable<BackupFileInfo> ProcessFolderFiles(
            IMailFolder folder,
            CancellationToken cancellationToken)
        {
            IMailFolder? openedFolder = null;
            _imapLock.Wait(cancellationToken);
            try
            {
                openedFolder = OpenFolder(folder, cancellationToken);
                if (openedFolder is null)
                {
                    yield break;
                }

                int total = openedFolder.Count;
                _logger.LogInformation(
                    "Enumerating {Total} emails in folder {Folder} in batches of {Batch}",
                    total,
                    openedFolder.FullName,
                    _batchSize);

                for (int start = 0; start < total; start += _batchSize)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int end = Math.Min(start + _batchSize - 1, total - 1);
                    IReadOnlyList<IMessageSummary>? summaries = FetchSummaries(
                        openedFolder,
                        start,
                        end,
                        cancellationToken);
                    if (summaries is null)
                    {
                        yield break;
                    }

                    foreach (IMessageSummary summary in summaries)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        BackupFileInfo? file = CreateFileInfo(openedFolder, summary);
                        if (file is not null)
                        {
                            yield return file;
                        }
                    }
                }
            }
            finally
            {
                CloseFolder(openedFolder, cancellationToken);
                _imapLock.Release();
            }
        }

        private IMailFolder? OpenFolder(IMailFolder folder, CancellationToken cancellationToken)
        {
            ImapClient client = _client
                ?? throw new InvalidOperationException("IMAP client is not connected.");
            IMailFolder? openedFolder = string.IsNullOrEmpty(folder.FullName)
                ? client.Inbox
                : client.GetFolder(folder.FullName, cancellationToken);

            if (openedFolder is null)
            {
                _logger.LogDebug("Skipping unavailable IMAP folder: {Folder}", folder.FullName);
                return null;
            }

            if ((openedFolder.Attributes & FolderAttributes.NoSelect) != 0)
            {
                _logger.LogDebug(
                    "Skipping NoSelect folder after fresh reference (namespace container): {Folder}",
                    openedFolder.FullName);
                return null;
            }

            if (!openedFolder.IsOpen)
            {
                openedFolder.Open(FolderAccess.ReadOnly, cancellationToken);
            }

            return openedFolder;
        }

        private IReadOnlyList<IMessageSummary>? FetchSummaries(
            IMailFolder folder,
            int start,
            int end,
            CancellationToken cancellationToken)
        {
            try
            {
                IList<IMessageSummary> fetched = folder.Fetch(
                    start,
                    end,
                    MessageSummaryItems.UniqueId
                        | MessageSummaryItems.InternalDate
                        | MessageSummaryItems.Size,
                    cancellationToken: cancellationToken);
                return fetched is IReadOnlyList<IMessageSummary> summaries
                    ? summaries
                    : [.. fetched];
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Failed to fetch summaries for {Folder} range {Start}-{End}",
                    folder.FullName,
                    start,
                    end);
                return null;
            }
        }

        private BackupFileInfo? CreateFileInfo(IMailFolder folder, IMessageSummary summary)
        {
            UniqueId uniqueId = summary.UniqueId;
            if (!uniqueId.IsValid)
            {
                return null;
            }

            string fileName = $"{uniqueId.Id}.eml";
            string normalizedFolderPath = NormalizeFolderPath(folder.FullName);
            string filePath = string.IsNullOrEmpty(normalizedFolderPath)
                ? fileName
                : $"{normalizedFolderPath}/{fileName}";
            if (_ignoredPaths is not null
                && ScheduleHelpers.IsPathIgnored("/" + filePath, fileName, _ignoredPaths))
            {
                _logger.LogDebug("Skipping ignored email: {File}", filePath);
                return null;
            }

            return new BackupFileInfo
            {
                Path = filePath,
                Name = fileName,
                Size = summary.Size,
                LastModified = summary.InternalDate?.UtcDateTime,
            };
        }

        private string NormalizeFolderPath(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath)
                || !_serverDirectorySeparator.HasValue
                || _serverDirectorySeparator.Value == '/')
            {
                return folderPath;
            }

            return folderPath.Replace(_serverDirectorySeparator.Value, '/');
        }

        private void CloseFolder(IMailFolder? folder, CancellationToken cancellationToken)
        {
            if (folder is null || !folder.IsOpen)
            {
                return;
            }

            try
            {
                folder.Close(cancellationToken: cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogDebug(exception, "Failed to close IMAP folder {Folder}", folder.FullName);
            }
        }

        private bool TryMoveNext(IEnumerator<BackupFileInfo> enumerator, IMailFolder folder)
        {
            try
            {
                return enumerator.MoveNext();
            }
            catch (ImapCommandException exception) when (exception.Message.Contains("Unknown Mailbox"))
            {
                _logger.LogWarning(
                    "Folder {Folder} does not exist or is not accessible, skipping",
                    folder.FullName);
                return false;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Error enumerating folder {Folder}", folder.FullName);
                return false;
            }
        }

        private IEnumerable<IMailFolder> GetAllFoldersRecursive(IMailFolder rootFolder, CancellationToken cancellationToken = default)
        {
            Queue<IMailFolder> queue = new Queue<IMailFolder>();
            queue.Enqueue(rootFolder);

            while (queue.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                IMailFolder folder = queue.Dequeue();
                yield return folder;

                IReadOnlyList<IMailFolder> subfolders;
                try
                {
                    _imapLock.Wait(cancellationToken);
                    try
                    {
                        IList<IMailFolder> fetched = folder.GetSubfolders(cancellationToken: cancellationToken);
                        subfolders = fetched is IReadOnlyList<IMailFolder> ro
                            ? ro
                            : [.. fetched];
                    }
                    finally
                    {
                        _imapLock.Release();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to get subfolders for {Folder}", folder.FullName);
                    continue;
                }

                foreach (IMailFolder subfolder in subfolders)
                {
                    queue.Enqueue(subfolder);
                }
            }
        }

        public async Task<Stream> GetFileStreamAsync(BackupFileInfo file, CancellationToken cancellationToken = default)
        {
            await EnsureConnectedAsync(cancellationToken);
            if (_client == null)
            {
                return Stream.Null;
            }

            try
            {
                string[] parts = file.Path.Split('/');
                string fileName = parts[^1];
                string folderPath = parts.Length > 1
                    ? string.Join("/", parts[..^1])
                    : string.Empty;

                string uidStr = fileName.Replace(".eml", "");
                if (!uint.TryParse(uidStr, out uint uidValue))
                {
                    _logger.LogError("Invalid email UID in filename: {FileName}", fileName);
                    return Stream.Null;
                }

                UniqueId uid = new UniqueId(uidValue);

                await _imapLock.WaitAsync(cancellationToken);
                try
                {
                    ImapClient client = _client;
                    if (client == null)
                    {
                        return Stream.Null;
                    }

                    IMailFolder? folder;
                    if (string.IsNullOrEmpty(folderPath))
                    {
                        folder = client.Inbox;
                    }
                    else
                    {
                        // Convert normalized '/' path back to server's directory separator
                        string serverFolderPath = (_serverDirectorySeparator.HasValue && _serverDirectorySeparator.Value != '/')
                            ? folderPath.Replace('/', _serverDirectorySeparator.Value)
                            : folderPath;
                        folder = await client.GetFolderAsync(serverFolderPath, cancellationToken);
                    }

                    if (folder == null)
                    {
                        _logger.LogWarning("IMAP folder not found for email path: {Path}", file.Path);
                        return Stream.Null;
                    }

                    await folder.OpenAsync(FolderAccess.ReadOnly, cancellationToken);
                    MimeMessage message = await folder.GetMessageAsync(uid, cancellationToken);
                    await folder.CloseAsync(cancellationToken: cancellationToken);

                    // Pre-allocate MemoryStream with known size to avoid resizing
                    int initialCapacity = file.Size.HasValue && file.Size.Value > 0 && file.Size.Value < int.MaxValue
                        ? (int)file.Size.Value
                        : 64 * 1024; // 64KB default

                    MemoryStream ms = new MemoryStream(initialCapacity);
                    await message.WriteToAsync(ms, cancellationToken);
                    ms.Position = 0;

                    return ms;
                }
                finally
                {
                    _imapLock.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to download email: {Path}", file.Path);
                return Stream.Null;
            }
        }

        public void Dispose()
        {
            _client?.Disconnect(true);
            _client?.Dispose();
            GC.SuppressFinalize(this);
        }

        private IMailFolder GetRootFolder(CancellationToken cancellationToken = default)
        {
            _imapLock.Wait(cancellationToken);
            try
            {
                if (string.IsNullOrWhiteSpace(_rootPath) || _rootPath == "/")
                {
                    return _client!.GetFolder(_client.PersonalNamespaces[0]);
                }

                // User-configured rootPath uses '/', convert to server separator if needed
                string serverRoot = (_serverDirectorySeparator.HasValue && _serverDirectorySeparator.Value != '/')
                    ? _rootPath.TrimStart('/').Replace('/', _serverDirectorySeparator.Value)
                    : _rootPath.TrimStart('/');
                return _client!.GetFolder(serverRoot, cancellationToken);
            }
            finally
            {
                _imapLock.Release();
            }
        }

        public async Task<BackupFileInfo?> GetFileInfoAsync(string path, CancellationToken cancellationToken)
        {
            await EnsureConnectedAsync(cancellationToken);
            if (_client == null)
            {
                return null;
            }

            try
            {
                string[] parts = path.Split('/');
                string fileName = parts[^1];
                string folderPath = parts.Length > 1
                    ? string.Join("/", parts[..^1])
                    : string.Empty;

                string uidStr = fileName.Replace(".eml", "");
                if (!uint.TryParse(uidStr, out uint uidValue))
                {
                    _logger.LogError("Invalid email UID in filename: {FileName}", fileName);
                    return null;
                }

                UniqueId uid = new UniqueId(uidValue);

                await _imapLock.WaitAsync(cancellationToken);
                try
                {
                    ImapClient client = _client;
                    if (client == null)
                    {
                        return null;
                    }

                    IMailFolder? folder;
                    if (string.IsNullOrEmpty(folderPath))
                    {
                        folder = client.Inbox;
                    }
                    else
                    {
                        string serverFolderPath = (_serverDirectorySeparator.HasValue && _serverDirectorySeparator.Value != '/')
                            ? folderPath.Replace('/', _serverDirectorySeparator.Value)
                            : folderPath;
                        folder = await client.GetFolderAsync(serverFolderPath, cancellationToken);
                    }

                    if (folder == null)
                    {
                        _logger.LogWarning("IMAP folder not found for email path: {Path}", path);
                        return null;
                    }

                    await folder.OpenAsync(FolderAccess.ReadOnly, cancellationToken);

                    IList<IMessageSummary> summaries = await folder.FetchAsync([uid], MessageSummaryItems.UniqueId |
                        MessageSummaryItems.InternalDate |
                        MessageSummaryItems.Size, cancellationToken: cancellationToken);

                    await folder.CloseAsync(cancellationToken: cancellationToken);

                    IMessageSummary? summary = summaries.FirstOrDefault();
                    if (summary == null || !summary.UniqueId.IsValid)
                    {
                        return null;
                    }

                    return new BackupFileInfo
                    {
                        Path = path,
                        Name = fileName,
                        Size = summary.Size,
                        LastModified = summary.InternalDate?.UtcDateTime
                    };
                }
                finally
                {
                    _imapLock.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get file info for email: {Path}", path);
                return null;
            }
        }
    }
}
