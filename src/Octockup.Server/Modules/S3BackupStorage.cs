// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov | bvdcode | belov.us

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
            var config = new AmazonS3Config
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

            _validateChecksums = parameters.TryGetValue("validateChecksums", out var validateStr) &&
                                 bool.TryParse(validateStr, out var validateBool) &&
                                 validateBool;
            _path = parameters["path"].Trim().Trim('/');
            _bucket = parameters["bucket"];

            string accessKey = parameters["accessKey"];
            string secretKey = parameters["secretKey"];

            bool hasChunkEncodingParam = parameters.TryGetValue("useChunkEncoding", out var chunkEncodingStr);
            if (hasChunkEncodingParam)
            {
                bool parsed = bool.TryParse(chunkEncodingStr, out var chunkEncodingBool);
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
            var basePrefix = GetBasePrefix();
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

            var key = GetFullKey(path);

            try
            {
                var result = await _s3.GetObjectAsync(new GetObjectRequest
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
                    string presignedUrl = _s3.GetPreSignedURL(new GetPreSignedUrlRequest
                    {
                        Key = key,
                        BucketName = _bucket,
                        Expires = DateTime.UtcNow.AddHours(1)
                    });
                    var httpClient = new HttpClient();
                    var response = await httpClient.GetAsync(presignedUrl, cancellationToken);
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

            var key = GetFullKey(path);

            var req = new GetObjectMetadataRequest
            {
                Key = key,
                BucketName = _bucket,
            };

            try
            {
                var res = await _s3.GetObjectMetadataAsync(req, cancellationToken);
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

            var basePrefix = GetBasePrefix();
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!recursive)
            {
                string? continuationToken = null;

                do
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var request = new ListObjectsV2Request
                    {
                        BucketName = _bucket,
                        Prefix = basePrefix,
                        Delimiter = PathSeparator.ToString(),
                        ContinuationToken = continuationToken
                    };

                    ListObjectsV2Response? response;
                    try
                    {
                        response = _s3.ListObjectsV2Async(request, cancellationToken).GetAwaiter().GetResult();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "S3 list request failed for prefix {Prefix}", basePrefix);
                        break;
                    }

                    if (response == null)
                    {
                        break;
                    }

                    var prefixes = response.CommonPrefixes ?? Enumerable.Empty<string>();

                    foreach (var prefix in prefixes)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (string.IsNullOrEmpty(prefix))
                        {
                            continue;
                        }

                        var relative = ToRelativeKey(prefix, basePrefix);
                        if (!string.IsNullOrEmpty(relative))
                        {
                            // Check if directory is ignored
                            if (_ignoredPaths != null && ScheduleHelpers.IsPathIgnored(PathSeparator + relative, null, _ignoredPaths))
                            {
                                _logger.LogDebug("Skipping ignored S3 directory: {Name}", relative);
                                continue;
                            }
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
                    cancellationToken.ThrowIfCancellationRequested();
                    var request = new ListObjectsV2Request
                    {
                        BucketName = _bucket,
                        Prefix = basePrefix,
                        ContinuationToken = continuationToken
                    };

                    ListObjectsV2Response? response;
                    try
                    {
                        response = _s3.ListObjectsV2Async(request, cancellationToken).GetAwaiter().GetResult();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "S3 list request failed for prefix {Prefix}", basePrefix);
                        break;
                    }

                    if (response == null)
                    {
                        break;
                    }

                    var objects = response.S3Objects ?? Enumerable.Empty<S3Object>();

                    foreach (var obj in objects)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (obj == null || string.IsNullOrEmpty(obj.Key))
                        {
                            continue;
                        }

                        var relativeKey = ToRelativeKey(obj.Key, basePrefix);
                        if (string.IsNullOrEmpty(relativeKey))
                        {
                            continue;
                        }

                        var segments = relativeKey.Split(PathSeparator, StringSplitOptions.RemoveEmptyEntries);
                        if (segments.Length <= 1)
                        {
                            continue;
                        }

                        var current = segments[0];

                        // Check each directory segment
                        if (_ignoredPaths != null && ScheduleHelpers.IsPathIgnored(PathSeparator + current, null, _ignoredPaths))
                        {
                            continue;
                        }

                        result.Add(current);

                        for (int i = 1; i < segments.Length - 1; i++)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            current = current + PathSeparator + segments[i];

                            if (_ignoredPaths != null && ScheduleHelpers.IsPathIgnored(PathSeparator + current, null, _ignoredPaths))
                            {
                                break;
                            }

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

        public IEnumerable<BackupFileInfo> GetFiles(bool recursive = false, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(_s3);
            ArgumentException.ThrowIfNullOrEmpty(_bucket);

            var basePrefix = GetBasePrefix();
            var files = new List<BackupFileInfo>();
            string? continuationToken = null;

            do
            {
                cancellationToken.ThrowIfCancellationRequested();
                var request = new ListObjectsV2Request
                {
                    BucketName = _bucket,
                    Prefix = basePrefix,
                    ContinuationToken = continuationToken
                };

                ListObjectsV2Response? response;
                try
                {
                    response = _s3.ListObjectsV2Async(request, cancellationToken).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "S3 list request failed for prefix {Prefix}", basePrefix);
                    break;
                }

                if (response == null)
                {
                    break;
                }

                var objects = response.S3Objects ?? Enumerable.Empty<S3Object>();

                foreach (var obj in objects)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (obj == null || string.IsNullOrEmpty(obj.Key))
                    {
                        continue;
                    }

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

                    // Check if file or its parent directories are ignored
                    if (_ignoredPaths != null)
                    {
                        var fileName = Path.GetFileName(relativeKey);
                        if (ScheduleHelpers.IsPathIgnored(PathSeparator + relativeKey, fileName, _ignoredPaths))
                        {
                            _logger.LogDebug("Skipping ignored S3 file: {Name}", relativeKey);
                            continue;
                        }
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

        public Task UploadAsync(string path, Stream data, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            ArgumentNullException.ThrowIfNull(_s3);

            var key = GetFullKey(path);

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

            var key = GetFullKey(path);
            var result = await _s3.DeleteObjectAsync(_bucket, key, cancellationToken);
            return result.HttpStatusCode == System.Net.HttpStatusCode.NoContent;
        }

        public async Task<BackupFileInfo?> GetFileInfoAsync(string path, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrEmpty(path);
            ArgumentNullException.ThrowIfNull(_s3);

            var key = GetFullKey(path);

            try
            {
                var request = new GetObjectMetadataRequest
                {
                    Key = key,
                    BucketName = _bucket,
                };

                var response = await _s3.GetObjectMetadataAsync(request, cancellationToken);

                if (response.HttpStatusCode != HttpStatusCode.OK)
                {
                    return null;
                }

                var fileName = Path.GetFileName(path);
                var relativePath = ToRelativeKey(key, GetBasePrefix());

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
