using EasyExtensions.Models.Enums;

namespace Octockup.Server.Helpers
{
    public static class CompressionHelpers
    {
        /// <summary>
        /// Specifies the compression algorithm to use.
        /// </summary>
        public const CompressionAlgorithm Algorithm = CompressionAlgorithm.Zstd;
        public const string Extension = "oct";

        public static Stream CreateCompressionStream(Stream compressedStream)
        {
            return new ZstdSharp.CompressionStream(compressedStream, level: 3, leaveOpen: true);
        }

        public static Stream CreateDecompressionStream(Stream decrypted, bool leaveOpen = true)
        {
            return new ZstdSharp.DecompressionStream(decrypted, leaveOpen: leaveOpen);
        }

        internal static bool ShouldCompressChunk(string fileNameOrPath, long chunkLength)
        {
            if (chunkLength < 1024)
            {
                return false;
            }
            string ext = Path.GetExtension(fileNameOrPath).ToLowerInvariant();
            string[] skip =
            [
                ".zip", ".rar", ".7z", ".gz", ".bz2", ".xz",
                ".jpg", ".jpeg", ".gif", ".png", ".webp", ".avif", ".heic",
                ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".webm",
                ".mp3", ".aac", ".m4a", ".ogg", ".opus", ".flac",
            ];
            return !skip.Contains(ext);
        }
    }
}
