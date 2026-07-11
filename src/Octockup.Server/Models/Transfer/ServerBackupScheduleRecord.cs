// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Octockup.Server.Models.Enums;

namespace Octockup.Server.Models.Transfer
{
    public class ServerBackupScheduleRecord
    {
        public Guid Id { get; set; }
        public Guid BackupId { get; set; }
        public DateTime? FinishedAt { get; set; }
        public ScheduleStatus Status { get; set; }
        public DateTime StartAt { get; set; }
        public DateTime? NextRunAt { get; set; }
        public TimeSpan? Interval { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
