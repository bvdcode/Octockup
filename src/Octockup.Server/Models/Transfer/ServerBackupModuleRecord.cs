// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Octockup.Server.Models.Enums;

namespace Octockup.Server.Models.Transfer
{
    public class ServerBackupModuleRecord
    {
        public Guid Id { get; set; }
        public string Tag { get; set; } = string.Empty;
        public ModuleDestination Destination { get; set; }
        public string BackupModuleId { get; set; } = string.Empty;
        public Dictionary<string, string> Parameters { get; set; } = [];
    }
}
