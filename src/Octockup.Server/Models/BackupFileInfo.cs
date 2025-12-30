// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Octockup.Server.Models
{
    public class BackupFileInfo
    {
        public long? Size { get; set; }
        public string Path { get; set; } = null!;
        public string? Name { get; set; } = null!;
        public DateTime? LastModified { get; set; }
    }
}
