using Octockup.Server.Models;
using Octockup.Server.Abstractions;

namespace Octockup.Server.BackupSources
{
    public class FileSystemBackupSource : IBackupSource
    {
        public string Name => "File System";
        public string Id => GetType().FullName!;
        public IEnumerable<string> RequiredParameters => [ "path" ];

        private static readonly string _baseDirectory = Path.Combine(AppContext.BaseDirectory, "mounts");

        public IEnumerable<BackupFileInfo> GetFiles(string directory, bool recursive = false)
        {
            Directory.CreateDirectory(_baseDirectory);
            string fullPath = Path.GetFullPath(Path.Combine(_baseDirectory, directory));
            if (!Directory.Exists(fullPath))
            {
                throw new DirectoryNotFoundException($"The directory '{directory}' does not exist.");
            }
            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var files = Directory.GetFiles(fullPath, "*", searchOption);
            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                yield return new BackupFileInfo
                {
                    Path = file,
                    Name = fileInfo.Name,
                    Size = fileInfo.Length,
                    LastModified = fileInfo.LastWriteTimeUtc
                };
            }
        }
    }
}
