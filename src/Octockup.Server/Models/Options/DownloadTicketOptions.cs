// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Octockup.Server.Models.Options
{
    public class DownloadTicketOptions
    {
        public TimeSpan Lifetime { get; set; } = TimeSpan.FromMinutes(2);
    }
}
