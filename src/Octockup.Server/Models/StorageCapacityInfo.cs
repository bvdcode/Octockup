// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Octockup.Server.Models
{
    public class StorageCapacityInfo
    {
        public long? TotalBytes { get; set; }
        public long? AvailableBytes { get; set; }
    }
}
