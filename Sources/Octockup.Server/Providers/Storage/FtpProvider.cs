using FluentFTP;
using Octockup.Server.Models;
using System.Text.RegularExpressions;

namespace Octockup.Server.Providers.Storage
{
    public class FtpProvider(ILogger<FtpProvider> _logger) : IStorageProvider<BaseStorageParameters>
    {
        public string Name => "FTP - File Transfer Protocol";
        public BaseStorageParameters Parameters { get; set; } = null!;

        private FtpClient? _client;

        private static readonly string[] ignored = ["httpcache"];

        public IEnumerable<RemoteFileInfo> GetAllFiles(Action<int>? progressCallback = null, CancellationToken cancellationToken = default)
        {
            return GetAllFiles(Parameters.RemotePath, progressCallback, cancellationToken: cancellationToken);
        }

        public Stream GetFileStream(RemoteFileInfo fileInfo)
        {
            _client ??= CreateClient();
            return _client.OpenRead(fileInfo.Path, FtpDataType.Binary);
        }

        private IEnumerable<RemoteFileInfo> GetAllFiles(string remotePath,
            Action<int>? progressCallback = null, CancellationToken cancellationToken = default)
        {
            int counter = 0;
            _client ??= CreateClient();
            var files = _client.GetListing(remotePath);
            _logger.LogInformation("Got {count} files from {path}", files.Length, remotePath);
            progressCallback?.Invoke(counter);
            foreach (var file in files)
            {
                if (ignored.Any(i => Regex.IsMatch(file.Name, i)))
                {
                    _logger.LogInformation("Ignoring item: {file}", file.Name);
                    continue;
                }
                cancellationToken.ThrowIfCancellationRequested();
                if (file.FullName == "."
                    || file.FullName == ".."
                    || file.FullName == remotePath)
                {
                    continue;
                }
                if (file.Type == FtpObjectType.Directory)
                {
                    foreach (var item in GetAllFiles(file.FullName, progressCallback, cancellationToken))
                    {
                        yield return item;
                        counter++;
                    }
                    continue;
                }
                if (file.Type != FtpObjectType.File)
                {
                    continue;
                }
                yield return new RemoteFileInfo
                {
                    Name = file.Name,
                    Size = file.Size,
                    Path = file.FullName,
                    LastModified = file.Modified,
                    FileCreatedAt = file.Created
                };
                counter++;
            }
        }

        private FtpClient CreateClient()
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(Parameters.RemoteHost, nameof(Parameters.RemoteHost));
            ArgumentException.ThrowIfNullOrWhiteSpace(Parameters.Username, nameof(Parameters.Username));
            ArgumentException.ThrowIfNullOrWhiteSpace(Parameters.Password, nameof(Parameters.Password));
            ArgumentOutOfRangeException.ThrowIfLessThan(Parameters.RemotePort, 1, nameof(Parameters.RemotePort));
            var client = new FtpClient(Parameters.RemoteHost, Parameters.Username, Parameters.Password, Parameters.RemotePort);
            client.AutoConnect();
            return client;
        }
    }
}