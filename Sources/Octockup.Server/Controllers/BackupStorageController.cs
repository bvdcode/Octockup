using Microsoft.AspNetCore.Mvc;
using Octockup.Server.Abstractions;
using Microsoft.AspNetCore.Authorization;

namespace Octockup.Server.Controllers
{
    [ApiController]
    public class BackupStorageController(IEnumerable<IBackupStorage> _backupStorages) : ControllerBase
    {
        [Authorize]
        [HttpGet("/api/v1/backups/storages")]
        public IActionResult GetBackupStorages()
        {
            var mapped = _backupStorages.Select(x => new
            {
                name = x.Name,
                id = x.GetType().FullName,
                parameters = x.RequiredParameters,
            });
            return Ok(mapped);
        }
    }
}
