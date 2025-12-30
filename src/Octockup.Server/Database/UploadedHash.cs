// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.EntityFrameworkCore.Abstractions;
using EasyExtensions.Models.Enums;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace Octockup.Server.Database
{
    [Table("uploaded_hashes")]
    [Index(nameof(Hash))]
    [Index(nameof(ModuleId), nameof(Hash), IsUnique = true)]
    public class UploadedHash : BaseEntity<Guid>
    {
        [Column("module_id")]
        public Guid ModuleId { get; set; }

        [Column("hash")]
        public string Hash { get; set; } = string.Empty;

        [Column("stored_size")]
        public long StoredSize { get; set; }

        [Column("original_size")]
        public long OriginalSize { get; set; }

        [Column("compression_algorithm")]
        public CompressionAlgorithm CompressionAlgorithm { get; set; }

        public virtual Module Module { get; set; } = null!;
    }
}
