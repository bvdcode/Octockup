// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Octockup.Server.Models.Enums
{
    public enum ServerBackupSection
    {
        None = -1,
        Modules = 0,
        Backups = 1,
        Schedules = 2,
        Snapshots = 3,
        SnapshotFiles = 4
    }
}
