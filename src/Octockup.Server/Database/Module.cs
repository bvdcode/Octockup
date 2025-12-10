// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using EasyExtensions.EntityFrameworkCore.Abstractions;
using Microsoft.EntityFrameworkCore;
using Octockup.Server.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace Octockup.Server.Database
{
    [Table("modules")]
    [Index(nameof(Tag), IsUnique = true)]
    public class Module : BaseEntity<Guid>
    {
        [Column("user_id")]
        public Guid UserId { get; set; }

        [Column("tag")]
        public string Tag { get; set; } = string.Empty;

        [Column("destination")]
        public ModuleDestination Destination { get; set; }

        [Column("backup_module_id")]
        public string BackupModuleId { get; set; } = string.Empty;

        [Column("parameters")]
        public Dictionary<string, string> Parameters { get; set; } = [];

        // [Column("encrypted_parameters")]
        // public string EncryptedParameters { get; set; } = string.Empty;

        public virtual User User { get; set; } = null!;
    }
}
