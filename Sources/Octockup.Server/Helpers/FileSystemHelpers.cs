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
            if (filename.Contains(Path.DirectorySeparatorChar))
            {
                var path = Path.GetDirectoryName(filename) ?? string.Empty;
                string dirPath = Path.Combine(DataFolder, path);
                if (!string.IsNullOrEmpty(path) && !Directory.Exists(dirPath))
                {
                    Directory.CreateDirectory(dirPath);
                }
            }
            return Path.Combine(DataFolder, filename);
        }
    }
}
