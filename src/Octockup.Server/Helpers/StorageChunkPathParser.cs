// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using System.Diagnostics.CodeAnalysis;

namespace Octockup.Server.Helpers
{
    public static class StorageChunkPathParser
    {
        private const string Version = "v2";
        private const int HashLength = 64;

        public static bool TryParse(
            string path,
            char pathSeparator,
            [NotNullWhen(true)] out string? chunkKey)
        {
            chunkKey = null;

            string normalizedPath = NormalizePath(path, pathSeparator);
            string[] segments = normalizedPath.Split(
                '/',
                StringSplitOptions.RemoveEmptyEntries);

            if (TryParseVersionedPath(normalizedPath, segments, out string? versionedKey))
            {
                chunkKey = versionedKey;
                return true;
            }

            if (TryParseLegacyPath(normalizedPath, segments, out string? legacyKey))
            {
                chunkKey = legacyKey;
                return true;
            }

            return false;
        }

        private static bool TryParseVersionedPath(
            string normalizedPath,
            IReadOnlyList<string> segments,
            [NotNullWhen(true)] out string? chunkKey)
        {
            chunkKey = null;

            if (segments.Count != 6 || segments[0] != Version)
            {
                return false;
            }

            string? suffix = StripExtension(segments[5]);
            if (suffix is null)
            {
                return false;
            }

            string contentHash = segments[3] + segments[4] + suffix;
            if (!IsHash(contentHash))
            {
                return false;
            }

            string candidate = $"{Version}-{segments[1]}-{segments[2]}-{contentHash}";
            if (!IsSupportedChunkKey(candidate, normalizedPath))
            {
                return false;
            }

            chunkKey = candidate;
            return true;
        }

        private static bool TryParseLegacyPath(
            string normalizedPath,
            IReadOnlyList<string> segments,
            [NotNullWhen(true)] out string? chunkKey)
        {
            chunkKey = null;

            if (segments.Count != 3)
            {
                return false;
            }

            string? suffix = StripExtension(segments[2]);
            if (suffix is null)
            {
                return false;
            }

            string contentHash = segments[0] + segments[1] + suffix;
            if (!IsHash(contentHash))
            {
                return false;
            }

            if (!IsSupportedChunkKey(contentHash, normalizedPath))
            {
                return false;
            }

            chunkKey = contentHash;
            return true;
        }

        private static bool IsSupportedChunkKey(string candidate, string normalizedPath)
        {
            try
            {
                ChunkStorageHelpers.Parse(candidate);
                string expectedPath = NormalizePath(
                    ChunkStorageHelpers.GetStoragePath(candidate, '/'),
                    '/');

                return string.Equals(expectedPath, normalizedPath, StringComparison.Ordinal);
            }
            catch (Exception ex) when (ex is FormatException or NotSupportedException)
            {
                return false;
            }
        }

        private static string NormalizePath(string path, char pathSeparator)
        {
            return path
                .Replace(pathSeparator, '/')
                .Replace('\\', '/')
                .Trim('/');
        }

        private static string? StripExtension(string fileName)
        {
            string extension = "." + CompressionHelpers.Extension;
            if (!fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return fileName[..^extension.Length];
        }

        private static bool IsHash(string value)
        {
            if (value.Length != HashLength)
            {
                return false;
            }

            foreach (char character in value)
            {
                bool isHex =
                    character is >= '0' and <= '9' ||
                    character is >= 'a' and <= 'f' ||
                    character is >= 'A' and <= 'F';

                if (!isHex)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
