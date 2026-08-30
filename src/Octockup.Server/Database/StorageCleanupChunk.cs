// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.EntityFrameworkCore.Abstractions;
using EasyExtensions.Models.Enums;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Octockup.Server.Database
{
    [Table("storage_cleanup_chunks")]
    [Index(nameof(ModuleId), nameof(Hash), IsUnique = true)]
    public class StorageCleanupChunk : BaseEntity<Guid>
    {
        [Column("module_id")]
        public Guid ModuleId { get; set; }

        [Column("hash")]
        public string Hash { get; set; } = null!;

        [Column("stored_size")]
        public long StoredSize { get; set; }

        [Column("original_size")]
        public long OriginalSize { get; set; }

        [Column("compression_algorithm")]
        public CompressionAlgorithm CompressionAlgorithm { get; set; }

        [DeleteBehavior(DeleteBehavior.Restrict)]
        public virtual Module Module { get; set; } = null!;
    }
}
