using Mapster;
using EasyExtensions;
using Octockup.Server.Database;
using Microsoft.AspNetCore.Mvc;
using Octockup.Server.Models.Dto;
using Octockup.Server.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Octockup.Server.Models.Requests;
using Microsoft.AspNetCore.Authorization;
using EasyExtensions.AspNetCore.Extensions;

namespace Octockup.Server.Controllers
{
    [ApiController]
    public class ScheduleController(AppDbContext _dbContext) : ControllerBase
    {
        [Authorize]
        [HttpGet("/api/v1/schedules")]
        public async Task<IEnumerable<ScheduleDto>> GetUserSchedules()
        {
            var userId = User.GetUserId();
            return await _dbContext.Schedules
                .AsNoTracking()
                .Include(s => s.Backup)
                    .ThenInclude(b => b.Source)
                .Include(s => s.Backup)
                    .ThenInclude(b => b.Storage)
                .Where(s => s.Backup.Source.UserId == userId)
                .ProjectToType<ScheduleDto>()
                .ToListAsync();
        }

        [Authorize]
        [HttpPost("/api/v1/schedules")]
        public async Task<IActionResult> CreateSchedule([FromBody] CreateScheduleRequest request)
        {
            var userId = User.GetUserId();
            var backup = await _dbContext.Backups
                .Include(b => b.Source)
                .FirstOrDefaultAsync(b => b.Id == request.BackupId && b.Source.UserId == userId);
            if (backup == null)
            {
                return this.ApiNotFound("Backup not found: " + request.BackupId);
            }

            Schedule schedule = new()
            {
                BackupId = backup.Id,
                StartAt = request.StartAt,
                Interval = request.IntervalMinutes.HasValue ? TimeSpan.FromMinutes(request.IntervalMinutes.Value) : null,
                Status = BackupStatus.Created,
            };
            await _dbContext.Schedules.AddAsync(schedule);
            await _dbContext.SaveChangesAsync();
            return Ok(new { message = "Schedule created successfully." });
        }
    }
}
