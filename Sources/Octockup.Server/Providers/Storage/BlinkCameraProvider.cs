using Octockup.Server.Models;

namespace Octockup.Server.Providers.Storage
{
    public class BlinkCameraProvider : IStorageProvider<BaseStorageParameters>
    {
        public BaseStorageParameters Parameters { get; set; } = null!;

        public string Name => "Blink Camera Storage";

        public IEnumerable<RemoteFileInfo> GetAllFiles()
        {
            return [];
        }
    }
}
