using Octockup.Server.Models;

namespace Octockup.Server.Providers.Storage
{
    public class YouTubeChannelProvider : IStorageProvider<YouTubeChannelParameters>
    {
        public YouTubeChannelParameters Parameters { get; set; } = null!;

        public string Name => "YouTube Channel";

        public IEnumerable<RemoteFileInfo> GetAllFiles(Action<int>? progressCallback = null, CancellationToken cancellationToken = default)
        {
            return [];
        }
    }

    public class YouTubeChannelParameters
    {
        public string ChannelId { get; set; } = string.Empty;
    }
}
