using Octockup.Server.Helpers;

namespace Octockup.Server.Services
{
    public class FileSystemService : IFileService
    {
        public Task DeleteFileAsync(int snapshotId, Guid fileId)
        {
            var (savedFile, fileBackupInfo) = FileSystemHelpers.GetSavedFiles(snapshotId, fileId);
            try
            {
                savedFile.Delete();
            }
            catch (Exception) { }
            try
            {
                fileBackupInfo.Delete();
            }
            catch (Exception) { }
            return Task.CompletedTask;
        }

        public bool FileBackupInfoExists(int snapshotId, Guid fileId)
        {
            var (_, fileBackupInfo) = FileSystemHelpers.GetSavedFiles(snapshotId, fileId);
            return fileBackupInfo.Exists;
        }

        public Stream GetSavedFileStream(int snapshotId, Guid newFileId)
        {
            var (savedFile, _) = FileSystemHelpers.GetSavedFiles(snapshotId, newFileId);
            return savedFile.OpenRead();
        }

        public Task SaveBackupInfoAsync(int snapshotId, Guid newFileId, string fileInfoJson, CancellationToken merged)
        {
            var (_, fileBackupInfo) = FileSystemHelpers.GetSavedFiles(snapshotId, newFileId);
            return File.WriteAllTextAsync(fileBackupInfo.FullName, fileInfoJson, merged);
        }

        public bool SavedFileExists(int snapshotId, Guid fileId)
        {
            var (savedFile, _) = FileSystemHelpers.GetSavedFiles(snapshotId, fileId);
            return savedFile.Exists;
        }

        public async Task SaveFileAsync(int snapshotId, Guid newFileId, Stream sourceStream,
            Action<long>? readBytesCount = null, CancellationToken merged = default)
        {
            var (savedFile, fileBackupInfo) = FileSystemHelpers.GetSavedFiles(snapshotId, newFileId);
            using var fileStream = File.OpenWrite(savedFile.FullName);
            _ = Task.Run(() =>
            {
                long prevPosition = 0;
                while (true)
                {
                    readBytesCount?.Invoke(fileStream.Position);
                    Thread.Sleep(1000);
                    if (fileStream.Position == prevPosition)
                    {
                        break;
                    }
                    prevPosition = fileStream.Position;
                }
            }, merged);
            await sourceStream.CopyToAsync(fileStream, merged);
        }
    }
}
