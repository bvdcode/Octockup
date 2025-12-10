// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using EasyExtensions.EntityFrameworkCore.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Octockup.Server.Database
{
    [Table("users")]
    [Index(nameof(Username), IsUnique = true)]
    public class User : BaseEntity<Guid>
    {
        [Column("username")]
        public string Username { get; set; } = string.Empty;

        [Column("password_phc")]
        public string PasswordPhc { get; set; } = string.Empty;

        public ICollection<Module> Modules { get; set; } = [];
    }
}
