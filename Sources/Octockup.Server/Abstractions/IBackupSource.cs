using Octockup.Server.Models;

namespace Octockup.Server.Abstractions
{
    public interface IBackupSource : IBackupModule
    {
        IEnumerable<BackupFileInfo> GetFiles();
    }
}
