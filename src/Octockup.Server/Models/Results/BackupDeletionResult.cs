// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Octockup.Server.Models.Results
{
    public class BackupDeletionResult
    {
        public bool Deleted { get; set; }
        public string? ErrorMessage { get; set; }
        public int DeletedSchedules { get; set; }
        public int DeletedSnapshots { get; set; }
        public int DeletedSnapshotFiles { get; set; }
    }
}
