// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

using EasyExtensions.Models.Dto;

namespace Octockup.Server.Models.Dto
{
    public class OidcProviderDto : BaseDto<Guid>
    {
        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string Issuer { get; set; } = string.Empty;
        public string PublicBaseUrl { get; set; } = string.Empty;
        public string CallbackUrl { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public bool HasClientSecret { get; set; }
        public string[] Scopes { get; set; } = [];
        public bool IsEnabled { get; set; }
    }
}
