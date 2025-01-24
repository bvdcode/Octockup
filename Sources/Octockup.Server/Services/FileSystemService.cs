using Octockup.Server.Helpers;

namespace Octockup.Server.Services
{
    public class FileSystemService : IFileService
    {
        public int DeleteEmptyFolders()
        {
            var root = FileSystemHelpers.GetRootDirectory();
            int deletedFolders = 0;
            var folders = root.GetDirectories("*", SearchOption.TopDirectoryOnly);
            foreach (var folder in folders)
            {
                if (folder.GetFiles().Length == 0 && folder.GetDirectories().Length == 0)
                {
                    try
                    {
                        folder.Delete();
                        deletedFolders++;
                    }
                    catch (Exception) { }
                }
            }
            return deletedFolders;
        }

        public Task DeleteFileAsync(int backupSnapshotId, Guid fileId)
        {
            var (savedFile, fileBackupInfo) = FileSystemHelpers.GetSavedFiles(backupSnapshotId, fileId);
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

        public bool FileBackupInfoExists(int backupSnapshotId, Guid fileId)
        {
            var (_, fileBackupInfo) = FileSystemHelpers.GetSavedFiles(backupSnapshotId, fileId);
            return fileBackupInfo.Exists;
        }

        public Stream GetSavedFileStream(int backupSnapshotId, Guid newFileId)
        {
            var (savedFile, _) = FileSystemHelpers.GetSavedFiles(backupSnapshotId, newFileId);
            return savedFile.OpenRead();
        }

        public Task SaveBackupInfoAsync(int backupSnapshotId, Guid newFileId, string fileInfoJson, CancellationToken merged)
        {
            var (_, fileBackupInfo) = FileSystemHelpers.GetSavedFiles(backupSnapshotId, newFileId);
            return File.WriteAllTextAsync(fileBackupInfo.FullName, fileInfoJson, merged);
        }

        public bool SavedFileExists(int backupSnapshotId, Guid fileId)
        {
            var (savedFile, _) = FileSystemHelpers.GetSavedFiles(backupSnapshotId, fileId);
            return savedFile.Exists;
        }

        public async Task SaveFileAsync(int backupSnapshotId, Guid newFileId, Stream sourceStream,
            Action<long>? readBytesCount = null, CancellationToken merged = default)
        {
            var (savedFile, fileBackupInfo) = FileSystemHelpers.GetSavedFiles(backupSnapshotId, newFileId);
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

        public bool IsStorageHealthy()
        {
            const int threshold = 100 * 1024 * 1024; // 100 MB
            var root = FileSystemHelpers.GetRootDirectory();
            var drive = new DriveInfo(root.Root.FullName);
            return drive.AvailableFreeSpace > threshold;
        }
    }
}
