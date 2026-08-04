// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Octockup.Server.Models.Requests
{
    public class AdminUpdateUserAccessRequest
    {
        public bool IsAdmin { get; set; }
        public bool IsDisabled { get; set; }
    }
}
