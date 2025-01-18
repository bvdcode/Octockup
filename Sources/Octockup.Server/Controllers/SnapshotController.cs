using Quartz;
using Gridify;
using AutoMapper;
using EasyExtensions;
using Octockup.Server.Jobs;
using Gridify.EntityFramework;
using Microsoft.AspNetCore.Mvc;
using Octockup.Server.Database;
using Octockup.Server.Models.Dto;
using Microsoft.EntityFrameworkCore;
using EasyExtensions.Quartz.Extensions;
using Microsoft.AspNetCore.Authorization;

namespace Octockup.Server.Controllers
{
    [ApiController]
    public class SnapshotController(AppDbContext _dbContext, IMapper _mapper,
        ISchedulerFactory _scheduler) : ControllerBase
    {
        [Authorize]
        [HttpDelete]
        [Route(Routes.Version + "/snapshots/{snapshot}")]
        public async Task<IActionResult> DeleteSnapshotAsync([FromRoute] int snapshot)
        {
            int userId = User.GetId();
            var found = await _dbContext.BackupSnapshots
                .FirstOrDefaultAsync(x => x.Id == snapshot && x.BackupTask.UserId == userId);
            if (found == null)
            {
                return NotFound();
            }
            found.IsDeleted = true;
            await _dbContext.SaveChangesAsync();
            await _scheduler.TriggerJobAsync<CleanupJob>();
            return Ok();
        }

        [Authorize]
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
