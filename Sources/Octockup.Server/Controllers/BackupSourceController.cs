using EasyExtensions;
using Octockup.Server.Models;
using Microsoft.AspNetCore.Mvc;
using Octockup.Server.Services;
using Octockup.Server.Abstractions;
using Microsoft.AspNetCore.Authorization;
using EasyExtensions.AspNetCore.Extensions;

namespace Octockup.Server.Controllers
{
    [ApiController]
    public class BackupSourceController(
        UserDataStorage _userDataStorage,
        IEnumerable<IBackupSource> _backupSources) : ControllerBase
    {
        [Authorize]
        [HttpPost("/api/v1/backups/sources/{backupSourceId:required}/create")]
        public IActionResult CreateBackupSource([FromRoute] string backupSourceId, [FromBody] CreateBackupRequest request)
        {
            var foundSource = _backupSources.FirstOrDefault(x => x.Id == backupSourceId);
            if (foundSource == null)
            {
                return this.ApiNotFound("Backup source not found: " + backupSourceId);
            }
            UserBackupSource newSource = new()
            {
                Tag = request.Tag,
                CreatedAt = DateTime.UtcNow,
                Username = User.GetUserName(),
                BackupSourceId = backupSourceId,
                Parameters = request.Parameters,
            };
            _userDataStorage.AddBackupSource(newSource);
            return Ok(new { message = "Backup source created successfully." });
        }

        [Authorize]
        [HttpPost("/api/v1/backups/sources/{backupSourceId:required}/directories")]
        public IActionResult GetBackupSourceDirectories([FromRoute] string backupSourceId, [FromBody] CreateBackupRequest request)
        {
            var foundSource = _backupSources.FirstOrDefault(x => x.Id == backupSourceId);
            if (foundSource == null)
            {
                return this.ApiNotFound("Backup source not found: " + backupSourceId);
            }

            foundSource.SetParameters(request.Parameters);
            try
            {
                var result = foundSource.GetDirectories(recursive: false);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return this.ApiBadRequest("Failed to connect to backup source with provided parameters: " + ex.Message);
            }
        }

        [Authorize]
        [HttpPost("/api/v1/backups/sources/{id:required}/test")]
        public IActionResult TestBackupSource([FromRoute] string id, [FromBody] CreateBackupRequest request)
        {
            var foundSource = _backupSources.FirstOrDefault(x => x.Id == id);
            if (foundSource == null)
            {
                return this.ApiNotFound("Backup source not found: " + id);
            }

            foundSource.SetParameters(request.Parameters);
            try
            {
                var result = foundSource.GetFiles(recursive: false);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return this.ApiBadRequest("Failed to connect to backup source with provided parameters: " + ex.Message);
            }
        }

        [Authorize]
        [HttpGet("/api/v1/backups/sources")]
        public IActionResult GetUserBackupSources()
        {
            string username = User.GetUserName();
            var userSources = _userDataStorage.GetUserData(username).BackupSources;
            foreach (var userSource in userSources)
            {
                userSource.Parameters.Clear();
            }
            return Ok(userSources);
        }

        [Authorize]
        [HttpGet("/api/v1/backups/sources/available")]
        public IActionResult GetBackupSources()
        {
            var mapped = _backupSources.Select(x => new
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
