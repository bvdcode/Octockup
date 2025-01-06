using EasyExtensions;

namespace Octockup.Server.Helpers
{
    public static class FileSystemHelpers
    {
        private const string DataFolder = "data";

        internal static string CalculateSHA512(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            return stream.SHA512();
        }

        internal static string GetFilePath(string filename)
        {
            if (!Directory.Exists(DataFolder))
            {
                Directory.CreateDirectory(DataFolder);
            }
            return Path.Combine(DataFolder, filename);
        }
    }
}
