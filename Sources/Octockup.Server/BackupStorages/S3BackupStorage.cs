using Octockup.Server.Models;
using Octockup.Server.Abstractions;

namespace Octockup.Server.BackupStorages
{
    public class S3BackupStorage : IBackupStorage
    {
        public char PathSeparator => '/';
        public string Id => GetType().FullName!;
        public string Name => "S3 or compatible";

        public IEnumerable<string> RequiredParameters =>
        [
            "accessKey", "secretKey", "bucketName", "region", "endpoint", "folderPath"
        ];

        public Task<Stream> DownloadAsync(string path)
        {
            throw new NotImplementedException();
        }

        public bool? Exists(string path)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<string> GetDirectories(bool recursive = false)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<BackupFileInfo> GetFiles(bool recursive = false)
        {
            throw new NotImplementedException();
        }

        public void SetParameters(Dictionary<string, string> parameters)
        {
            throw new NotImplementedException();
        }

        public Task UploadAsync(string path, Stream data)
        {
            throw new NotImplementedException();
        }
    }
}
