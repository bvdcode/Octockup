// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using Microsoft.AspNetCore.Mvc;
using Octockup.Server.Controllers;
using Octockup.Server.Abstractions;
using EasyExtensions.AspNetCore.Extensions;

namespace Octockup.Server.Helpers
{
    public static class TestHelpers
    {
        public static async Task<IActionResult> TestStorageAsync(ControllerBase controller, IBackupStorage storage)
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

        public static async Task<IActionResult> TestSourceAsync(ModuleController moduleController, IBackupSource source)
        {
            try
            {
                _ = source.GetDirectories(recursive: false).Take(1).ToList();

                const int maxEnumeratedFiles = 100;
                const int maxTestedFiles = 10;

                var candidates = source
                    .GetFiles(recursive: true)
                    .Take(maxEnumeratedFiles)
                    .ToList();

                if (candidates.Count == 0)
                {
                    return moduleController.ApiBadRequest(
                        "No files found in the backup source to test file stream retrieval.");
                }

                int tested = 0;
                string? lastError = null;
                foreach (var file in candidates)
                {
                    if (tested >= maxTestedFiles)
                    {
                        break;
                    }

                    Stream? testStream = null;
                    try
                    {
                        testStream = await source.GetFileStreamAsync(file);
                    }
                    catch (Exception ex)
                    {
                        lastError = ex.Message;
                        continue;
                    }

                    if (testStream == null || testStream == Stream.Null)
                    {
                        continue;
                    }

                    try
                    {
                        var buffer = new byte[1024];
                        int read = 0;

                        if (testStream.CanRead)
                        {
                            read = await testStream.ReadAsync(buffer.AsMemory(0, buffer.Length));
                        }

                        if (read >= 0)
                        {
                            return moduleController.Ok(candidates);
                        }
                    }
                    finally
                    {
                        testStream.Dispose();
                    }

                    tested++;
                }

                return moduleController.ApiBadRequest(
                    "Failed to retrieve a readable stream from the backup source in the first "
                    + maxTestedFiles + " files: " + (lastError ?? "no files could be read."));
            }
            catch (Exception ex)
            {
                return moduleController.ApiBadRequest(
                    "Failed to connect to backup source with provided parameters: " + ex.Message);
            }
        }

    }
}
