using MailKit;
using MailKit.Net.Imap;
using Octockup.Server.Models;
using Octockup.Server.Helpers;
using Octockup.Server.Abstractions;

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
        private int _batchSize = 10;
        private string? _rootPath;
        private readonly SemaphoreSlim _imapLock = new(1, 1);

        public void SetParameters(Dictionary<string, string> parameters)
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

        private async Task EnsureConnectedAsync()
        {
            // serialize connect/authenticate operations to avoid races
            await _imapLock.WaitAsync();
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
                    await _client.ConnectAsync(_host, _port, _useSsl);
                    await _client.AuthenticateAsync(_username, _password);
                    _logger.LogInformation("Successfully connected to IMAP server {Host}:{Port}", _host, _port);
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

        public IEnumerable<string> GetDirectories(bool recursive = false)
        {
            EnsureConnectedAsync().GetAwaiter().GetResult();
            if (_client == null)
            {
                yield break;
            }

            IEnumerable<IMailFolder> folders;

            if (recursive)
            {
                var root = GetRootFolder();
                folders = GetAllFoldersRecursive(root);
            }
            else
            {
                var root = GetRootFolder();

                IReadOnlyList<IMailFolder> subfolders;
                try
                {
                    _imapLock.Wait();
                    try
                    {
                        var fetched = root.GetSubfolders();
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

        public IEnumerable<BackupFileInfo> GetFiles(bool recursive = false)
        {
            EnsureConnectedAsync().GetAwaiter().GetResult();
            if (_client == null)
            {
                yield break;
            }

            var visitedFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var root = GetRootFolder();

            if (!string.IsNullOrWhiteSpace(_rootPath) && _rootPath != "/")
            {
                if (recursive)
                {
                    foreach (var folder in GetAllFoldersRecursive(root))
                    {
                        if (!visitedFolders.Add(folder.FullName))
                        {
                            continue;
                        }

                        foreach (var file in EnumerateFolderFiles(folder))
                        {
                            yield return file;
                        }
                    }
                }
                else
                {
                    foreach (var file in EnumerateFolderFiles(root))
                    {
                        yield return file;
                    }
                }

                yield break;
            }

            if (!visitedFolders.Contains(_client.Inbox.FullName))
            {
                foreach (var file in EnumerateFolderFiles(_client.Inbox))
                {
                    yield return file;
                }

                visitedFolders.Add(_client.Inbox.FullName);
            }

            if (!recursive)
            {
                yield break;
            }

            foreach (var folder in GetAllFoldersRecursive(root))
            {
                if (!visitedFolders.Add(folder.FullName))
                {
                    continue;
                }

                if (string.Equals(folder.FullName, _client.Inbox.FullName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                foreach (var file in EnumerateFolderFiles(folder))
                {
                    yield return file;
                }
            }
        }

        private IEnumerable<BackupFileInfo> EnumerateFolderFiles(IMailFolder folder)
        {
            var folderPath = "/" + folder.FullName;

            if (_ignoredPaths != null &&
                ScheduleHelpers.IsPathIgnored(folderPath, folder.Name, _ignoredPaths))
            {
                _logger.LogDebug("Skipping ignored IMAP folder during file enumeration: {Folder}", folderPath);
                yield break;
            }

            IMailFolder? openedFolder = null;

            try
            {
                openedFolder = folder;

                _imapLock.Wait();
                try
                {
                    if (!openedFolder.IsOpen)
                    {
                        openedFolder.Open(FolderAccess.ReadOnly);
                    }

                    var total = openedFolder.Count;
                    _logger.LogInformation(
                        "Enumerating {Total} emails in folder {Folder} in batches of {Batch}",
                        total,
                        openedFolder.FullName,
                        _batchSize);

                    for (var start = 0; start < total; start += _batchSize)
                    {
                        var end = Math.Min(start + _batchSize - 1, total - 1);

                        IReadOnlyList<IMessageSummary> summaries;
                        try
                        {
                            var fetched = openedFolder.Fetch(
                                start,
                                end,
                                MessageSummaryItems.UniqueId |
                                MessageSummaryItems.InternalDate |
                                MessageSummaryItems.Size);

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
                            yield break;
                        }

                        foreach (var summary in summaries)
                        {
                            var uid = summary.UniqueId;
                            if (uid.IsValid == false)
                            {
                                continue;
                            }

                            var fileName = $"{uid.Id}.eml";
                            var filePath = string.IsNullOrEmpty(openedFolder.FullName)
                                ? fileName
                                : $"{openedFolder.FullName}/{fileName}";

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
                            openedFolder.Close();
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
            finally
            {
                // no-op
            }
        }

        private IEnumerable<IMailFolder> GetAllFoldersRecursive(IMailFolder rootFolder)
        {
            var queue = new Queue<IMailFolder>();
            queue.Enqueue(rootFolder);

            while (queue.Count > 0)
            {
                var folder = queue.Dequeue();
                yield return folder;

                IReadOnlyList<IMailFolder> subfolders;
                try
                {
                    _imapLock.Wait();
                    try
                    {
                        var fetched = folder.GetSubfolders();
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

        public async Task<Stream> GetFileStreamAsync(BackupFileInfo file)
        {
            await EnsureConnectedAsync();
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

                await _imapLock.WaitAsync();
                try
                {
                    var folder = string.IsNullOrEmpty(folderPath)
                        ? _client.Inbox
                        : _client.GetFolder(folderPath);

                    await folder.OpenAsync(FolderAccess.ReadOnly);
                    var message = await folder.GetMessageAsync(uid);
                    await folder.CloseAsync();

                    var ms = new MemoryStream();
                    await message.WriteToAsync(ms);
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

        private IMailFolder GetRootFolder()
        {
            if (string.IsNullOrWhiteSpace(_rootPath) || _rootPath == "/")
            {
                return _client!.GetFolder(_client.PersonalNamespaces[0]);
            }

            return _client!.GetFolder(_rootPath.TrimStart('/'));
        }
    }
}
