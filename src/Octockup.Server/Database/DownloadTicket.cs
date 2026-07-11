// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.EntityFrameworkCore.Abstractions;
using Microsoft.EntityFrameworkCore;
using Octockup.Server.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Octockup.Server.Database
{
    [Table("download_tickets")]
    [Index(nameof(TokenHash), IsUnique = true)]
    [Index(nameof(ExpiresAt))]
    public class DownloadTicket : BaseEntity<Guid>
    {
        [Column("user_id")]
        public Guid UserId { get; set; }

        [Column("token_hash")]
        [MaxLength(64)]
        public string TokenHash { get; set; } = string.Empty;

        [Column("kind")]
        public DownloadTicketKind Kind { get; set; }

        [Column("resource_id")]
        public Guid? ResourceId { get; set; }

        [Column("secondary_resource_id")]
        public Guid? SecondaryResourceId { get; set; }

        [Column("include_files")]
        public bool IncludeFiles { get; set; }

        [Column("expires_at")]
        public DateTime ExpiresAt { get; set; }

        [Column("consumed_at")]
        public DateTime? ConsumedAt { get; set; }
    }
}
