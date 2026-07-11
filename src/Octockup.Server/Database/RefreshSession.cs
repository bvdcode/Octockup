// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.EntityFrameworkCore.Abstractions;
using Microsoft.EntityFrameworkCore;
using Octockup.Server.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Octockup.Server.Database
{
    [Table("refresh_sessions")]
    [Index(nameof(TokenHash), IsUnique = true)]
    [Index(nameof(UserId), nameof(RevokedAt))]
    [Index(nameof(FamilyId), nameof(RevokedAt))]
    [Index(nameof(ExpiresAt))]
    public class RefreshSession : BaseEntity<Guid>
    {
        [Column("user_id")]
        public Guid UserId { get; set; }

        [Column("family_id")]
        public Guid FamilyId { get; set; }

        [Column("token_hash")]
        [MaxLength(64)]
        public string TokenHash { get; set; } = string.Empty;

        [Column("expires_at")]
        public DateTime ExpiresAt { get; set; }

        [Column("revoked_at")]
        public DateTime? RevokedAt { get; set; }

        [Column("revocation_reason")]
        public RefreshSessionRevocationReason? RevocationReason { get; set; }

        [DeleteBehavior(DeleteBehavior.Restrict)]
        public virtual User User { get; set; } = null!;
    }
}
