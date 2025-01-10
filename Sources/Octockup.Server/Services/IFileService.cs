namespace Octockup.Server.Services
{
    public interface IFileService
    {
        Task DeleteFileAsync(int id, Guid fileId);
        bool FileBackupInfoExists(int id, Guid fileId);
        Stream GetSavedFileStream(int id, Guid newFileId);
        Task SaveBackupInfoAsync(int id, Guid newFileId, string fileInfoJson, CancellationToken merged);
        bool SavedFileExists(int id, Guid fileId);
        Task SaveFileAsync(int id, Guid newFileId, Stream sourceStream, Action<long>? readBytesCount = null, CancellationToken merged = default);
    }
}