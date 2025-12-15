using EasyExtensions.Models.Enums;
using System.IO.Compression;

namespace Octockup.Server.Helpers
{
    public static class CompressionHelpers
    {
        /// <summary>
        /// Specifies the compression algorithm to use.
        /// </summary>
        public const CompressionAlgorithm Algorithm = CompressionAlgorithm.Zstd;

        public const string AppExtension = "oct";
        public const string CompressionExtension = "zst";
        public const string LegacyCompressionExtension = "br";

        public static Stream CreateCompressionStream(Stream compressedStream)
        {
            return new ZstdSharp.CompressionStream(compressedStream, level: 3, leaveOpen: true);
        }

        public static Stream CreateDecompressionStream(Stream decrypted, bool leaveOpen = true)
        {
            return new ZstdSharp.DecompressionStream(decrypted, leaveOpen: leaveOpen);
        }

        internal static BrotliStream CreateLegacyDecompressionStream(Stream decrypted, bool leaveOpen)
        {
            return new BrotliStream(
                decrypted,
                CompressionMode.Decompress,
                leaveOpen: leaveOpen
            );
        }

        internal static CompressionAlgorithm DetectAlgorithmFromPath(string path)
        {
            if (path.EndsWith($"{CompressionExtension}.{AppExtension}"))
            {
                return CompressionAlgorithm.Zstd;
            }
            else if (path.EndsWith($"{LegacyCompressionExtension}.{AppExtension}"))
            {
                return CompressionAlgorithm.Brotli;
            }
            else if (path.EndsWith($".{AppExtension}"))
            {
                return CompressionAlgorithm.None;
            }
            else
            {
                throw new InvalidDataException("Unknown compression format for path: " + path);
            }
        }

        internal static bool ShouldCompressChunk(string fileNameOrPath, long chunkLength)
        {
            bool isTooSmall = chunkLength < 1024; // 1 KB
            if (isTooSmall)
            {
                return false;
            }
            string[] extensionsNotToCompress =
            [
                ".zip", ".rar", ".7z", ".gz", ".bz2", ".xz", // Compressed archives
                ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tiff", // Images
                ".mp4", ".mkv", ".avi", ".mov", ".wmv", // Videos
                ".mp3", ".flac", ".wav", ".aac", // Audio
                ".pdf", // PDF documents
            ];
            string fileExtension = Path.GetExtension(fileNameOrPath).ToLowerInvariant();
            return !extensionsNotToCompress.Contains(fileExtension);
        }
    }
}
