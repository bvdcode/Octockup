using Amazon.S3;
using Amazon.S3.Model;
using System.Net.Mime;
using Octockup.Server.Models;
using Octockup.Server.Abstractions;

namespace Octockup.Server.BackupStorages
{
    public class S3BackupStorage : IBackupStorage
    {
        public char PathSeparator => '/';
        public string Id => GetType().FullName!;
        public string Name => "S3 or compatible";

        private string? _path;
        private string? _bucket;
        private AmazonS3Client? _s3;

        public IEnumerable<string> RequiredParameters =>
        [
            "accessKey", "secretKey", "bucket",
            "region", "endpoint", "path"
        ];

        public void SetParameters(Dictionary<string, string> parameters)
        {
            var config = new AmazonS3Config
            {
                UseHttp = false,
                MaxErrorRetry = 5,
                ForcePathStyle = true,
                ServiceURL = parameters["endpoint"],
                AuthenticationRegion = parameters["region"],
            };
            _path = parameters["path"].Trim().Trim('/');
            _bucket = parameters["bucket"];
            string accessKey = parameters["accessKey"];
            string secretKey = parameters["secretKey"];
            _s3 = new AmazonS3Client(accessKey, secretKey, config);
        }

        public async Task<Stream> DownloadAsync(string path)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            ArgumentNullException.ThrowIfNull(_s3);
            var result = await _s3.GetObjectAsync(new GetObjectRequest
            {
                BucketName = _bucket,
                Key = string.IsNullOrEmpty(_path) ? path : $"{_path}/{path}"
            });
            return result.ResponseStream;
        }

        public bool? Exists(string path)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            ArgumentNullException.ThrowIfNull(_s3);
            var req = new GetObjectMetadataRequest
            {
                BucketName = _bucket,
                Key = string.IsNullOrEmpty(_path) ? path : $"{_path}/{path}"
            };
            try
            {
                var res = _s3.GetObjectMetadataAsync(req).Result;
                return res.HttpStatusCode == System.Net.HttpStatusCode.OK;
            }
            catch (AggregateException ex) when (ex.InnerException is Amazon.S3.AmazonS3Exception s3Ex && s3Ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return false;
            }
        }

        public IEnumerable<string> GetDirectories(bool recursive = false)
        {
            ArgumentNullException.ThrowIfNull(_s3);

        }

        public IEnumerable<BackupFileInfo> GetFiles(bool recursive = false)
        {

        }

        public Task UploadAsync(string path, Stream data)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            ArgumentNullException.ThrowIfNull(_s3);
            PutObjectRequest req = new()
            {
                InputStream = data,
                BucketName = _bucket,
                UseChunkEncoding = false,
                ContentType = MediaTypeNames.Application.Octet,
                Key = string.IsNullOrEmpty(_path) ? path : $"{_path}/{path}"
            };
            return _s3.PutObjectAsync(req);
        }
    }
}
