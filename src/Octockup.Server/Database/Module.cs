// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using Octockup.Server.Models.Enums;
using Microsoft.EntityFrameworkCore;
using EasyExtensions.EntityFrameworkCore.Abstractions;

namespace Octockup.Server.Database
{
    [Index(nameof(Tag), IsUnique = true)]
    public class Module : BaseEntity<Guid>
    {
        public Guid UserId { get; set; }
        public string Tag { get; set; } = string.Empty;
        public ModuleDestination Destination { get; set; }
        public string BackupModuleId { get; set; } = string.Empty;
        public Dictionary<string, string> Parameters { get; set; } = [];

        public virtual User User { get; set; } = null!;
    }
}
