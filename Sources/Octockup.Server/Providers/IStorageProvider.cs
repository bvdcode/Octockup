namespace Octockup.Server.Providers
{
    public interface IStorageProvider
    {
        string Name { get; }
        IEnumerable<string> Parameters { get; }
        IEnumerable<RemoteFileInfo> GetAllFiles();
    }
}