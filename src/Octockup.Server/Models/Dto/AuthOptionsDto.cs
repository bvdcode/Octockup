// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Octockup.Server.Models.Dto
{
    public class AuthOptionsDto
    {
        public bool PasswordLoginEnabled { get; set; }
        public IReadOnlyList<PublicOidcProviderDto> OidcProviders { get; set; } = [];
    }
}
