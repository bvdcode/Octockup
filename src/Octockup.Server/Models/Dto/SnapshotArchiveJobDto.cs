// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Octockup.Server.Models.Enums;

namespace Octockup.Server.Models.Dto
{
    public class SnapshotArchiveJobDto
    {
        public Guid JobId { get; set; }
        public Guid UserId { get; set; }
        public Guid SnapshotId { get; set; }
        public SnapshotArchiveStatus Status { get; set; }
        public SnapshotArchivePhase Phase { get; set; }
        public bool CancellationRequested { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime? FinishedAt { get; set; }
        public string? ErrorMessage { get; set; }
        public long TotalFiles { get; set; }
        public long ProcessedFiles { get; set; }
        public long TotalBytes { get; set; }
        public long ProcessedBytes { get; set; }
        public long PreparedChunkReferences { get; set; }
        public string? CurrentPath { get; set; }
    }
}
