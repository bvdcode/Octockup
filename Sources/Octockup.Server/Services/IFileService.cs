namespace Octockup.Server.Services
{
    public interface IFileService
    {
        bool IsStorageHealthy();
        int DeleteEmptyFolders();
        Task DeleteFileAsync(int backupSnapshotId, Guid fileId);
        bool SavedFileExists(int backupSnapshotId, Guid fileId);
        bool FileBackupInfoExists(int backupSnapshotId, Guid fileId);
        Stream GetSavedFileStream(int backupSnapshotId, Guid fileId);
        Task SaveBackupInfoAsync(int backupSnapshotId, Guid fileId, string fileInfoJson, CancellationToken merged);
        Task SaveFileAsync(int backupSnapshotId, Guid fileId, Stream sourceStream, Action<long>? readBytesCount = null, CancellationToken merged = default);
    }
}