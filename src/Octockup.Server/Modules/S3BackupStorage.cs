// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Amazon.S3;
using Amazon.S3.Model;
using Octockup.Server.Abstractions;
using Octockup.Server.Helpers;
using Octockup.Server.Models;
using System.Net;
using System.Net.Mime;

namespace Octockup.Server.Modules
{
    public class S3BackupStorage(ILogger<S3BackupStorage> _logger) : IBackupStorage
    {
        public char PathSeparator => '/';
        public string Id => GetType().FullName!;
        public string Name => "S3 or compatible";

        private string? _path;
        private string? _bucket;
        private AmazonS3Client? _s3;
        private bool _useChunkEncoding = true;
        private bool _validateChecksums = false;
        private ICollection<string>? _ignoredPaths;

        public IEnumerable<string> RequiredParameters =>
        [
            "accessKey", "secretKey", "bucket",
            "region", "httpEndpoint", "path",
            "validateChecksums"
        ];

        public void SetParameters(IReadOnlyDictionary<string, string> parameters)
        {
            AmazonS3Config config = new AmazonS3Config
            {
                UseHttp = false,
                MaxErrorRetry = 5,
                ForcePathStyle = true,
                ServiceURL = parameters["httpEndpoint"],
                AuthenticationRegion = parameters["region"],
                RequestChecksumCalculation = _validateChecksums
                    ? Amazon.Runtime.RequestChecksumCalculation.WHEN_SUPPORTED
                    : Amazon.Runtime.RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = _validateChecksums
                    ? Amazon.Runtime.ResponseChecksumValidation.WHEN_SUPPORTED
                    : Amazon.Runtime.ResponseChecksumValidation.WHEN_REQUIRED
            };

            _validateChecksums = parameters.TryGetValue("validateChecksums", out string? validateStr) &&
                                 bool.TryParse(validateStr, out bool validateBool) &&
                                 validateBool;
            _path = parameters["path"].Trim().Trim('/');
            _bucket = parameters["bucket"];

            string accessKey = parameters["accessKey"];
            string secretKey = parameters["secretKey"];

            bool hasChunkEncodingParam = parameters.TryGetValue("useChunkEncoding", out string? chunkEncodingStr);
            if (hasChunkEncodingParam)
            {
                bool parsed = bool.TryParse(chunkEncodingStr, out bool chunkEncodingBool);
                _useChunkEncoding = parsed && chunkEncodingBool;
            }

            _s3 = new AmazonS3Client(accessKey, secretKey, config);
        }

        public void SetIgnoredPaths(ICollection<string>? ignoredPaths)
        {
            _ignoredPaths = ignoredPaths;
        }

        private string GetBasePrefix()
        {
            return string.IsNullOrWhiteSpace(_path)
                ? string.Empty
                : _path!.TrimEnd('/') + PathSeparator;
        }

        private string GetFullKey(string relativePath)
        {
            string basePrefix = GetBasePrefix();
            return string.IsNullOrEmpty(basePrefix)
                ? relativePath.Trim(PathSeparator)
                : basePrefix + relativePath.Trim(PathSeparator);
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

        public async Task<Stream> GetFileStreamAsync(BackupFileInfo fileInfo, CancellationToken cancellationToken = default)
        {
            string path = fileInfo.Path;
            ArgumentException.ThrowIfNullOrEmpty(path);
            ArgumentNullException.ThrowIfNull(_s3);

            string key = GetFullKey(path);

            try
            {
                GetObjectResponse result = await _s3.GetObjectAsync(new GetObjectRequest
                {
                    Key = key,
                    BucketName = _bucket,
                    ChecksumMode = new ChecksumMode("DISABLED")
                }, cancellationToken);

                return result.ResponseStream;
            }
            catch (ArgumentOutOfRangeException ex)
            {
                if (ex.Message.Contains("hex") && !_validateChecksums)
                {
                    _logger.LogWarning("Checksum error detected while downloading file {FilePath}. Bypassing checksum validation through presigned URL.", path);
                    string presignedUrl = await _s3.GetPreSignedURLAsync(new GetPreSignedUrlRequest
                    {
                        Key = key,
                        BucketName = _bucket,
                        Expires = DateTime.UtcNow.AddHours(1)
                    });
                    HttpClient httpClient = new HttpClient();
                    HttpResponseMessage response = await httpClient.GetAsync(presignedUrl, cancellationToken);
                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadAsStreamAsync(cancellationToken);
                }
                throw;
            }
        }

        public async Task<bool?> ExistsAsync(string path, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            ArgumentNullException.ThrowIfNull(_s3);

            string key = GetFullKey(path);

            GetObjectMetadataRequest req = new GetObjectMetadataRequest
            {
                Key = key,
                BucketName = _bucket,
            };

            try
            {
                GetObjectMetadataResponse res = await _s3.GetObjectMetadataAsync(req, cancellationToken);
                bool? result = res.HttpStatusCode == HttpStatusCode.OK;
                return result;
            }
            catch (AmazonS3Exception s3Ex) when (s3Ex.StatusCode == HttpStatusCode.NotFound)
            {
                return false;
            }
        }

        public IEnumerable<string> GetDirectories(bool recursive = false, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(_s3);
            ArgumentException.ThrowIfNullOrEmpty(_bucket);

            string basePrefix = GetBasePrefix();
            HashSet<string> result = new(StringComparer.OrdinalIgnoreCase);
            string? delimiter = recursive ? null : PathSeparator.ToString();

            foreach (ListObjectsV2Response response in ListObjectPages(
                basePrefix,
                delimiter,
                cancellationToken))
            {
                if (recursive)
                {
                    AddRecursiveDirectories(response, basePrefix, result, cancellationToken);
                }
                else
                {
                    AddDirectDirectories(response, basePrefix, result, cancellationToken);
                }
            }

            return result;
        }

        public IEnumerable<BackupFileInfo> GetFiles(bool recursive = false, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(_s3);
            ArgumentException.ThrowIfNullOrEmpty(_bucket);

            string basePrefix = GetBasePrefix();
            List<BackupFileInfo> files = [];

            foreach (ListObjectsV2Response response in ListObjectPages(
                basePrefix,
                delimiter: null,
                cancellationToken))
            {
                IEnumerable<S3Object> objects = response.S3Objects ?? Enumerable.Empty<S3Object>();
                foreach (S3Object s3Object in objects)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    BackupFileInfo? file = CreateFileInfo(s3Object, basePrefix, recursive);
                    if (file is not null)
                    {
                        files.Add(file);
                    }
                }
            }

            return files;
        }

        private IEnumerable<ListObjectsV2Response> ListObjectPages(
            string prefix,
            string? delimiter,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(_s3);
            string? continuationToken = null;

            do
            {
                cancellationToken.ThrowIfCancellationRequested();
                ListObjectsV2Request request = new()
                {
                    BucketName = _bucket,
                    Prefix = prefix,
                    Delimiter = delimiter,
                    ContinuationToken = continuationToken,
                };

                ListObjectsV2Response? response;
                try
                {
                    response = _s3.ListObjectsV2Async(request, cancellationToken)
                        .GetAwaiter()
                        .GetResult();
                }
                catch (Exception exception)
                {
                    _logger.LogDebug(exception, "S3 list request failed for prefix {Prefix}", prefix);
                    yield break;
                }

                if (response is null)
                {
                    yield break;
                }

                yield return response;
                continuationToken = response.IsTruncated == true
                    ? response.NextContinuationToken
                    : null;
            }
            while (continuationToken is not null);
        }

        private void AddDirectDirectories(
            ListObjectsV2Response response,
            string basePrefix,
            ISet<string> result,
            CancellationToken cancellationToken)
        {
            IEnumerable<string> prefixes = response.CommonPrefixes ?? Enumerable.Empty<string>();
            foreach (string prefix in prefixes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrEmpty(prefix))
                {
                    continue;
                }

                string relative = ToRelativeKey(prefix, basePrefix);
                if (string.IsNullOrEmpty(relative) || IsIgnored(relative, fileName: null))
                {
                    continue;
                }

                result.Add(relative);
            }
        }

        private void AddRecursiveDirectories(
            ListObjectsV2Response response,
            string basePrefix,
            ISet<string> result,
            CancellationToken cancellationToken)
        {
            IEnumerable<S3Object> objects = response.S3Objects ?? Enumerable.Empty<S3Object>();
            foreach (S3Object s3Object in objects)
            {
                cancellationToken.ThrowIfCancellationRequested();
                AddObjectDirectories(s3Object, basePrefix, result, cancellationToken);
            }
        }

        private void AddObjectDirectories(
            S3Object s3Object,
            string basePrefix,
            ISet<string> result,
            CancellationToken cancellationToken)
        {
            if (s3Object is null || string.IsNullOrEmpty(s3Object.Key))
            {
                return;
            }

            string relativeKey = ToRelativeKey(s3Object.Key, basePrefix);
            string[] segments = relativeKey.Split(PathSeparator, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length <= 1)
            {
                return;
            }

            string current = segments[0];
            for (int index = 1; index < segments.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (IsIgnored(current, fileName: null))
                {
                    return;
                }

                result.Add(current);
                current += PathSeparator + segments[index];
            }
        }

        private BackupFileInfo? CreateFileInfo(
            S3Object s3Object,
            string basePrefix,
            bool recursive)
        {
            if (s3Object is null
                || string.IsNullOrEmpty(s3Object.Key)
                || s3Object.Key.EndsWith(PathSeparator))
            {
                return null;
            }

            string relativeKey = ToRelativeKey(s3Object.Key, basePrefix);
            if (string.IsNullOrEmpty(relativeKey)
                || (!recursive && relativeKey.Contains(PathSeparator)))
            {
                return null;
            }

            string fileName = Path.GetFileName(relativeKey);
            if (IsIgnored(relativeKey, fileName))
            {
                return null;
            }

            return new BackupFileInfo
            {
                Size = s3Object.Size,
                Path = relativeKey,
                Name = fileName,
                LastModified = s3Object.LastModified?.ToUniversalTime(),
            };
        }

        private bool IsIgnored(string relativePath, string? fileName)
        {
            if (_ignoredPaths is null
                || !ScheduleHelpers.IsPathIgnored(PathSeparator + relativePath, fileName, _ignoredPaths))
            {
                return false;
            }

            _logger.LogDebug("Skipping ignored S3 path: {Name}", relativePath);
            return true;
        }

        public Task UploadAsync(string path, Stream data, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            ArgumentNullException.ThrowIfNull(_s3);

            string key = GetFullKey(path);

            PutObjectRequest req = new()
            {
                Key = key,
                InputStream = data,
                BucketName = _bucket,
                UseChunkEncoding = _useChunkEncoding,
                ContentType = MediaTypeNames.Application.Octet,
            };

            return _s3.PutObjectAsync(req, cancellationToken);
        }

        public async Task<bool?> DeleteAsync(string path, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            ArgumentNullException.ThrowIfNull(_s3);

            string key = GetFullKey(path);
            DeleteObjectResponse result = await _s3.DeleteObjectAsync(_bucket, key, cancellationToken);
            return result.HttpStatusCode == System.Net.HttpStatusCode.NoContent;
        }

        public async Task<BackupFileInfo?> GetFileInfoAsync(string path, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            ArgumentNullException.ThrowIfNull(_s3);

            string key = GetFullKey(path);

            try
            {
                GetObjectMetadataRequest request = new GetObjectMetadataRequest
                {
                    Key = key,
                    BucketName = _bucket,
                };

                GetObjectMetadataResponse response = await _s3.GetObjectMetadataAsync(request, cancellationToken);

                if (response.HttpStatusCode != HttpStatusCode.OK)
                {
                    return null;
                }

                string fileName = Path.GetFileName(path);
                string relativePath = ToRelativeKey(key, GetBasePrefix());

                return new BackupFileInfo
                {
                    Path = relativePath,
                    Name = fileName,
                    Size = response.ContentLength,
                    LastModified = response.LastModified?.ToUniversalTime()
                };
            }
            catch (AmazonS3Exception s3Ex) when (s3Ex.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get file info for '{Path}' from S3", path);
                return null;
            }
        }
    }
}
