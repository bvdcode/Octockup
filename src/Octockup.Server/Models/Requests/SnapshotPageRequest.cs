// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using System.ComponentModel.DataAnnotations;

namespace Octockup.Server.Models.Requests
{
    public class SnapshotPageRequest
    {
        [Range(1, 200)]
        public int PageSize { get; set; } = 25;

        [StringLength(256)]
        public string? Cursor { get; set; }
    }
}
