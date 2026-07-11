// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Octockup.Server.Models.Options
{
    public class BackupExecutionOptions
    {
        public int MaxConcurrentBackups { get; set; } = 4;
        public int MaxChunkLookupMemoryBytes { get; set; } = 64 * 1024 * 1024;
    }
}
