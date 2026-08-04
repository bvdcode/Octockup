// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Octockup.Server.Services
{
    public class OidcCallbackException(string returnUrl, Exception innerException)
        : Exception("OIDC callback failed.", innerException)
    {
        public string ReturnUrl { get; } = returnUrl;
    }
}
