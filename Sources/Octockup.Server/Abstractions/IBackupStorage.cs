namespace Octockup.Server.Abstractions
{
    public interface IBackupStorage : IBackupSource, IBackupModule
    {
        bool? Exists(string path);
        Task DeleteAsync(string path);
        Task UploadAsync(string path, Stream data);
        Task<Stream> DownloadAsync(string path);
    }
}
