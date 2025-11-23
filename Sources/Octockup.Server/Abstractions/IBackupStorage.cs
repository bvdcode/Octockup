namespace Octockup.Server.Abstractions
{
    public interface IBackupStorage
    {
        bool? Exists(string path);
        Task UploadAsync(string path, Stream data);
        Task<Stream> DownloadAsync(string path);
    }
}
