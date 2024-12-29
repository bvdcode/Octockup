using FluentFTP;
using Octockup.Server.Models;

namespace Octockup.Server.Providers.Storage
{
    public class FtpProvider(ILogger<FtpProvider> _logger) : IStorageProvider<BaseStorageParameters>
    {
        public string Name => "FTP";
        public BaseStorageParameters Parameters { get; set; } = null!;

        private FtpClient? _client;

        public IEnumerable<RemoteFileInfo> GetAllFiles()
        {
            return GetAllFiles(Parameters.RemotePath);
        }

        private IEnumerable<RemoteFileInfo> GetAllFiles(string remotePath)
        {
            _client ??= CreateClient();
            var files = _client.GetListing(remotePath);
            _logger.LogInformation("Got {count} files from {path}", files.Length, remotePath);
            foreach (var file in files)
            {
                if (file.FullName == "." 
                    || file.FullName == ".." 
                    || file.FullName == remotePath)
                {
                    continue;
                }
                if (file.Type == FtpObjectType.Directory)
                {
                    foreach (var item in GetAllFiles(file.FullName))
                    {
                        yield return item;
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
                    Path = file.FullName,
                    Size = file.Size,
                    LastModified = file.Modified
                };
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