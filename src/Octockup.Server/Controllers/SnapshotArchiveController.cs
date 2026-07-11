// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Octockup.Server.Archives;
using Octockup.Server.Models.Dto;
using Octockup.Server.Models.Requests;
using Octockup.Server.Models.Results;
using Octockup.Server.Services;

namespace Octockup.Server.Controllers
{
    [ApiController]
    public class SnapshotArchiveController(
        SnapshotArchiveJobService _jobs,
        SnapshotArchiveExecutionService _execution,
        DownloadTicketService _downloadTickets) : ControllerBase
    {
        [Authorize]
        [HttpPost("/api/v1/snapshots/{snapshotId:guid}/archive-jobs")]
        public async Task<IActionResult> Start(
            [FromRoute] Guid snapshotId,
            CancellationToken cancellationToken)
        {
            SnapshotArchiveJobDto? job = await _jobs.StartAsync(
                User.GetUserId(),
                snapshotId,
                cancellationToken).ConfigureAwait(false);
            return job is null ? NotFound() : Ok(job);
        }

        [Authorize]
        [HttpPost("/api/v1/snapshot-archive-jobs/query")]
        public async Task<IActionResult> GetForSnapshots(
            [FromBody] SnapshotArchiveJobQueryRequest request,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<SnapshotArchiveJobDto> jobs = await _jobs.GetForSnapshotsAsync(
                User.GetUserId(),
                request.SnapshotIds,
                cancellationToken).ConfigureAwait(false);
            return Ok(jobs);
        }

        [Authorize]
        [HttpPost("/api/v1/snapshot-archive-jobs/{jobId:guid}/cancel")]
        public async Task<IActionResult> Cancel(
            [FromRoute] Guid jobId,
            CancellationToken cancellationToken)
        {
            bool canceled = await _jobs.CancelAsync(
                User.GetUserId(),
                jobId,
                cancellationToken).ConfigureAwait(false);
            return canceled ? NoContent() : NotFound();
        }

        [AllowAnonymous]
        [HttpGet("/api/v1/snapshot-archive-jobs/{jobId:guid}/download")]
        public async Task<IActionResult> Download(
            [FromRoute] Guid jobId,
            [FromQuery] string? ticket,
            CancellationToken cancellationToken)
        {
            DownloadTicketGrant? grant = await _downloadTickets
                .ConsumeSnapshotArchiveJobAsync(ticket, jobId, cancellationToken)
                .ConfigureAwait(false);
            if (grant is null)
            {
                return Unauthorized();
            }

            SnapshotArchiveRunContext? context = await _execution.BeginAsync(
                grant.UserId,
                jobId,
                cancellationToken).ConfigureAwait(false);
            if (context is null)
            {
                return Conflict();
            }

            HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
            Response.ContentType = "application/zip";
            Response.Headers.CacheControl = "no-store";
            Response.Headers.ContentDisposition = SnapshotArchiveFileName
                .CreateContentDisposition(context.FileName);
            Response.Headers.XContentTypeOptions = "nosniff";

            await _execution.ExecuteAsync(
                context,
                Response.Body,
                HttpContext.RequestAborted).ConfigureAwait(false);
            return new EmptyResult();
        }
    }
}
