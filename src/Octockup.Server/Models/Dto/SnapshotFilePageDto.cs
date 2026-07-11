// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Octockup.Server.Models.Dto
{
    public class SnapshotFilePageDto
    {
        public IReadOnlyList<SnapshotFileDto> Items { get; set; } = [];
        public string? NextCursor { get; set; }
        public bool HasNextPage { get; set; }
        public long TotalCount { get; set; }
    }
}
