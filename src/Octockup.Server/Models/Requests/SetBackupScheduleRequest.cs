// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Octockup.Server.Models.Requests
{
    public class SetBackupScheduleRequest
    {
        public int IntervalMinutes { get; set; }
    }
}
