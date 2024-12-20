using Octockup.Server.Models;

namespace Octockup.Server.Providers.Storage
{
    public interface IStorageProvider
    {
        string Name { get; }
        IEnumerable<string> Parameters { get; }
        IEnumerable<RemoteFileInfo> GetAllFiles();
    }
}