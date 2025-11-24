using Octockup.Server.Abstractions;
using Octockup.Server.Models;

namespace Octockup.Server.BackupSources
{
    public class FileSystemBackupSource : IBackupSource
    {
        public string Name => "File System";
        public string Id => GetType().FullName!;
        public IEnumerable<string> RequiredParameters => [ "path" ];

        public IEnumerable<BackupFileInfo> GetFiles()
        {
            return [];
        }
    }
}
