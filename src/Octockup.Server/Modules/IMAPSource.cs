using MailKit;
using MailKit.Net.Imap;
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
        private int _batchSize = 100;
        private string? _rootPath;
        private readonly SemaphoreSlim _imapLock = new(1, 1);
        private char? _serverDirectorySeparator;

        public void SetParameters(IReadOnlyDictionary<string, string> parameters)
        {
            _host = parameters["host"];
            _port = int.TryParse(parameters["port"], out var p) ? p : 993;
            _username = parameters["username"];
            _password = parameters["password"];
            _useSsl = parameters.TryGetValue("useSsl", out var sslStr) &&
                      bool.TryParse(sslStr, out var ssl) && ssl;

            if (!parameters.ContainsKey("useSsl"))
            {
                _useSsl = true;
            }

            if (parameters.TryGetValue("batchSize", out var batchStr) &&
                int.TryParse(batchStr, out var bs) &&
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
                    await _client.ConnectAsync(_host, _port, _useSsl, cancellationToken);
                    await _client.AuthenticateAsync(_username, _password, cancellationToken);
                    _logger.LogInformation("Successfully connected to IMAP server {Host}:{Port}", _host, _port);

                    // Cache the directory separator from the server
                    var personalNamespace = _client.GetFolder(_client.PersonalNamespaces[0]);
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
                var root = GetRootFolder(cancellationToken);
                folders = GetAllFoldersRecursive(root, cancellationToken);
            }
            else
            {
                var root = GetRootFolder(cancellationToken);

                IReadOnlyList<IMailFolder> subfolders;
                try
                {
                    _imapLock.Wait(cancellationToken);
                    try
                    {
                        var fetched = root.GetSubfolders(cancellationToken: cancellationToken);
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

            foreach (var folder in folders)
            {
                var folderPath = "/" + folder.FullName;
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
            if (_client == null)
            {
                yield break;
            }

            var visitedFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var root = GetRootFolder(cancellationToken);

            if (!string.IsNullOrWhiteSpace(_rootPath) && _rootPath != "/")
            {
                if (recursive)
                {
                    foreach (var folder in GetAllFoldersRecursive(root, cancellationToken))
                    {
                        if (!visitedFolders.Add(folder.FullName))
                        {
                            continue;
                        }

                        foreach (var file in EnumerateFolderFiles(folder, cancellationToken))
                        {
                            yield return file;
                        }
                    }
                }
                else
                {
                    foreach (var file in EnumerateFolderFiles(root, cancellationToken))
                    {
                        yield return file;
                    }
                }

                yield break;
            }

            string inboxFullName;
            IMailFolder inboxFolder;
            _imapLock.Wait(cancellationToken);
            try
            {
                inboxFolder = _client.Inbox;
                inboxFullName = inboxFolder.FullName;
            }
            finally
            {
                _imapLock.Release();
            }

            if (!visitedFolders.Contains(inboxFullName))
            {
                foreach (var file in EnumerateFolderFiles(inboxFolder, cancellationToken))
                {
                    yield return file;
                }

                visitedFolders.Add(inboxFullName);
            }

            if (!recursive)
            {
                yield break;
            }

            foreach (var folder in GetAllFoldersRecursive(root, cancellationToken))
            {
                if (!visitedFolders.Add(folder.FullName))
                {
                    continue;
                }

                if (string.Equals(folder.FullName, inboxFullName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (var file in EnumerateFolderFiles(folder, cancellationToken))
                {
                    yield return file;
                }
            }
        }

        private IEnumerable<BackupFileInfo> EnumerateFolderFiles(IMailFolder folder, CancellationToken cancellationToken = default)
        {
            var folderPath = "/" + folder.FullName;
            cancellationToken.ThrowIfCancellationRequested();

            if (_ignoredPaths != null &&
                ScheduleHelpers.IsPathIgnored(folderPath, folder.Name, _ignoredPaths))
            {
                _logger.LogDebug("Skipping ignored IMAP folder during file enumeration: {Folder}", folderPath);
                yield break;
            }

            // Skip folders with empty names
            if (string.IsNullOrWhiteSpace(folder.FullName))
            {
                _logger.LogDebug("Skipping folder with empty name");
                yield break;
            }

            IMailFolder? openedFolder = null;

            IEnumerable<BackupFileInfo> ProcessFolder()
            {
                _imapLock.Wait(cancellationToken);
                try
                {
                    // Get a fresh reference to the folder to avoid stale references
                    openedFolder = string.IsNullOrEmpty(folder.FullName)
                        ? _client!.Inbox
                        : _client!.GetFolder(folder.FullName, cancellationToken);

                    if (!openedFolder.IsOpen)
                    {
                        openedFolder.Open(FolderAccess.ReadOnly, cancellationToken);
                    }

                    var total = openedFolder.Count;
                    _logger.LogInformation(
                        "Enumerating {Total} emails in folder {Folder} in batches of {Batch}",
                        total,
                        openedFolder.FullName,
                        _batchSize);

                    for (var start = 0; start < total; start += _batchSize)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var end = Math.Min(start + _batchSize - 1, total - 1);

                        IReadOnlyList<IMessageSummary> summaries;
                        try
                        {
                            var fetched = openedFolder.Fetch(start, end, MessageSummaryItems.UniqueId |
                                MessageSummaryItems.InternalDate |
                                MessageSummaryItems.Size, cancellationToken: cancellationToken);

                            summaries = fetched is IReadOnlyList<IMessageSummary> ro
                                ? ro
                                : [.. fetched];
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(
                                ex,
                                "Failed to fetch summaries for {Folder} range {Start}-{End}",
                                openedFolder.FullName,
                                start,
                                end);
                            break;
                        }

                        foreach (var summary in summaries)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            var uid = summary.UniqueId;
                            if (uid.IsValid == false)
                            {
                                continue;
                            }

                            var fileName = $"{uid.Id}.eml";

                            // Normalize folder path to always use '/' separator, regardless of server's separator
                            var normalizedFolderPath = string.IsNullOrEmpty(openedFolder.FullName)
                                ? string.Empty
                                : (_serverDirectorySeparator.HasValue && _serverDirectorySeparator.Value != '/')
                                    ? openedFolder.FullName.Replace(_serverDirectorySeparator.Value, '/')
                                    : openedFolder.FullName;

                            var filePath = string.IsNullOrEmpty(normalizedFolderPath)
                                ? fileName
                                : $"{normalizedFolderPath}/{fileName}";

                            if (_ignoredPaths != null &&
                                ScheduleHelpers.IsPathIgnored("/" + filePath, fileName, _ignoredPaths))
                            {
                                _logger.LogDebug("Skipping ignored email: {File}", filePath);
                                continue;
                            }

                            yield return new BackupFileInfo
                            {
                                Path = filePath,
                                Name = fileName,
                                Size = summary.Size,
                                LastModified = summary.InternalDate?.UtcDateTime
                            };
                        }
                    }
                }
                finally
                {
                    try
                    {
                        if (openedFolder != null && openedFolder.IsOpen)
                        {
                            openedFolder.Close(cancellationToken: cancellationToken);
                        }
                    }
                    catch
                    {
                        // ignore
                    }
                    finally
                    {
                        _imapLock.Release();
                    }
                }
            }

            IEnumerable<BackupFileInfo>? results = null;
            Exception? exception = null;

            try
            {
                results = ProcessFolder();
            }
            catch (ImapCommandException ex) when (ex.Message.Contains("Unknown Mailbox"))
            {
                _logger.LogWarning("Folder {Folder} does not exist or is not accessible, skipping", folder.FullName);
                yield break;
            }
            catch (Exception ex)
            {
                exception = ex;
            }

            if (exception != null)
            {
                _logger.LogError(exception, "Error enumerating folder {Folder}", folder.FullName);
                yield break;
            }

            if (results != null)
            {
                foreach (var result in results)
                {
                    yield return result;
                }
            }
        }

        private IEnumerable<IMailFolder> GetAllFoldersRecursive(IMailFolder rootFolder, CancellationToken cancellationToken = default)
        {
            var queue = new Queue<IMailFolder>();
            queue.Enqueue(rootFolder);

            while (queue.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var folder = queue.Dequeue();
                yield return folder;

                IReadOnlyList<IMailFolder> subfolders;
                try
                {
                    _imapLock.Wait(cancellationToken);
                    try
                    {
                        var fetched = folder.GetSubfolders(cancellationToken: cancellationToken);
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

                foreach (var subfolder in subfolders)
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
                var parts = file.Path.Split('/');
                var fileName = parts[^1];
                var folderPath = parts.Length > 1
                    ? string.Join("/", parts[..^1])
                    : string.Empty;

                var uidStr = fileName.Replace(".eml", "");
                if (!uint.TryParse(uidStr, out var uidValue))
                {
                    _logger.LogError("Invalid email UID in filename: {FileName}", fileName);
                    return Stream.Null;
                }

                var uid = new UniqueId(uidValue);

                await _imapLock.WaitAsync(cancellationToken);
                try
                {
                    IMailFolder folder;
                    if (string.IsNullOrEmpty(folderPath))
                    {
                        folder = _client.Inbox;
                    }
                    else
                    {
                        // Convert normalized '/' path back to server's directory separator
                        var serverFolderPath = (_serverDirectorySeparator.HasValue && _serverDirectorySeparator.Value != '/')
                            ? folderPath.Replace('/', _serverDirectorySeparator.Value)
                            : folderPath;
                        folder = await _client.GetFolderAsync(serverFolderPath, cancellationToken);
                    }

                    await folder.OpenAsync(FolderAccess.ReadOnly, cancellationToken);
                    var message = await folder.GetMessageAsync(uid);
                    await folder.CloseAsync(cancellationToken: cancellationToken);

                    var ms = new MemoryStream();
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
                var serverRoot = (_serverDirectorySeparator.HasValue && _serverDirectorySeparator.Value != '/')
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
                var parts = path.Split('/');
                var fileName = parts[^1];
                var folderPath = parts.Length > 1
                    ? string.Join("/", parts[..^1])
                    : string.Empty;

                var uidStr = fileName.Replace(".eml", "");
                if (!uint.TryParse(uidStr, out var uidValue))
                {
                    _logger.LogError("Invalid email UID in filename: {FileName}", fileName);
                    return null;
                }

                var uid = new UniqueId(uidValue);

                await _imapLock.WaitAsync(cancellationToken);
                try
                {
                    IMailFolder folder;
                    if (string.IsNullOrEmpty(folderPath))
                    {
                        folder = _client.Inbox;
                    }
                    else
                    {
                        var serverFolderPath = (_serverDirectorySeparator.HasValue && _serverDirectorySeparator.Value != '/')
                            ? folderPath.Replace('/', _serverDirectorySeparator.Value)
                            : folderPath;
                        folder = await _client.GetFolderAsync(serverFolderPath, cancellationToken);
                    }

                    await folder.OpenAsync(FolderAccess.ReadOnly, cancellationToken);

                    var summaries = await folder.FetchAsync([uid], MessageSummaryItems.UniqueId |
                        MessageSummaryItems.InternalDate |
                        MessageSummaryItems.Size, cancellationToken: cancellationToken);

                    await folder.CloseAsync(cancellationToken: cancellationToken);

                    var summary = summaries.FirstOrDefault();
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
