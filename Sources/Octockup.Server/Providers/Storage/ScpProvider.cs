using Octockup.Server.Models;

namespace Octockup.Server.Providers.Storage
{
    public class ScpProvider : IStorageProvider<BaseStorageParameters>
    {
        public string Name => "SCP - Secure Copy Protocol";
        public BaseStorageParameters Parameters { get; set; } = null!;

        public IEnumerable<RemoteFileInfo> GetAllFiles(Action<int>? progressCallback = null, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
