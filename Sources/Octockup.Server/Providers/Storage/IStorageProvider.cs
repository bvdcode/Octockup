using Octockup.Server.Models;

namespace Octockup.Server.Providers.Storage
{
    public interface IStorageProvider<TParams> : IStorageProvider where TParams : class
    {
        TParams Parameters { get; set; }
    }

    public interface IStorageProvider
    {
        string Name { get; }
        IEnumerable<RemoteFileInfo> GetAllFiles();
    }
}