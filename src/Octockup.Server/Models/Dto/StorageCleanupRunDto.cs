// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.Models.Dto;
using Octockup.Server.Models.Enums;

namespace Octockup.Server.Models.Dto
{
    public class StorageCleanupRunDto : BaseDto<Guid>
    {
        public Guid ModuleId { get; set; }
        public string ModuleTag { get; set; } = string.Empty;
        public StorageCleanupStatus Status { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public long ScannedChunks { get; set; }
        public long DeletedChunks { get; set; }
        public long ReclaimedBytes { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
