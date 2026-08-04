// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Octockup.Server.Models.Requests
{
    public class OidcProviderRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Slug { get; set; }
        public string Issuer { get; set; } = string.Empty;
        public string PublicBaseUrl { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string? ClientSecret { get; set; }
        public bool ClearClientSecret { get; set; }
        public string[] Scopes { get; set; } = [];
        public bool IsEnabled { get; set; }
    }
}
