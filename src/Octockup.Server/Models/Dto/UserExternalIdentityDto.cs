// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using EasyExtensions.Models.Dto;

namespace Octockup.Server.Models.Dto
{
    public class UserExternalIdentityDto : BaseDto<Guid>
    {
        public Guid ProviderId { get; set; }
        public string ProviderName { get; set; } = string.Empty;
        public string ProviderSlug { get; set; } = string.Empty;
        public bool ProviderEnabled { get; set; }
        public string? Email { get; set; }
        public string? DisplayName { get; set; }
        public DateTime? LastUsedAt { get; set; }
    }
}
