// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Octockup.Server.Models.Dto;
using Octockup.Server.Services;

namespace Octockup.Server.Handlers.Administration
{
    public class CreateAdminUserCommandHandler(AdminUserService _users)
        : IRequestHandler<CreateAdminUserCommand, AdminUserDto>
    {
        public Task<AdminUserDto> Handle(
            CreateAdminUserCommand request,
            CancellationToken cancellationToken)
        {
            return _users.CreateAsync(
                request.Username,
                request.Password,
                request.IsAdmin,
                cancellationToken);
        }
    }
}
