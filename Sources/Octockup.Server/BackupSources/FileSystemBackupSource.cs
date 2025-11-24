using Octockup.Server.Abstractions;

namespace Octockup.Server.BackupSources
{
    public class FileSystemBackupSource : IBackupSource
    {
        public string Name => "File System";
        public string Id => GetType().FullName!;
        public IEnumerable<string> RequiredParameters => [ "path" ];
    }
}
