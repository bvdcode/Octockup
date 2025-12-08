// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using EasyExtensions.Models.Dto;

namespace Octockup.Server.Models.Dto
{
    public class UserDto : BaseDto<Guid>
    {
        public string Username { get; set; } = string.Empty;
    }
}
