using MailKit;
using MimeKit;
using MailKit.Net.Imap;
using MailKit.Search;
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

        public IEnumerable<string> RequiredParameters => ["host", "port", "username", "password", "useSsl"];

        private string? _host;
        private int _port;
        private string? _username;
        private string? _password;
        private bool _useSsl;
        private ICollection<string>? _ignoredPaths;
        private ImapClient? _client;

        public void SetParameters(Dictionary<string, string> parameters)
        {
            _host = parameters["host"];
            _port = int.TryParse(parameters["port"], out var p) ? p : 993;
            _username = parameters["username"];
            _password = parameters["password"];
            _useSsl = parameters.TryGetValue("useSsl", out var sslStr) &&
                      bool.TryParse(sslStr, out var ssl) && ssl;

            // Default to SSL if not specified
            if (!parameters.ContainsKey("useSsl"))
            {
                _useSsl = true;
            }
        }

        public void SetIgnoredPaths(ICollection<string>? ignoredPaths)
        {
            _ignoredPaths = ignoredPaths;
        }

        private async Task EnsureConnectedAsync()
        {
            if (_client != null && _client.IsConnected && _client.IsAuthenticated)
            {
                return;
            }

            _client?.Dispose();
            _client = new ImapClient();

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

        public IEnumerable<string> GetDirectories(bool recursive = false)
        {
            EnsureConnectedAsync().GetAwaiter().GetResult();
            if (_client == null) yield break;

            var folders = recursive
                ? GetAllFoldersRecursive(_client.PersonalNamespaces[0])
                : _client.Inbox.GetSubfolders();
            
            foreach (var folder in folders)
            {
                // Skip special system folders if ignored
                var folderPath = "/" + folder.FullName;
                
                if (_ignoredPaths != null && ScheduleHelpers.IsPathIgnored(folderPath, folder.Name, _ignoredPaths))
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
            if (_client == null) yield break;

            var folders = recursive 
                ? GetAllFoldersRecursive(_client.PersonalNamespaces[0])
                : new[] { _client.Inbox };

            foreach (var folder in folders)
            {
                var folderPath = "/" + folder.FullName;

                // Check if folder is ignored
                if (_ignoredPaths != null && ScheduleHelpers.IsPathIgnored(folderPath, folder.Name, _ignoredPaths))
                {
                    _logger.LogDebug("Skipping ignored IMAP folder during file enumeration: {Folder}", folderPath);
                    continue;
                }

                IMailFolder? openedFolder = null;
                List<BackupFileInfo> folderFiles = new();

                try
                {
                    openedFolder = _client.GetFolder(folder.FullName);
                    openedFolder.Open(FolderAccess.ReadOnly);

                    var uids = openedFolder.Search(SearchQuery.All);
                    _logger.LogInformation("Found {Count} emails in folder {Folder}", uids.Count, folder.FullName);

                    foreach (var uid in uids)
                    {
                        var message = openedFolder.GetMessage(uid);
                        var fileName = $"{uid.Id}.eml";
                        var filePath = string.IsNullOrEmpty(folder.FullName) 
                            ? fileName 
                            : $"{folder.FullName}/{fileName}";

                        // Check if specific email is ignored
                        if (_ignoredPaths != null && ScheduleHelpers.IsPathIgnored("/" + filePath, fileName, _ignoredPaths))
                        {
                            _logger.LogDebug("Skipping ignored email: {File}", filePath);
                            continue;
                        }

                        folderFiles.Add(new BackupFileInfo
                        {
                            Path = filePath,
                            Name = fileName,
                            Size = EstimateEmailSize(message),
                            LastModified = message.Date.UtcDateTime
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to process IMAP folder: {Folder}", folder.FullName);
                }
                finally
                {
                    openedFolder?.Close();
                }

                foreach (var file in folderFiles)
                {
                    yield return file;
                }
            }
        }

        private IEnumerable<IMailFolder> GetAllFoldersRecursive(FolderNamespace ns)
        {
            var queue = new Queue<IMailFolder>();
            var rootFolder = _client!.GetFolder(ns);
            queue.Enqueue(rootFolder);

            while (queue.Count > 0)
            {
                var folder = queue.Dequeue();
                yield return folder;

                var subfolders = folder.GetSubfolders();
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
                // Parse folder and UID from path: "FolderName/123.eml"
                var parts = file.Path.Split('/');
                var fileName = parts[^1]; // "123.eml"
                var folderPath = parts.Length > 1 
                    ? string.Join("/", parts[..^1]) 
                    : string.Empty;

                // Extract UID from filename
                var uidStr = fileName.Replace(".eml", "");
                if (!uint.TryParse(uidStr, out var uidValue))
                {
                    _logger.LogError("Invalid email UID in filename: {FileName}", fileName);
                    return Stream.Null;
                }

                var uid = new UniqueId(uidValue);

                // Open folder and get message
                var folder = string.IsNullOrEmpty(folderPath)
                    ? _client.Inbox
                    : _client.GetFolder(folderPath);

                await folder.OpenAsync(FolderAccess.ReadOnly);
                var message = await folder.GetMessageAsync(uid);
                await folder.CloseAsync();

                // Write message to memory stream in EML format
                var ms = new MemoryStream();
                await message.WriteToAsync(ms);
                ms.Position = 0;

                return ms;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to download email: {Path}", file.Path);
                return Stream.Null;
            }
        }

        /// <summary>
        /// Estimates email size based on message content.
        /// Not 100% accurate but good enough for progress reporting.
        /// </summary>
        private static long EstimateEmailSize(MimeMessage message)
        {
            long size = 0;

            // Estimate headers (~2KB average)
            size += 2048;

            // Estimate body
            if (!string.IsNullOrEmpty(message.TextBody))
            {
                size += message.TextBody.Length;
            }
            if (!string.IsNullOrEmpty(message.HtmlBody))
            {
                size += message.HtmlBody.Length;
            }

            // Estimate attachments
            foreach (var attachment in message.Attachments)
            {
                if (attachment is MimePart part && part.Content != null)
                {
                    size += part.Content.Stream.Length;
                }
            }

            return size;
        }

        public void Dispose()
        {
            _client?.Disconnect(true);
            _client?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
