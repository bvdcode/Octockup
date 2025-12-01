// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using Amazon.S3;
using Amazon.S3.Model;
using System.Net.Mime;
using Octockup.Server.Models;
using Octockup.Server.Abstractions;

namespace Octockup.Server.Modules
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
            "region", "httpEndpoint", "path"
        ];

        public void SetParameters(Dictionary<string, string> parameters)
        {
            var config = new AmazonS3Config
            {
                UseHttp = false,
                MaxErrorRetry = 5,
                ForcePathStyle = true,
                ServiceURL = parameters["httpEndpoint"],
                AuthenticationRegion = parameters["region"],
            };

            _path = parameters["path"].Trim().Trim('/');
            _bucket = parameters["bucket"];

            string accessKey = parameters["accessKey"];
            string secretKey = parameters["secretKey"];

            _s3 = new AmazonS3Client(accessKey, secretKey, config);
        }

        private string GetBasePrefix()
        {
            return string.IsNullOrWhiteSpace(_path)
                ? string.Empty
                : _path!.TrimEnd('/') + PathSeparator;
        }

        private string ToRelativeKey(string fullKey, string basePrefix)
        {
            if (string.IsNullOrEmpty(basePrefix))
            {
                return fullKey.Trim(PathSeparator);
            }

            if (fullKey.StartsWith(basePrefix, StringComparison.Ordinal))
            {
                return fullKey[basePrefix.Length..].Trim(PathSeparator);
            }

            return fullKey.Trim(PathSeparator);
        }

        public async Task<Stream> GetFileStreamAsync(BackupFileInfo fileInfo)
        {
            string path = fileInfo.Path;
            ArgumentException.ThrowIfNullOrEmpty(path);
            ArgumentNullException.ThrowIfNull(_s3);

            var key = string.IsNullOrEmpty(_path) ? path : $"{_path}/{path}";

            var result = await _s3.GetObjectAsync(new GetObjectRequest
            {
                BucketName = _bucket,
                Key = key
            });

            return result.ResponseStream;
        }

        public Task<bool?> ExistsAsync(string path)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            ArgumentNullException.ThrowIfNull(_s3);

            var key = string.IsNullOrEmpty(_path) ? path : $"{_path}/{path}";

            var req = new GetObjectMetadataRequest
            {
                BucketName = _bucket,
                Key = key
            };

            try
            {
                var res = _s3.GetObjectMetadataAsync(req).Result;
                bool? result = res.HttpStatusCode == System.Net.HttpStatusCode.OK;
                return Task.FromResult(result);
            }
            catch (AggregateException ex) when (
                ex.InnerException is AmazonS3Exception s3Ex &&
                s3Ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return Task.FromResult<bool?>(false);
            }
        }

        public IEnumerable<string> GetDirectories(bool recursive = false)
        {
            ArgumentNullException.ThrowIfNull(_s3);
            ArgumentException.ThrowIfNullOrEmpty(_bucket);

            var basePrefix = GetBasePrefix();
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!recursive)
            {
                string? continuationToken = null;

                do
                {
                    var request = new ListObjectsV2Request
                    {
                        BucketName = _bucket,
                        Prefix = basePrefix,
                        Delimiter = PathSeparator.ToString(),
                        ContinuationToken = continuationToken
                    };

                    var response = _s3.ListObjectsV2Async(request).Result;

                    foreach (var prefix in response.CommonPrefixes)
                    {
                        var relative = ToRelativeKey(prefix, basePrefix);
                        if (!string.IsNullOrEmpty(relative))
                        {
                            result.Add(relative);
                        }
                    }

                    continuationToken = response.IsTruncated == true
                        ? response.NextContinuationToken
                        : null;
                }
                while (continuationToken != null);
                return result;
            }

            {
                string? continuationToken = null;

                do
                {
                    var request = new ListObjectsV2Request
                    {
                        BucketName = _bucket,
                        Prefix = basePrefix,
                        ContinuationToken = continuationToken
                    };

                    var response = _s3.ListObjectsV2Async(request).Result;

                    foreach (var obj in response.S3Objects)
                    {
                        var relativeKey = ToRelativeKey(obj.Key, basePrefix);
                        if (string.IsNullOrEmpty(relativeKey))
                            continue;

                        var segments = relativeKey.Split(PathSeparator, StringSplitOptions.RemoveEmptyEntries);
                        if (segments.Length <= 1)
                            continue;

                        var current = segments[0];
                        result.Add(current);

                        for (int i = 1; i < segments.Length - 1; i++)
                        {
                            current = current + PathSeparator + segments[i];
                            result.Add(current);
                        }
                    }

                    continuationToken = response.IsTruncated == true
                        ? response.NextContinuationToken
                        : null;
                }
                while (continuationToken != null);
                return result;
            }
        }

        public IEnumerable<BackupFileInfo> GetFiles(bool recursive = false)
        {
            ArgumentNullException.ThrowIfNull(_s3);
            ArgumentException.ThrowIfNullOrEmpty(_bucket);

            var basePrefix = GetBasePrefix();
            var files = new List<BackupFileInfo>();
            string? continuationToken = null;

            do
            {
                var request = new ListObjectsV2Request
                {
                    BucketName = _bucket,
                    Prefix = basePrefix,
                    ContinuationToken = continuationToken
                };

                var response = _s3.ListObjectsV2Async(request).Result;

                foreach (var obj in response.S3Objects)
                {
                    if (obj.Key.EndsWith(PathSeparator))
                    {
                        continue;
                    }

                    var relativeKey = ToRelativeKey(obj.Key, basePrefix);
                    if (string.IsNullOrEmpty(relativeKey))
                    {
                        continue;
                    }

                    if (!recursive && relativeKey.Contains(PathSeparator))
                    {
                        continue;
                    }

                    var info = new BackupFileInfo
                    {
                        Size = obj.Size,
                        Path = relativeKey,
                        Name = Path.GetFileName(relativeKey),
                        LastModified = obj.LastModified?.ToUniversalTime()
                    };

                    files.Add(info);
                }

                continuationToken = response.IsTruncated == true
                    ? response.NextContinuationToken
                    : null;
            }
            while (continuationToken != null);
            return files;
        }

        public Task UploadAsync(string path, Stream data)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            ArgumentNullException.ThrowIfNull(_s3);

            var key = string.IsNullOrEmpty(_path) ? path : $"{_path}/{path}";

            PutObjectRequest req = new()
            {
                InputStream = data,
                BucketName = _bucket,
                UseChunkEncoding = false,
                ContentType = MediaTypeNames.Application.Octet,
                Key = key
            };

            return _s3.PutObjectAsync(req);
        }

        public async Task<bool?> DeleteAsync(string path)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            ArgumentNullException.ThrowIfNull(_s3);
            var key = string.IsNullOrEmpty(_path) ? path : $"{_path}/{path}";
            var result = await _s3.DeleteObjectAsync(_bucket, key);
            return result.HttpStatusCode == System.Net.HttpStatusCode.NoContent;
        }
    }
}
