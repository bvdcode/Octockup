// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Octockup.Server.Models
{
    internal record NormalizedOidcProviderInput(
        string Name,
        string? Slug,
        string Issuer,
        string PublicBaseUrl,
        string ClientId,
        string? ClientSecret,
        string[] Scopes,
        bool IsEnabled);
}
