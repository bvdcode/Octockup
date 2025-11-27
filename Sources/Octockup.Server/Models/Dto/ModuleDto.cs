// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using EasyExtensions.Models.Dto;
using Octockup.Server.Models.Enums;

namespace Octockup.Server.Models.Dto
{
    public class ModuleDto : BaseDto<Guid>
    {
        public ModuleDestination Destination { get; set; }
        public Guid UserId { get; set; }
        public string Tag { get; set; } = string.Empty;
        public string BackupModuleId { get; set; } = string.Empty;
    }
}
