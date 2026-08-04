// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions;
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
    [Route("/api/v1/admin/users")]
    public class AdminUsersController(IMediator _mediator) : ControllerBase
    {
        [HttpGet]
        public async Task<ActionResult<IReadOnlyCollection<AdminUserDto>>> GetUsersAsync(
            CancellationToken cancellationToken)
        {
            IReadOnlyCollection<AdminUserDto> users = await _mediator.Send(
                new GetAdminUsersQuery(),
                cancellationToken);
            return Ok(users);
        }

        [HttpPost]
        public async Task<ActionResult<AdminUserDto>> CreateUserAsync(
            [FromBody] AdminCreateUserRequest request,
            CancellationToken cancellationToken)
        {
            AdminUserDto user = await _mediator.Send(
                new CreateAdminUserCommand(request.Username, request.Password, request.IsAdmin),
                cancellationToken);
            return Ok(user);
        }

        [HttpPut("{userId:guid}/access")]
        public async Task<ActionResult<AdminUserDto>> UpdateAccessAsync(
            Guid userId,
            [FromBody] AdminUpdateUserAccessRequest request,
            CancellationToken cancellationToken)
        {
            AdminUserDto user = await _mediator.Send(
                new UpdateAdminUserAccessCommand(
                    User.GetUserId(),
                    userId,
                    request.IsAdmin,
                    request.IsDisabled),
                cancellationToken);
            return Ok(user);
        }
    }
}
