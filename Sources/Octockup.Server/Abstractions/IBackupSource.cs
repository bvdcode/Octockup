using Octockup.Server.Models;

namespace Octockup.Server.Abstractions
{
    public interface IBackupSource : IBackupProvider
    {
        IEnumerable<string> GetDirectories(bool recursive = false);
        IEnumerable<BackupFileInfo> GetFiles(bool recursive = false);
    }
}
