// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.EntityFrameworkCore.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Octockup.Server.Database
{
    [Table("authentication_settings")]
    [Index(nameof(Name), IsUnique = true)]
    public class AuthenticationSettings : BaseEntity<Guid>
    {
        public const string GlobalName = "global";

        [Column("name")]
        [MaxLength(32)]
        public string Name { get; set; } = null!;

        [Column("password_login_enabled")]
        public bool PasswordLoginEnabled { get; set; }
    }
}
