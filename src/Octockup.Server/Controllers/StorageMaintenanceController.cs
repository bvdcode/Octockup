// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions;
using EasyExtensions.AspNetCore.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Octockup.Server.Models.Dto;
using Octockup.Server.Services;

namespace Octockup.Server.Controllers
{
    [ApiController]
    public class StorageMaintenanceController(
        StorageMaintenanceService _storageMaintenanceService) : ControllerBase
    {
        [Authorize]
        [HttpGet("/api/v1/storage-maintenance")]
        public async Task<IActionResult> GetStorageMaintenance(
            CancellationToken cancellationToken)
        {
            IReadOnlyList<StorageMaintenanceSummaryDto> summaries =
                await _storageMaintenanceService.GetSummariesAsync(
                    User.GetUserId(),
                    cancellationToken);

            return Ok(summaries);
        }

        [Authorize]
        [HttpGet("/api/v1/storage-maintenance/storages/{storageId:guid}/stats")]
        public async Task<IActionResult> GetStorageMaintenanceStats(
            [FromRoute] Guid storageId,
            CancellationToken cancellationToken)
        {
            try
            {
                StorageMaintenanceSummaryDto summary =
                    await _storageMaintenanceService.GetStorageStatsAsync(
                        User.GetUserId(),
                        storageId,
                        cancellationToken);

                return Ok(summary);
            }
            catch (InvalidOperationException ex)
            {
                return this.ApiBadRequest(ex.Message);
            }
        }

        [Authorize]
        [HttpGet("/api/v1/storage-maintenance/jobs")]
        public async Task<IActionResult> GetStorageCleanupJobs()
        {
            IReadOnlyList<StorageCleanupJobDto> jobs =
                await _storageMaintenanceService.GetJobsAsync(
                    User.GetUserId(),
                    HttpContext.RequestAborted);

            return Ok(jobs);
        }

        [Authorize]
        [HttpPost("/api/v1/storage-maintenance/storages/{storageId:guid}/cleanup")]
        public Task<IActionResult> StartStorageCleanup(
            [FromRoute] Guid storageId,
            CancellationToken cancellationToken)
        {
            return StartStorageCleanupCore(storageId, cancellationToken);
        }

        [Authorize]
        [HttpPost("/api/v1/storages/{storageId:guid}/garbage-collect")]
        public Task<IActionResult> StartStorageCleanupLegacy(
            [FromRoute] Guid storageId,
            CancellationToken cancellationToken)
        {
            return StartStorageCleanupCore(storageId, cancellationToken);
        }

        [Authorize]
        [HttpPost("/api/v1/storage-maintenance/jobs/{jobId:guid}/cancel")]
        public async Task<IActionResult> CancelStorageCleanup([FromRoute] Guid jobId)
        {
            bool canceled = await _storageMaintenanceService.CancelCleanupAsync(
                User.GetUserId(),
                jobId,
                HttpContext.RequestAborted);

            if (!canceled)
            {
                return this.ApiNotFound("Cleanup job not found: " + jobId);
            }

            return Ok(new { message = "Cleanup cancellation requested." });
        }

        private async Task<IActionResult> StartStorageCleanupCore(
            Guid storageId,
            CancellationToken cancellationToken)
        {
            try
            {
                StorageCleanupJobDto job = await _storageMaintenanceService
                    .StartCleanupAsync(
                        User.GetUserId(),
                        storageId,
                        cancellationToken);

                return Ok(job);
            }
            catch (InvalidOperationException ex)
            {
                return this.ApiBadRequest(ex.Message);
            }
        }
    }
}
