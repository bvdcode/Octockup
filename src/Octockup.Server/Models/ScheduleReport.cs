// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using System.Diagnostics;
using Octockup.Server.Hubs;
using Microsoft.AspNetCore.SignalR;
using Octockup.Server.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Octockup.Server.Models
{
    public class ScheduleReport : IAsyncDisposable
    {
        private readonly IHubContext<EventHub> _hubContext;
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private readonly Timer _periodicSendTimer;
        private readonly SemaphoreSlim _sendLock = new(1, 1);
        private const int MinUpdateIntervalMs = 500;
        private long _pendingBytes;
        private bool _disposed;

        public long ProcessedBytes { get; private set; }
        public Guid UserId { get; }
        public Guid BackupId { get; }
        public Guid ScheduleId { get; }
        public ScheduleStatus Status { get; private set; }
        public DateTime Timestamp { get; private set; }
        public TimeSpan Elapsed => _stopwatch.Elapsed;
        public string Message { get; private set; } = string.Empty;
        public int Processed { get; private set; }
        public double Speed { get; private set; }
        public int Total { get; set; }

        public ScheduleReport(Guid userId, Guid scheduleId, Guid backupId, IHubContext<EventHub> hubContext)
        {
            UserId = userId;
            ScheduleId = scheduleId;
            BackupId = backupId;
            _hubContext = hubContext;
            _periodicSendTimer = new Timer(async _ => await SendPendingUpdatesAsync(), null, TimeSpan.FromMilliseconds(MinUpdateIntervalMs), TimeSpan.FromMilliseconds(MinUpdateIntervalMs));
        }

        public void UpdateProgress(int processedFiles, string message, long processedBytes = 0)
        {
            Processed = processedFiles;
            Message = message;
            if (processedBytes > 0)
            {
                Interlocked.Add(ref _pendingBytes, processedBytes);
            }
        }

        public async Task SendAsync(int processedFiles, string message, long processedBytes = 0, ScheduleStatus status = ScheduleStatus.Running)
        {
            UpdateProgress(processedFiles, message, processedBytes);
            Status = status;
            await SendNowAsync();
        }

        private async Task SendPendingUpdatesAsync()
        {
            if (_disposed)
            {
                return;
            }

            await SendNowAsync();
        }

        private async Task SendNowAsync()
        {
            if (_disposed)
            {
                return;
            }

            await _sendLock.WaitAsync();
            try
            {
                var pendingBytes = Interlocked.Exchange(ref _pendingBytes, 0);
                if (pendingBytes > 0)
                {
                    ProcessedBytes += pendingBytes;
                }

                Timestamp = DateTime.UtcNow;
                Speed = ProcessedBytes / Math.Max(1, _stopwatch.Elapsed.TotalSeconds);
                await _hubContext.Clients.User(UserId.ToString()).SendAsync("ScheduleReport", this);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            await _periodicSendTimer.DisposeAsync();
            
            await SendNowAsync();
            _sendLock.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
