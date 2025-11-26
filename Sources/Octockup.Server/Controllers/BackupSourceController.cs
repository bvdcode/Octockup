using EasyExtensions;
using Microsoft.AspNetCore.Mvc;
using Octockup.Server.Abstractions;
using Microsoft.AspNetCore.Authorization;
using EasyExtensions.AspNetCore.Extensions;
using Octockup.Server.Database;
using Octockup.Server.Models.Requests;

namespace Octockup.Server.Controllers
{
    [ApiController]
    public class BackupSourceController(
        IUserDataStorage _userDataStorage,
        IEnumerable<IBackupSource> _backupSources) : ControllerBase
    {
        [Authorize]
        [HttpDelete("/api/v1/backups/sources/{savedSourceId:guid}")]
        public IActionResult DeleteUserBackupSource([FromRoute] Guid savedSourceId)
        {
            string username = User.GetUserName();
            var userData = _userDataStorage.GetUser(username);
            var foundSource = userData.SavedSources.FirstOrDefault(x => x.Id == savedSourceId);
            if (foundSource == null)
            {
                return this.ApiNotFound("Backup source not found: " + savedSourceId);
            }
            _userDataStorage.RemoveSavedSource(foundSource);
            return Ok(new { message = "Backup source deleted successfully." });
        }

        [Authorize]
        [HttpPost("/api/v1/backups/sources/{backupSourceId:required}/create")]
        public IActionResult CreateBackupSource([FromRoute] string backupSourceId, [FromBody] SaveModuleRequest request)
        {
            var foundSource = _backupSources.FirstOrDefault(x => x.Id == backupSourceId);
            if (foundSource == null)
            {
                return this.ApiNotFound("Backup source not found: " + backupSourceId);
            }
            var user = _userDataStorage.GetUser(User.GetUserName());
            Module newSource = new()
            {
                UserId = user.Id,
                Tag = request.Tag,
                Parameters = request.Parameters,
                BackupModuleId = foundSource.Id,
            };
            _userDataStorage.AddSavedSource(newSource);
            return Ok(new { message = "Backup source created successfully." });
        }

        [Authorize]
        [HttpPost("/api/v1/backups/sources/{backupSourceId:required}/directories")]
        public IActionResult GetBackupSourceDirectories([FromRoute] string backupSourceId, [FromBody] SaveModuleRequest request)
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
        public IActionResult TestBackupSource([FromRoute] string id, [FromBody] SaveModuleRequest request)
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
            var userSources = _userDataStorage.GetUser(username).SavedSources;
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
