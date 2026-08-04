// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Octockup.Server.Models.Dto;
using Octockup.Server.Services;

namespace Octockup.Server.Handlers.Administration
{
    public class UpdateAdminUserAccessCommandHandler(AdminUserService _users)
        : IRequestHandler<UpdateAdminUserAccessCommand, AdminUserDto>
    {
        public Task<AdminUserDto> Handle(
            UpdateAdminUserAccessCommand request,
            CancellationToken cancellationToken)
        {
            return _users.UpdateAccessAsync(
                request.ActorUserId,
                request.UserId,
                request.IsAdmin,
                request.IsDisabled,
                cancellationToken);
        }
    }
}
