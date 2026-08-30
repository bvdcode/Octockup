// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Octockup.Server.Hubs;
using Octockup.Server.Models.Enums;
using System.Diagnostics;
using System.Linq;

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
        public bool IsEnumerationCompleted { get; set; }
        public string CurrentPath { get; set; } = string.Empty;
        public string CurrentFile { get; set; } = string.Empty;

        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private readonly CancellationTokenSource _backgroundTaskCts = new();
        private Task? _backgroundTask;
        private static readonly TimeSpan SpeedWindow = TimeSpan.FromMinutes(1);
        private readonly Queue<(DateTime Timestamp, long Bytes)> _speedSamples = new();
        private readonly Lock _speedLock = new();

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
                        UpdateSpeed(0, forceSample: true);
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
            UpdateSpeed(processedBytes, forceSample: processedBytes == 0);
            await _hubContext.Clients.User(UserId.ToString()).SendAsync("ScheduleReport", this, cancellationToken: cancellationToken);
        }

        private void UpdateSpeed(long processedBytes, bool forceSample)
        {
            DateTime now = DateTime.UtcNow;
            if (processedBytes > 0)
            {
                ProcessedBytes += processedBytes;
            }

            lock (_speedLock)
            {
                if (forceSample || processedBytes > 0 || _speedSamples.Count == 0)
                {
                    _speedSamples.Enqueue((now, ProcessedBytes));
                }

                while (_speedSamples.Count > 0 && now - _speedSamples.Peek().Timestamp > SpeedWindow)
                {
                    _speedSamples.Dequeue();
                }

                if (_speedSamples.Count >= 2)
                {
                    (DateTime Timestamp, long Bytes) first = _speedSamples.Peek();
                    (DateTime Timestamp, long Bytes) last = _speedSamples.Last();
                    long deltaBytes = last.Bytes - first.Bytes;
                    double deltaSeconds = (last.Timestamp - first.Timestamp).TotalSeconds;
                    Speed = deltaSeconds > 0 ? deltaBytes / deltaSeconds : 0;
                }
                else
                {
                    Speed = 0;
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _backgroundTaskCts.CancelAsync();
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
