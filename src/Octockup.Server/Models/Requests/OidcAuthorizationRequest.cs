// SPDX-License-Identifier: MIT
// Copyright (c) 2025-2026 Vadim Belov <https://belov.us>

namespace Octockup.Server.Models.Requests
{
    public class OidcAuthorizationRequest
    {
        public string? ReturnUrl { get; set; }
        public bool LinkAccount { get; set; }
    }
}
