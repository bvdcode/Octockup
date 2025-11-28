// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using System.Diagnostics;
using Octockup.Server.Hubs;
using Microsoft.AspNetCore.SignalR;
using Octockup.Server.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Octockup.Server.Models
{
    public class ScheduleReport(Guid userId, Guid scheduleId, IHubContext<EventHub> _hubContext)
    {
        public long ProcessedBytes { get; private set; }
        public Guid UserId { get; } = userId;
        public Guid ScheduleId { get; } = scheduleId;
        public ScheduleStatus Status { get; private set; }
        public DateTime Timestamp { get; private set; }
        public TimeSpan Elapsed => _stopwatch.Elapsed;
        public string Message { get; private set; } = string.Empty;
        public int Processed { get; private set; }
        public double Speed { get; private set; }
        public int Total { get; set; }

        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

        public async Task SendAsync(int processedFiles, string message, long processedBytes = 0, ScheduleStatus status = ScheduleStatus.Running)
        {
            Status = status;
            Timestamp = DateTime.UtcNow;
            Message = message;
            Processed = processedFiles;
            if (processedBytes > 0)
            {
                ProcessedBytes += processedBytes;
            }
            Speed = ProcessedBytes / Math.Max(1, _stopwatch.Elapsed.TotalSeconds);
            await _hubContext.Clients.User(UserId.ToString()).SendAsync("ScheduleReport", this);
        }
    }
}
