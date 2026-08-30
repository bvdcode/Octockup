// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

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
        public string Username { get; set; } = null!;

        [Column("password_phc")]
        public string PasswordPhc { get; set; } = null!;

        [Column("is_admin")]
        public bool IsAdmin { get; set; }

        [Column("is_disabled")]
        public bool IsDisabled { get; set; }

        public virtual ICollection<Module> Modules { get; set; } = [];

        public virtual ICollection<UserExternalIdentity> ExternalIdentities { get; set; } = [];

        public virtual ICollection<OidcLoginState> OidcLoginStates { get; set; } = [];
    }
}
