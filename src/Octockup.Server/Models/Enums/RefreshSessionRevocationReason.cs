// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Octockup.Server.Models.Enums
{
    public enum RefreshSessionRevocationReason
    {
        Rotated = 0,
        Logout = 1,
        PasswordChanged = 2,
        ReuseDetected = 3,
        Expired = 4
    }
}
