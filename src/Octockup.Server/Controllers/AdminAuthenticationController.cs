// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Octockup.Server.Extensions;
using Octockup.Server.Handlers.Administration;
using Octockup.Server.Models.Dto;
using Octockup.Server.Models.Requests;

namespace Octockup.Server.Controllers
{
    [ApiController]
    [Authorize(Policy = AuthenticationExtensions.AdminPolicy)]
    [Route("/api/v1/admin/authentication")]
    public class AdminAuthenticationController(IMediator _mediator) : ControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<AuthenticationSettingsDto>> GetSettingsAsync(
            CancellationToken cancellationToken)
        {
            AuthenticationSettingsDto settings = await _mediator.Send(
                new GetAuthenticationSettingsQuery(),
                cancellationToken);
            return Ok(settings);
        }

        [HttpPut]
        public async Task<ActionResult<AuthenticationSettingsDto>> UpdateSettingsAsync(
            [FromBody] UpdateAuthenticationSettingsRequest request,
            CancellationToken cancellationToken)
        {
            AuthenticationSettingsDto settings = await _mediator.Send(
                new UpdateAuthenticationSettingsCommand(request.PasswordLoginEnabled),
                cancellationToken);
            return Ok(settings);
        }
    }
}
