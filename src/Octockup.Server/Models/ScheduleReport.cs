// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Octockup.Server.Hubs;
using Octockup.Server.Models.Enums;
using System.Diagnostics;

namespace Octockup.Server.Models
{
    public class ScheduleReport(Guid userId, Guid scheduleId, Guid backupId, IHubContext<EventHub> _hubContext) : IAsyncDisposable
    {
        public long ProcessedBytes { get; private set; }
        public Guid UserId { get; } = userId;
        public Guid BackupId { get; } = backupId;
        public Guid ScheduleId { get; } = scheduleId;
        public ScheduleStatus Status { get; private set; }
        public DateTime Timestamp { get; private set; }
        public TimeSpan Elapsed => _stopwatch.Elapsed;
        public string Message { get; private set; } = string.Empty;
        public int Processed { get; private set; }
        public double Speed { get; private set; }
        public int Total { get; set; }

        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private readonly CancellationTokenSource _backgroundTaskCts = new();
        private Task? _backgroundTask;

        public void StartBackgroundReporting(CancellationToken cancellationToken)
        {
            CancellationToken linkedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _backgroundTaskCts.Token).Token;
            _backgroundTask = BackgroundReportingTask(linkedToken);
        }

        private async Task BackgroundReportingTask(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(500, cancellationToken);
                    if (!cancellationToken.IsCancellationRequested)
                    {
                        Speed = ProcessedBytes / Math.Max(1, _stopwatch.Elapsed.TotalSeconds);
                        await _hubContext.Clients.User(UserId.ToString()).SendAsync("ScheduleReport", this, cancellationToken);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when disposing
            }
        }

        public async Task SendAsync(int processedFiles,
            string message,
            long processedBytes = 0,
            ScheduleStatus status = ScheduleStatus.Running,
            CancellationToken cancellationToken = default)
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
            await _hubContext.Clients.User(UserId.ToString()).SendAsync("ScheduleReport", this, cancellationToken: cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            _backgroundTaskCts.Cancel();
            if (_backgroundTask != null)
            {
                try
                {
                    await _backgroundTask;
                }
                catch (OperationCanceledException)
                {
                    // Expected
                }
            }
            _backgroundTaskCts.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
