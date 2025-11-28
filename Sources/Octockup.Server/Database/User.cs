// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using Microsoft.EntityFrameworkCore;
using EasyExtensions.EntityFrameworkCore.Abstractions;

namespace Octockup.Server.Database
{
    /// <summary>
    /// Represents an application user with authentication credentials and associated modules.
    /// </summary>
    [Index(nameof(Username), IsUnique = true)]
    public class User : BaseEntity<Guid>
    {
        public string Username { get; set; } = string.Empty;
        public ICollection<Module> Modules { get; set; } = [];
        public string PasswordPhc { get; set; } = string.Empty;
        public string EncryptionKey { get; set; } = string.Empty;
    }
}
