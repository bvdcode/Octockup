// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using EasyExtensions.EntityFrameworkCore.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Octockup.Server.Database
{
    [Table("oidc_providers")]
    [Index(nameof(Slug), IsUnique = true)]
    public class OidcProvider : BaseEntity<Guid>
    {
        [Column("name")]
        [MaxLength(80)]
        public string Name { get; set; } = null!;

        [Column("slug")]
        [MaxLength(64)]
        public string Slug { get; set; } = null!;

        [Column("issuer")]
        [MaxLength(512)]
        public string Issuer { get; set; } = null!;

        [Column("public_base_url")]
        [MaxLength(512)]
        public string PublicBaseUrl { get; set; } = null!;

        [Column("client_id")]
        [MaxLength(256)]
        public string ClientId { get; set; } = null!;

        [Column("client_secret_encrypted")]
        public string? ClientSecretEncrypted { get; set; }

        [Column("scopes")]
        public string[] Scopes { get; set; } = [];

        [Column("is_enabled")]
        public bool IsEnabled { get; set; }

        public virtual ICollection<OidcLoginState> LoginStates { get; set; } = [];
        public virtual ICollection<UserExternalIdentity> ExternalIdentities { get; set; } = [];
    }
}
