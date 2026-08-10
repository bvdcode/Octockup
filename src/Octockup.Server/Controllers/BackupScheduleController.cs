// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions;
using EasyExtensions.AspNetCore.Extensions;
using EasyExtensions.Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Octockup.Server.Handlers.Scheduling;
using Octockup.Server.Models.Enums;
using Octockup.Server.Models.Requests;

namespace Octockup.Server.Controllers
{
    [ApiController]
    [Route("/api/v1/backups/{backupId:guid}")]
    public class BackupScheduleController(IMediator mediator) : ControllerBase
    {
        [Authorize]
        [HttpPost("run")]
        public async Task<IActionResult> RunNowAsync(
            Guid backupId,
            CancellationToken cancellationToken)
        {
            Guid? scheduleId = await mediator.Send(
                new ManageBackupScheduleCommand(
                    User.GetUserId(),
                    backupId,
                    BackupScheduleAction.RunNow),
                cancellationToken);
            return Ok(new { scheduleId });
        }

        [Authorize]
        [HttpPut("schedule")]
        public async Task<IActionResult> SetScheduleAsync(
            Guid backupId,
            [FromBody] SetBackupScheduleRequest request,
            CancellationToken cancellationToken)
        {
            Guid? scheduleId = await mediator.Send(
                new ManageBackupScheduleCommand(
                    User.GetUserId(),
                    backupId,
                    BackupScheduleAction.SetInterval,
                    request.IntervalMinutes),
                cancellationToken);
            return Ok(new { scheduleId });
        }

        [Authorize]
        [HttpDelete("schedule")]
        public async Task<IActionResult> DisableScheduleAsync(
            Guid backupId,
            CancellationToken cancellationToken)
        {
            await mediator.Send(
                new ManageBackupScheduleCommand(
                    User.GetUserId(),
                    backupId,
                    BackupScheduleAction.Disable),
                cancellationToken);
            return NoContent();
        }
    }
}
