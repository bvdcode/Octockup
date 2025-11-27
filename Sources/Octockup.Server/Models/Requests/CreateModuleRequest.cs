// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using Octockup.Server.Models.Enums;

namespace Octockup.Server.Models.Requests
{
    public class CreateModuleRequest
    {
        public ModuleDestination Destination { get; set; }
        public string Tag { get; set; } = string.Empty;
        public string BackupModuleId { get; set; } = string.Empty;
        public Dictionary<string, string> Parameters { get; set; } = [];
    }
}
