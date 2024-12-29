using FluentFTP;
using Octockup.Server.Models;

namespace Octockup.Server.Providers.Storage
{
    public class FtpProvider : IStorageProvider<BaseStorageParameters>
    {
        public string Name => "FTP";
        public BaseStorageParameters Parameters { get; set; } = null!;
        
        private FtpClient? _client;

        public IEnumerable<RemoteFileInfo> GetAllFiles()
        {
            _client ??= CreateClient();
            var items = _client.GetListing(Parameters.RemotePath);
            return items.Select(i => new RemoteFileInfo
            {
                Name = i.Name,
                Path = i.FullName,
                Size = i.Size,
                LastModified = i.Modified
            });
        }

        private FtpClient CreateClient()
        {
            var client = new FtpClient(Parameters.RemoteHost, Parameters.Username, Parameters.Password, Parameters.RemotePort);
            client.AutoConnect();
            return client;
        }
    }
}