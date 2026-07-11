// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Octockup.Server.Models.Dto;
using Octockup.Server.Services;

namespace Octockup.Server.Controllers
{
    [ApiController]
    [Authorize]
    [Route("/api/v1/download-tickets")]
    public class DownloadTicketController(DownloadTicketService _downloadTickets) : ControllerBase
    {
        [HttpPost("snapshot-archive-jobs/{jobId:guid}")]
        public async Task<IActionResult> CreateSnapshotArchiveJobTicket(
            [FromRoute] Guid jobId,
            CancellationToken cancellationToken)
        {
            DownloadTicketDto? ticket = await _downloadTickets
                .CreateSnapshotArchiveJobAsync(User.GetUserId(), jobId, cancellationToken);
            return ticket is null ? NotFound() : Ok(ticket);
        }

        [HttpPost("snapshots/{snapshotId:guid}/files/{fileId:guid}")]
        public async Task<IActionResult> CreateSnapshotFileTicket(
            [FromRoute] Guid snapshotId,
            [FromRoute] Guid fileId,
            CancellationToken cancellationToken)
        {
            DownloadTicketDto? ticket = await _downloadTickets
                .CreateSnapshotFileAsync(User.GetUserId(), snapshotId, fileId, cancellationToken);
            return ticket is null ? NotFound() : Ok(ticket);
        }

        [HttpPost("server-backup")]
        public async Task<DownloadTicketDto> CreateServerBackupTicket(
            [FromQuery] bool includeFiles,
            CancellationToken cancellationToken)
        {
            return await _downloadTickets
                .CreateServerBackupAsync(User.GetUserId(), includeFiles, cancellationToken);
        }
    }
}
