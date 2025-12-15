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

        public const string Extension = "zst";
        public const string LegacyExtension = "br";

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
    }
}
