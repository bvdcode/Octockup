using Mapster;
using Microsoft.AspNetCore.Mvc;
using Octockup.Server.Database;
using Octockup.Server.Models.Dto;
using Microsoft.AspNetCore.Authorization;

namespace Octockup.Server.Controllers
{
    [ApiController]
    public class SnapshotController(AppDbContext _dbContext) : ControllerBase
    {
        [Authorize]
        [HttpGet("/api/v1/snapshots")]
        public IActionResult GetSnapshots([FromQuery] Guid backupId)
        {
            var snapshots = _dbContext.Snapshots
                .Where(s => s.BackupId == backupId)
                .OrderBy(s => s.CreatedAt)
                .ToList();

            List<SnapshotDto> result = [];
            foreach (var snapshot in snapshots)
            {
                var dto = snapshot.Adapt<SnapshotDto>();
                dto.FilesCount = _dbContext.SnapshotFiles
                    .Count(sf => sf.SnapshotId == snapshot.Id);
                dto.TotalSize = _dbContext.SnapshotFiles
                    .Where(sf => sf.SnapshotId == snapshot.Id)
                    .Sum(sf => (long?)sf.Size) ?? 0;
                result.Add(dto);
            }
            return Ok(result);
        }
    }
}
