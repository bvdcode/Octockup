// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.Mediator.Contracts;
using Octockup.Server.Models.Dto;

namespace Octockup.Server.Handlers.Administration
{
    public class CreateAdminUserCommand(
        string username,
        string password,
        bool isAdmin) : IRequest<AdminUserDto>
    {
        public string Username { get; } = username;
        public string Password { get; } = password;
        public bool IsAdmin { get; } = isAdmin;
    }
}
