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
                var errors = new List<string>();
                
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
                        
                        if (testStream == null || testStream == Stream.Null)
                        {
                            errors.Add($"File '{file.Path}': returned null or empty stream");
                            tested++;
                            continue;
                        }

                        var buffer = new byte[1024];
                        int read = 0;

                        if (testStream.CanRead)
                        {
                            read = await testStream.ReadAsync(buffer.AsMemory(0, buffer.Length));
                        }
                        else
                        {
                            errors.Add($"File '{file.Path}': stream is not readable");
                            tested++;
                            continue;
                        }

                        if (read >= 0)
                        {
                            return moduleController.Ok(candidates);
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"File '{file.Path}': {ex.Message}");
                        tested++;
                        continue;
                    }
                    finally
                    {
                        testStream?.Dispose();
                    }

                    tested++;
                }

                var errorMessage = errors.Count > 0 
                    ? string.Join("; ", errors.Take(3))
                    : "no files could be read";

                return moduleController.ApiBadRequest(
                    $"Failed to retrieve a readable stream from the backup source in the first {maxTestedFiles} files: {errorMessage}");
            }
            catch (Exception ex)
            {
                return moduleController.ApiBadRequest(
                    "Failed to connect to backup source with provided parameters: " + ex.Message);
            }
        }

    }
}
