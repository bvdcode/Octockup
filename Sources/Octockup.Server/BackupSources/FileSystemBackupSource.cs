using Octockup.Server.Models;
using Octockup.Server.Abstractions;

namespace Octockup.Server.BackupSources
{
    public class FileSystemBackupSource : IBackupSource
    {
        public string Name => "File System";
        public string Id => GetType().FullName!;
        public IEnumerable<string> RequiredParameters => [ "path" ];

        public IEnumerable<BackupFileInfo> GetFiles(string directory, bool recursive = false)
        {
            if (!Directory.Exists(directory))
            {
                throw new DirectoryNotFoundException($"The directory '{directory}' does not exist.");
            }
            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var files = Directory.GetFiles(directory, "*", searchOption);
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
