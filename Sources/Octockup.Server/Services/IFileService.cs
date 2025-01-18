namespace Octockup.Server.Services
{
    public interface IFileService
    {
        bool IsStorageHealthy();
        int DeleteEmptyFolders();
        Task DeleteFileAsync(int id, Guid fileId);
        bool SavedFileExists(int id, Guid fileId);
        bool FileBackupInfoExists(int id, Guid fileId);
        Stream GetSavedFileStream(int id, Guid newFileId);
        Task SaveBackupInfoAsync(int id, Guid newFileId, string fileInfoJson, CancellationToken merged);
        Task SaveFileAsync(int id, Guid newFileId, Stream sourceStream, Action<long>? readBytesCount = null, CancellationToken merged = default);
    }
}