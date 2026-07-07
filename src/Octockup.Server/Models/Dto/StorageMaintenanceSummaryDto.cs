// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Octockup.Server.Models.Dto
{
    public class StorageMaintenanceSummaryDto : ModuleDto
    {
        public int TotalBackups { get; set; }
        public long IndexedObjects { get; set; }
        public long IndexedStoredSize { get; set; }
        public long IndexedOriginalSize { get; set; }
        public long ReferenceCount { get; set; }
        public long ReferencedChunks { get; set; }
        public long DeduplicatedChunks { get; set; }
        public long? TotalCapacityBytes { get; set; }
        public long? AvailableBytes { get; set; }
        public StorageCleanupJobDto? ActiveJob { get; set; }
        public StorageCleanupJobDto? LastJob { get; set; }
    }
}
