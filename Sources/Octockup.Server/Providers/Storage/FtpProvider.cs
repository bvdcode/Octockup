using Octockup.Server.Models;

namespace Octockup.Server.Providers.Storage
{
    public class FtpProvider : IStorageProvider
    {
        public string Name => "FTP";

        public IEnumerable<string> Parameters =>
        [
            "RemoteHost", "Username", "Password", "RemotePort"
        ];

        public IEnumerable<RemoteFileInfo> GetAllFiles()
        {
            throw new NotImplementedException();
        }
    }
}
