// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.Models.Dto;

namespace Octockup.Server.Models.Dto
{
    public class UserDto : BaseDto<Guid>
    {
        public string Username { get; set; } = string.Empty;
    }
}
