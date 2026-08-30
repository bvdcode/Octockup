// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using EasyExtensions.EntityFrameworkCore.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Octockup.Server.Database
{
    [Table("oidc_login_states")]
    [Index(nameof(StateHash), IsUnique = true)]
    [Index(nameof(ExpiresAt))]
    public class OidcLoginState : BaseEntity<Guid>
    {
        [Column("provider_id")]
        public Guid ProviderId { get; set; }

        [Column("state_hash")]
        [MaxLength(64)]
        public string StateHash { get; set; } = null!;

        [Column("code_verifier_encrypted")]
        public string CodeVerifierEncrypted { get; set; } = null!;

        [Column("nonce_encrypted")]
        public string NonceEncrypted { get; set; } = null!;

        [Column("return_url")]
        [MaxLength(1024)]
        public string ReturnUrl { get; set; } = null!;

        [Column("link_user_id")]
        public Guid? LinkUserId { get; set; }

        [Column("expires_at")]
        public DateTime ExpiresAt { get; set; }

        [ForeignKey(nameof(ProviderId))]
        [DeleteBehavior(DeleteBehavior.Restrict)]
        public virtual OidcProvider Provider { get; set; } = null!;

        [ForeignKey(nameof(LinkUserId))]
        [DeleteBehavior(DeleteBehavior.Restrict)]
        public virtual User? LinkUser { get; set; }
    }
}
