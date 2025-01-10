using AutoMapper;
using EasyExtensions;
using Gridify;
using Gridify.EntityFramework;
using Microsoft.AspNetCore.Mvc;
using Octockup.Server.Database;
using Octockup.Server.Models.Dto;

namespace Octockup.Server.Controllers
{
    [ApiController]
    public class SnapshotController(AppDbContext _dbContext, IMapper _mapper) : ControllerBase
    {
        [HttpGet]
        [Route(Routes.Version + "/snapshots")]
        public async Task<IActionResult> GetSnapshots([FromQuery] GridifyQuery query)
        {
            int userId = User.GetId();
            var snapshots = await _dbContext.BackupSnapshots
                .Where(x => x.BackupTask.UserId == userId)
                .GridifyAsync(query);
            Response.Headers.Append("X-Total-Count", snapshots.Count.ToString());
            var mapped = _mapper.Map<IEnumerable<BackupSnapshotDto>>(snapshots.Data);
            return Ok(mapped);
        }
    }
}
