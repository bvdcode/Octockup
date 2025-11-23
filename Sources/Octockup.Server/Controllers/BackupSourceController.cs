using Microsoft.AspNetCore.Mvc;
using Octockup.Server.Abstractions;
using Microsoft.AspNetCore.Authorization;

namespace Octockup.Server.Controllers
{
    [ApiController]
    public class BackupSourceController(IEnumerable<IBackupSource> _backupSources) : ControllerBase
    {
        [Authorize]
        [HttpGet("/api/v1/backups/sources")]
        public IActionResult GetBackupSources()
        {
            var mapped = _backupSources.Select(x => new
            {
                name = x.Name,
                id = x.GetType().FullName,
                parameters = x.RequiredParameters,
            });
            return Ok(mapped);
        }
    }
}
