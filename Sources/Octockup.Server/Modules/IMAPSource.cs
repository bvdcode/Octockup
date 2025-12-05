using Octockup.Server.Models;
using Octockup.Server.Abstractions;

namespace Octockup.Server.Modules
{
    public class IMAPSource : IBackupSource
    {
        public string Id => typeof(IMAPSource).FullName!;

        public string Name => "IMAP Email";

        public char PathSeparator => '/';

        public IEnumerable<string> RequiredParameters => ["host", "port", "username", "password", "useSsl"];
        

        public IEnumerable<string> GetDirectories(bool recursive = false)
        {
            return ["/"];
        }

        public IEnumerable<BackupFileInfo> GetFiles(bool recursive = false)
        {
            throw new NotImplementedException();
        }

        public Task<Stream> GetFileStreamAsync(BackupFileInfo file)
        {
            throw new NotImplementedException();
        }

        public void SetIgnoredPaths(ICollection<string>? ignoredPaths)
        {
            // No-op
        }

        public void SetParameters(Dictionary<string, string> parameters)
        {
            throw new NotImplementedException();
        }
    }
}
