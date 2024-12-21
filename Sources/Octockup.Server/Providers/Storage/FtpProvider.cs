using Octockup.Server.Models;

namespace Octockup.Server.Providers.Storage
{
    public class FtpProvider : IStorageProvider<BaseStorageParameters>
    {
        public string Name => "FTP";
        public BaseStorageParameters Parameters { get; set; } = null!;

        public IEnumerable<RemoteFileInfo> GetAllFiles()
        {
            throw new NotImplementedException();
        }
    }
}