namespace Octockup.Server.Providers
{
    public interface IStorageProvider
    {
        IEnumerable<RemoteFileInfo> GetAllFiles();
    }
}