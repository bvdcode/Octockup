// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions;
using EasyExtensions.AspNetCore.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Octockup.Server.Services;

namespace Octockup.Server.Controllers
{
    [ApiController]
    public class StorageMaintenanceController(
        StorageGarbageCollectionService _garbageCollectionService) : ControllerBase
    {
        [Authorize]
        [HttpPost("/api/v1/storages/{storageId:guid}/garbage-collect")]
        public async Task<IActionResult> CollectGarbage(
            [FromRoute] Guid storageId,
            CancellationToken cancellationToken)
        {
            try
            {
                return Ok(await _garbageCollectionService.CollectAsync(
                    User.GetUserId(),
                    storageId,
                    cancellationToken));
            }
            catch (InvalidOperationException ex)
            {
                return this.ApiBadRequest(ex.Message);
            }
        }
    }
}
