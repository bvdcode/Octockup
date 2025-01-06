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
        Stream GetFileStream(RemoteFileInfo fileInfo);
        IEnumerable<RemoteFileInfo> GetAllFiles(Action<int>? progressCallback = null, CancellationToken cancellationToken = default);
    }
}