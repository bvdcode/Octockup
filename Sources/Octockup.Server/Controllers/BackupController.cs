﻿using Quartz;
using MediatR;
using Gridify;
using AutoMapper;
using EasyExtensions;
using Octockup.Server.Jobs;
using Octockup.Server.Models;
using Gridify.EntityFramework;
using Octockup.Server.Services;
using Microsoft.AspNetCore.Mvc;
using Octockup.Server.Database;
using Octockup.Server.Extensions;
using Octockup.Server.Models.Dto;
using Octockup.Server.Models.Enums;
using Microsoft.EntityFrameworkCore;
using EasyExtensions.Quartz.Extensions;
using Octockup.Server.Providers.Storage;
using Microsoft.AspNetCore.Authorization;

namespace Octockup.Server.Controllers
{
    [ApiController]
    [Route(Routes.Version + "/backups")]
    public class BackupController(IEnumerable<IStorageProvider> _storageProviders, JobCancellationService _jobCancellationService,
        AppDbContext _dbContext, IMapper _mapper, IMediator _mediator, ISchedulerFactory _scheduler) : ControllerBase
    {
        [Authorize]
        [HttpPatch("trigger-job")]
        public async Task<IActionResult> TriggerJobAsync()
        {
            await _scheduler.TriggerJobAsync<HandleBackupJob>();
            return Ok();
        }

        [Authorize]
        [HttpDelete("{backupTask}")]
        public async Task<IActionResult> DeleteBackupAsync([FromRoute] int backupTask)
        {
            int userId = User.GetId();
            var found = await _dbContext.BackupTasks.FirstOrDefaultAsync(x => x.Id == backupTask && x.UserId == userId);
            if (found == null)
            {
                return NotFound();
            }
            found.IsDeleted = true;
            found.IsEnabled = false;
            await _dbContext.SaveChangesAsync();
            _jobCancellationService.Cancel(found.Id);
            return Ok();
        }

        [Authorize]
        [HttpPatch("{backupTask}/stop")]
        public async Task<IActionResult> StopBackupAsync([FromRoute] int backupTask)
        {
            int userId = User.GetId();
            var found = await _dbContext.BackupTasks.FirstOrDefaultAsync(x => x.Id == backupTask && x.UserId == userId);
            if (found == null)
            {
                return NotFound();
            }
            found.IsEnabled = false;
            await _dbContext.SaveChangesAsync();
            if (found.Status != BackupTaskStatus.Running)
            {
                return Ok();
            }
            _jobCancellationService.Cancel(found.Id);
            return Ok();
        }

        [Authorize]
        [HttpPatch("{backupTask}/start")]
        public async Task<IActionResult> TriggerBackupAsync([FromRoute] int backupTask)
        {
            int userId = User.GetId();
            var found = await _dbContext.BackupTasks.FirstOrDefaultAsync(x => x.Id == backupTask && x.UserId == userId);
            if (found == null)
            {
                return NotFound();
            }
            found.IsEnabled = true;
            await _dbContext.SaveChangesAsync();
            if (found.Status == BackupTaskStatus.Running)
            {
                return Ok();
            }
            found.ForceRun = true;
            await _dbContext.SaveChangesAsync();
            await _scheduler.TriggerJobAsync<HandleBackupJob>();
            return Ok();
        }

        [Authorize]
        [HttpPost("create")]
        public async Task<IActionResult> CreateBackupAsync([FromBody] CreateBackupRequest request)
        {
            await _mediator.Send(request);
            await _scheduler.TriggerJobAsync<HandleBackupJob>();
            return Ok();
        }

        [Authorize]
        [HttpGet("list")]
        public async Task<IEnumerable<BackupTaskDto>> GetStatusAsync([FromQuery] GridifyQuery query)
        {
            int userId = User.GetId();
            var result = await _dbContext
                .BackupTasks
                .Where(x => x.UserId == userId && !x.IsDeleted)
                .GridifyAsync(query);
            Response.Headers.Append("X-Total-Count", result.Count.ToString());
            return _mapper.Map<IEnumerable<BackupTaskDto>>(result.Data);
        }

        [Authorize]
        [HttpGet("providers")]
        public IEnumerable<StorageProviderInfo> GetProviders()
        {
            return _storageProviders.Select(p => new StorageProviderInfo()
            {
                Name = p.Name,
                Class = p.GetClassName(),
                Parameters = p.GetParametersKeys()
            });
        }
    }
}
