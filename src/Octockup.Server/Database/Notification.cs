// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.EntityFrameworkCore.Abstractions;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Octockup.Server.Database
{
    [Table("notifications")]
    public class Notification : BaseEntity<Guid>
    {
        [Column("user_id")]
        public Guid UserId { get; set; }

        [Column("message")]
        public string Message { get; set; } = null!;

        [Column("details")]
        public string? Details { get; set; }

        [Column("metadata")]
        public string? Metadata { get; set; }

        [Column("read_at")]
        public DateTime? ReadAt { get; set; }

        [Column("priority")]
        public int Priority { get; set; }

        [DeleteBehavior(DeleteBehavior.Restrict)]
        public virtual User User { get; set; } = null!;
    }
}
