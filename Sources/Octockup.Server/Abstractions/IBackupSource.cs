using Octockup.Server.Models;

namespace Octockup.Server.Abstractions
{
    public interface IBackupSource : IBackupModule
    {
        IEnumerable<BackupFileInfo> GetFiles(bool recursive = false);
        void SetParameters(Dictionary<string, string> parameters);
    }
}
