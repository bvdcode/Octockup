using Octockup.Server.Models;

namespace Octockup.Server.Providers.Storage
{
    public class ScpProvider : IStorageProvider
    {
        public string Name => throw new NotImplementedException();

        public IEnumerable<string> Parameters => throw new NotImplementedException();

        public IEnumerable<RemoteFileInfo> GetAllFiles()
        {
            throw new NotImplementedException();
        }
    }
}
