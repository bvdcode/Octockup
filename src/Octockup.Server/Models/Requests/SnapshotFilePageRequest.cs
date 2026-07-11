// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using System.ComponentModel.DataAnnotations;

namespace Octockup.Server.Models.Requests
{
    public class SnapshotFilePageRequest
    {
        [Range(1, 200)]
        public int PageSize { get; set; } = 50;

        [StringLength(4096)]
        public string? Cursor { get; set; }

        [StringLength(500)]
        public string? Search { get; set; }
    }
}
