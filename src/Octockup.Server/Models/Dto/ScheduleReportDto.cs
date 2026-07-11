// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Octockup.Server.Models.Enums;

namespace Octockup.Server.Models.Dto
{
    public class ScheduleReportDto
    {
        public long ProcessedBytes { get; set; }
        public Guid UserId { get; set; }
        public Guid BackupId { get; set; }
        public Guid ScheduleId { get; set; }
        public ScheduleStatus Status { get; set; }
        public BackupProgressStage Stage { get; set; }
        public DateTime Timestamp { get; set; }
        public DateTime LastProgressAt { get; set; }
        public TimeSpan NoProgressFor { get; set; }
        public TimeSpan Elapsed { get; set; }
        public string Message { get; set; } = string.Empty;
        public int Processed { get; set; }
        public double Speed { get; set; }
        public int Total { get; set; }
        public bool IsEnumerationCompleted { get; set; }
        public string CurrentPath { get; set; } = string.Empty;
        public string CurrentFile { get; set; } = string.Empty;
    }
}
