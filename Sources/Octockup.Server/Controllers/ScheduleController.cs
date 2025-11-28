using Quartz;
using Mapster;
using EasyExtensions;
using Octockup.Server.Jobs;
using Octockup.Server.Database;
using Microsoft.AspNetCore.Mvc;
using Octockup.Server.Models.Dto;
using Octockup.Server.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Octockup.Server.Models.Requests;
using EasyExtensions.Quartz.Extensions;
using Microsoft.AspNetCore.Authorization;
using EasyExtensions.AspNetCore.Extensions;

namespace Octockup.Server.Controllers
{
    [ApiController]
    public class ScheduleController(
        AppDbContext _dbContext,
        ISchedulerFactory _scheduler) : ControllerBase
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
        [HttpPost("/api/v1/schedules/{scheduleId:guid}/reset-error")]
        public async Task<IActionResult> Reschedule(Guid scheduleId)
        {
            var userId = User.GetUserId();
            var schedule = await _dbContext.Schedules
                .Include(s => s.Backup)
                    .ThenInclude(b => b.Source)
                .FirstOrDefaultAsync(s => s.Id == scheduleId && s.Backup.Source.UserId == userId);
            if (schedule == null)
            {
                return this.ApiNotFound("Schedule not found: " + scheduleId);
            }
            if (schedule.Status != ScheduleStatus.Failed)
            {
                return this.ApiBadRequest("Only schedules in Error status can be reset.");
            }
            schedule.Status = ScheduleStatus.Created;
            schedule.ErrorMessage = null;
            schedule.FinishedAt = null;
            await _dbContext.SaveChangesAsync();
            await _scheduler.TriggerJobAsync<ExecuteBackupJob>();
            return Ok(new { message = "Schedule error reset successfully." });
        }

        [Authorize]
        [HttpPost("/api/v1/schedules/{scheduleId:guid}/cancel")]
        public async Task<IActionResult> CancelSchedule(Guid scheduleId)
        {
            ExecuteBackupJob.StopRunningBackup(scheduleId);
            return Ok(new { message = "Schedule cancellation requested." });
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
            if (request.StartAt.Kind != DateTimeKind.Utc)
            {
                return this.ApiBadRequest("StartAt must be in UTC.");
            }

            Schedule schedule = new()
            {
                BackupId = backup.Id,
                StartAt = request.StartAt,
                Status = ScheduleStatus.Created,
                Interval = request.IntervalMinutes.HasValue ? TimeSpan.FromMinutes(request.IntervalMinutes.Value) : null,
            };

            await _dbContext.Schedules.AddAsync(schedule);
            await _dbContext.SaveChangesAsync();
            await _scheduler.TriggerJobAsync<ExecuteBackupJob>();
            return Ok(new { message = "Schedule created successfully." });
        }

        [Authorize]
        [HttpDelete("/api/v1/schedules/{scheduleId}")]
        public async Task<IActionResult> DeleteSchedule(Guid scheduleId)
        {
            var userId = User.GetUserId();
            var schedule = await _dbContext.Schedules
                .Include(s => s.Backup)
                    .ThenInclude(b => b.Source)
                .FirstOrDefaultAsync(s => s.Id == scheduleId && s.Backup.Source.UserId == userId);

            if (schedule == null)
            {
                return this.ApiNotFound("Schedule not found: " + scheduleId);
            }

            _dbContext.Schedules.Remove(schedule);
            await _dbContext.SaveChangesAsync();
            return Ok(new { message = "Schedule deleted successfully." });
        }
    }
}
