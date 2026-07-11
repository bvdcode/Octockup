// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Octockup.Server.Models.Dto
{
    public class StatsDto
    {
        public int TotalUsers { get; set; }
        public IReadOnlyList<StorageStatsDto> StorageStats { get; set; } = [];
    }
}
