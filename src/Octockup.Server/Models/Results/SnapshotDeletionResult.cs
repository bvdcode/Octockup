// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Octockup.Server.Models.Results
{
    public class SnapshotDeletionResult
    {
        public bool Deleted { get; set; }
        public string? ErrorMessage { get; set; }
        public Guid BackupId { get; set; }
        public int DeletedSnapshotFiles { get; set; }
        public long DeletedSnapshotFileBytes { get; set; }
    }
}
