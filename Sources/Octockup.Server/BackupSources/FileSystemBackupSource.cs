using Octockup.Server.Models;
using Octockup.Server.Abstractions;

namespace Octockup.Server.BackupSources
{
    public class FileSystemBackupSource : IBackupSource
    {
        public string Name => "File System";
        public string Id => GetType().FullName!;
        public IEnumerable<string> RequiredParameters => [ "path" ];

        private static readonly string _rootDirectory = Path.Combine(AppContext.BaseDirectory, "mounts");
        private string _baseDirectory = _rootDirectory;

        public IEnumerable<BackupFileInfo> GetFiles(bool recursive = false)
        {
            Directory.CreateDirectory(_rootDirectory);
            string fullPath = Path.GetFullPath(Path.Combine(_baseDirectory, _baseDirectory));
            if (!Directory.Exists(fullPath))
            {
                throw new DirectoryNotFoundException($"The directory '{_baseDirectory}' does not exist.");
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

        public void SetParameters(Dictionary<string, string> parameters)
        {
            if (!parameters.TryGetValue("path", out var path))
            {
                throw new ArgumentException("Missing required parameter: path");
            }
            _baseDirectory = Path.GetFullPath(Path.Combine(_rootDirectory, path));
        }
    }
}
