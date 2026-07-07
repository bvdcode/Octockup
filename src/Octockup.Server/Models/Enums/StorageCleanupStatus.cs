// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Octockup.Server.Models.Enums
{
    public enum StorageCleanupStatus
    {
        Pending = 0,
        Running = 1,
        Completed = 2,
        Failed = 3,
        Canceled = 4
    }
}
