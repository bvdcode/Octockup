using EasyExtensions;

namespace Octockup.Server.Helpers
{
    public static class FileSystemHelpers
    {
        private const string DataFolder = "data";

        internal static (FileInfo SavedFile, FileInfo FileBackupInfo) GetSavedFile(int snapshotId, Guid fileId)
        {
            string fileFolder = snapshotId.ToString();
            string filePath = Path.Combine(fileFolder, fileId + ".file");
            string fileInfo = filePath[0..^5] + ".backupinfo.json";
            filePath = GetFilePath(filePath);
            fileInfo = GetFilePath(fileInfo);
            return (new FileInfo(filePath), new FileInfo(fileInfo));
        }

        internal static string CalculateSHA512(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            return stream.SHA512();
        }

        internal static string GetFilePath(string filename)
        {
            if (!Directory.Exists(DataFolder))
            {
                Directory.CreateDirectory(DataFolder);
            }
            if (filename.Contains(Path.DirectorySeparatorChar))
            {
                var path = Path.GetDirectoryName(filename) ?? string.Empty;
                string dirPath = Path.Combine(DataFolder, path);
                if (!string.IsNullOrEmpty(path) && !Directory.Exists(dirPath))
                {
                    Directory.CreateDirectory(dirPath);
                }
            }
            return Path.Combine(DataFolder, filename);
        }
    }
}
