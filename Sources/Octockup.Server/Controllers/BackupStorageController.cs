using EasyExtensions;
using Octockup.Server.Models;
using Microsoft.AspNetCore.Mvc;
using Octockup.Server.Abstractions;
using Microsoft.AspNetCore.Authorization;
using EasyExtensions.AspNetCore.Extensions;
using Octockup.Server.Models.Database;

namespace Octockup.Server.Controllers
{
    [ApiController]
    public class BackupStorageController(
        IUserDataStorage _userDataStorage,
        IEnumerable<IBackupStorage> _backupStorages) : ControllerBase
    {
        [Authorize]
        [HttpPost("/api/v1/backups/storages/{backupStorageId:required}/create")]
        public IActionResult CreateBackupStorage([FromRoute] string backupStorageId, [FromBody] SaveModuleRequest request)
        {
            var foundStorage = _backupStorages.FirstOrDefault(x => x.Id == backupStorageId);
            if (foundStorage == null)
            {
                return this.ApiNotFound("Backup storage not found: " + backupStorageId);
            }
            SavedBackupModule newStorage = new()
            {
                Tag = request.Tag,
                CreatedAt = DateTime.UtcNow,
                Username = User.GetUserName(),
                Parameters = request.Parameters,
                BackupModuleId = foundStorage.Id,
            };
            _userDataStorage.AddSavedStorage(newStorage);
            return Ok(new { message = "Backup storage created successfully." });
        }

        [Authorize]
        [HttpPost("/api/v1/backups/storages/{backupStorageId:required}/directories")]
        public IActionResult GetBackupStorageDirectories([FromRoute] string backupStorageId, [FromBody] SaveModuleRequest request)
        {
            var foundStorage = _backupStorages.FirstOrDefault(x => x.Id == backupStorageId);
            if (foundStorage == null)
            {
                return this.ApiNotFound("Backup storage not found: " + backupStorageId);
            }

            foundStorage.SetParameters(request.Parameters);
            try
            {
                var result = foundStorage.GetDirectories(recursive: false);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return this.ApiBadRequest("Failed to connect to backup storage with provided parameters: " + ex.Message);
            }
        }

        [Authorize]
        [HttpPost("/api/v1/backups/storages/{backupStorageId:required}/test")]
        public async Task<IActionResult> TestBackupStorage([FromRoute] string backupStorageId, [FromBody] SaveModuleRequest request)
        {
            var foundStorage = _backupStorages.FirstOrDefault(x => x.Id == backupStorageId);
            if (foundStorage == null)
            {
                return this.ApiNotFound("Backup storage not found: " + backupStorageId);
            }

            foundStorage.SetParameters(request.Parameters);
            try
            {
                const string testFileName = "path_test_connection.txt";
                await foundStorage.UploadAsync(testFileName, Stream.Null);
                var result = foundStorage.GetFiles(recursive: false);
                if (!result.Any(x => x.Name == testFileName))
                {
                    return this.ApiBadRequest("Test file was not found after upload.");
                }
                await foundStorage.DeleteAsync(testFileName);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return this.ApiBadRequest("Failed to connect to backup storage with provided parameters: " + ex.Message);
            }
        }

        [Authorize]
        [HttpGet("/api/v1/backups/storages")]
        public IActionResult GetUserBackupStorages()
        {
            string username = User.GetUserName();
            var userStorages = _userDataStorage.GetUserData(username).SavedStorages;
            foreach (var userStorage in userStorages)
            {
                userStorage.Parameters.Clear();
            }
            return Ok(userStorages);
        }

        [Authorize]
        [HttpGet("/api/v1/backups/storages/available")]
        public IActionResult GetBackupStorages()
        {
            var mapped = _backupStorages.Select(x => new
            {
                name = x.Name,
                id = x.GetType().FullName,
                pathSeparator = x.PathSeparator,
                parameters = x.RequiredParameters,
            });
            return Ok(mapped);
        }
    }
}
