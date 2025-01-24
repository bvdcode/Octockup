namespace Octockup.Server.Services
{
    public interface IFileService
    {
        bool IsStorageHealthy();
        int DeleteEmptyFolders();
        Task DeleteFileAsync(int backupTaskId, Guid fileId);
        bool SavedFileExists(int backupTaskId, Guid fileId);
        bool FileBackupInfoExists(int backupTaskId, Guid fileId);
        Stream GetSavedFileStream(int backupTaskId, Guid fileId);
        Task SaveBackupInfoAsync(int backupTaskId, Guid fileId, string fileInfoJson, CancellationToken merged);
        Task SaveFileAsync(int backupTaskId, Guid fileId, Stream sourceStream, Action<long>? readBytesCount = null, CancellationToken merged = default);
    }
}