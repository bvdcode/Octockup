// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.Mediator.Contracts;
using Octockup.Server.Models.Dto;

namespace Octockup.Server.Handlers.Administration
{
    public class UpdateAdminUserAccessCommand(
        Guid actorUserId,
        Guid userId,
        bool isAdmin,
        bool isDisabled) : IRequest<AdminUserDto>
    {
        public Guid ActorUserId { get; } = actorUserId;
        public Guid UserId { get; } = userId;
        public bool IsAdmin { get; } = isAdmin;
        public bool IsDisabled { get; } = isDisabled;
    }
}
