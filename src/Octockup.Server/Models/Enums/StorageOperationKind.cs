// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Octockup.Server.Models.Enums
{
    public enum StorageOperationKind
    {
        Backup = 0,
        Cleanup = 1,
        Restore = 2,
        Maintenance = 3
    }
}
