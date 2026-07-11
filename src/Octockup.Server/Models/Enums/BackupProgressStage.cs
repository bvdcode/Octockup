// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Octockup.Server.Models.Enums
{
    public enum BackupProgressStage
    {
        Listing = 0,
        Preparing = 1,
        Reading = 2,
        Hashing = 3,
        Compressing = 4,
        Encrypting = 5,
        Uploading = 6,
        Recording = 7,
        Persisting = 8,
        Finalizing = 9,
        Completed = 10,
        Failed = 11
    }
}
