// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Octockup.Server.Extensions;
using Octockup.Server.Handlers.Administration;
using Octockup.Server.Models.Dto;

namespace Octockup.Server.Controllers
{
    [ApiController]
    [Authorize(Policy = AuthenticationExtensions.AdminPolicy)]
    [Route("/api/v1/admin/storage-cleanups")]
    public class AdminStorageCleanupController(IMediator mediator) : ControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<IReadOnlyCollection<StorageCleanupDto>>> GetAsync(
            CancellationToken cancellationToken)
        {
            IReadOnlyCollection<StorageCleanupDto> cleanups = await mediator.Send(
                new GetStorageCleanupsQuery(),
                cancellationToken);
            return Ok(cleanups);
        }

        [HttpGet("runs")]
        public async Task<ActionResult<IReadOnlyCollection<StorageCleanupRunDto>>> GetRunsAsync(
            [FromQuery] int limit = 50,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyCollection<StorageCleanupRunDto> runs = await mediator.Send(
                new GetStorageCleanupRunsQuery(limit),
                cancellationToken);
            return Ok(runs);
        }

        [HttpPost("{moduleId:guid}/start")]
        public async Task<ActionResult<StorageCleanupDto>> StartAsync(
            Guid moduleId,
            CancellationToken cancellationToken)
        {
            StorageCleanupDto cleanup = await mediator.Send(
                new StartStorageCleanupCommand(moduleId),
                cancellationToken);
            return Ok(cleanup);
        }
    }
}
