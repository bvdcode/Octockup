
namespace Octockup.Server.Helpers
{
    public static class FileSystemHelpers
    {
        private const string DataFolder = "data";

        internal static string GetFilePath(string databaseFile)
        {
            if (!Directory.Exists(DataFolder))
            {
                Directory.CreateDirectory(DataFolder);
            }
            return Path.Combine(DataFolder, databaseFile);
        }
    }
}
