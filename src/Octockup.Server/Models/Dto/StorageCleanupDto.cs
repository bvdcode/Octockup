// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.Models.Dto;
using Octockup.Server.Models.Enums;

namespace Octockup.Server.Models.Dto
{
    public class StorageCleanupDto : BaseDto<Guid>
    {
        public Guid ModuleId { get; set; }
        public string ModuleTag { get; set; } = string.Empty;
        public StorageCleanupStatus Status { get; set; }
        public long ScannedChunks { get; set; }
        public long PendingChunks { get; set; }
        public long TotalDeletedChunks { get; set; }
        public long TotalReclaimedBytes { get; set; }
        public DateTime? LastStartedAt { get; set; }
        public DateTime? LastCompletedAt { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
