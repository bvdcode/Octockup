// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Octockup.Server.Models.Dto;
using Octockup.Server.Services;

namespace Octockup.Server.Handlers.Administration
{
    public class GetAdminUsersQueryHandler(AdminUserService _users)
        : IRequestHandler<GetAdminUsersQuery, IReadOnlyCollection<AdminUserDto>>
    {
        public Task<IReadOnlyCollection<AdminUserDto>> Handle(
            GetAdminUsersQuery request,
            CancellationToken cancellationToken)
        {
            return _users.GetUsersAsync(cancellationToken);
        }
    }
}
