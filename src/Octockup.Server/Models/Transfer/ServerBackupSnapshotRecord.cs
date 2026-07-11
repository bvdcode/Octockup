// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Octockup.Server.Models.Transfer
{
    public class ServerBackupSnapshotRecord
    {
        public Guid Id { get; set; }
        public Guid BackupId { get; set; }
        public DateTime? CompletedAt { get; set; }
        public long TotalSize { get; set; }
        public int FilesCount { get; set; }
    }
}
