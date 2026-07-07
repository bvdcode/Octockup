// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Octockup.Server.Models.Enums
{
    public enum StorageCleanupPhase
    {
        Preparing = 0,
        CollectingReferences = 1,
        ScanningStorage = 2,
        Completed = 3
    }
}
