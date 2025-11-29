// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using Microsoft.AspNetCore.Mvc;
using Octockup.Server.Controllers;
using Octockup.Server.Abstractions;
using EasyExtensions.AspNetCore.Extensions;
using System.Runtime.CompilerServices;

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
                source.GetDirectories(recursive: false);
                var files = source.GetFiles(recursive: false);
                if (!files.Any())
                {
                    return moduleController.ApiBadRequest("No files found in the backup source to test file stream retrieval.");
                }
                using var testStream = await source.GetFileStreamAsync(files.First());
                if (testStream == null || testStream.Length == 0)
                {
                    return moduleController.ApiBadRequest("Failed to retrieve a valid stream for the test file.");
                }
                return moduleController.Ok(files);
            }
            catch (Exception ex)
            {
                return moduleController.ApiBadRequest("Failed to connect to backup source with provided parameters: " + ex.Message);
            }
        }
    }
}
