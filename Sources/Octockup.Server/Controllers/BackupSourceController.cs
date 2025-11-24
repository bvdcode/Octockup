using Octockup.Server.Models;
using Microsoft.AspNetCore.Mvc;
using Octockup.Server.Abstractions;
using Microsoft.AspNetCore.Authorization;
using EasyExtensions.AspNetCore.Extensions;

namespace Octockup.Server.Controllers
{
    [ApiController]
    public class BackupSourceController(IEnumerable<IBackupSource> _backupSources) : ControllerBase
    {
        [Authorize]
        [HttpPost("/api/v1/backups/sources/{id:required}/directories")]
        public IActionResult GetBackupSourceDirectories([FromRoute] string id, [FromBody] CreateBackupRequest request)
        {
            var foundSource = _backupSources.FirstOrDefault(x => x.Id == id);
            if (foundSource == null)
            {
                return this.ApiNotFound("Backup source not found: " + id);
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
