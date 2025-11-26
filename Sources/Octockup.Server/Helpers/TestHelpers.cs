using Microsoft.AspNetCore.Mvc;
using Octockup.Server.Abstractions;
using EasyExtensions.AspNetCore.Extensions;

namespace Octockup.Server.Helpers
{
    public static class TestHelpers
    {
        public static async Task<IActionResult> TestStorageAsync(this ControllerBase controller, IBackupStorage storage)
        {
            try
            {
                const string testFileName = "path_test_connection.txt";
                await storage.UploadAsync(testFileName, Stream.Null);
                var result = storage.GetFiles(recursive: false);
                if (!result.Any(x => x.Name == testFileName))
                {
                    return controller.ApiBadRequest("Test file was not found after upload.");
                }
                await storage.DeleteAsync(testFileName);
                return controller.Ok(result);
            }
            catch (Exception ex)
            {
                return controller.ApiBadRequest("Failed to connect to backup storage with provided parameters: " + ex.Message);
            }
        }
    }
}