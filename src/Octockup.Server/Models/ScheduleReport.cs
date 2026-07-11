// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.Extensions.Options;
using Octockup.Server.Abstractions;
using Octockup.Server.Models.Dto;
using Octockup.Server.Models.Enums;
using Octockup.Server.Models.Options;
using System.Diagnostics;

namespace Octockup.Server.Models
{
    public class ScheduleReport(
        Guid userId,
        Guid scheduleId,
        Guid backupId,
        IScheduleProgressPublisher _publisher,
        IOptions<BackupProgressOptions> options,
        TimeProvider _timeProvider,
        ILogger<ScheduleReport> _logger) : IAsyncDisposable
    {
        private static readonly TimeSpan SpeedWindow = TimeSpan.FromMinutes(1);
        private readonly Lock _stateLock = new();
        private readonly Lock _lifecycleLock = new();
        private readonly Queue<(DateTime Timestamp, long Bytes)> _speedSamples = [];
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private readonly Stopwatch _aggregateLogStopwatch = Stopwatch.StartNew();
        private readonly TimeSpan _publishInterval = options.Value.PublishInterval;
        private readonly TimeSpan _aggregateLogInterval = options.Value.AggregateLogInterval;
        private CancellationTokenSource? _backgroundTaskCts;
        private Task? _backgroundTask;
        private long _processedBytes;
        private ScheduleStatus _status = ScheduleStatus.Created;
        private BackupProgressStage _stage = BackupProgressStage.Preparing;
        private DateTime _lastProgressAt = _timeProvider.GetUtcNow().UtcDateTime;
        private string _message = string.Empty;
        private int _processed;
        private double _speed;
        private int _total;
        private bool _isEnumerationCompleted;
        private string _currentPath = string.Empty;
        private string _currentFile = string.Empty;

        public Guid UserId { get; } = userId;
        public Guid BackupId { get; } = backupId;
        public Guid ScheduleId { get; } = scheduleId;

        public int Processed
        {
            get
            {
                lock (_stateLock)
                {
                    return _processed;
                }
            }
        }

        public void StartBackgroundReporting(CancellationToken cancellationToken)
        {
            lock (_lifecycleLock)
            {
                if (_backgroundTask is not null)
                {
                    throw new InvalidOperationException("Background reporting is already running.");
                }

                _backgroundTaskCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _backgroundTask = BackgroundReportingAsync(_backgroundTaskCts.Token);
            }
        }

        public void Update(
            int processedFiles,
            string message,
            long processedBytes = 0,
            ScheduleStatus status = ScheduleStatus.Running,
            BackupProgressStage stage = BackupProgressStage.Preparing)
        {
            DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
            lock (_stateLock)
            {
                _status = status;
                _processed = processedFiles;
                SetStageCore(stage, message, now);
                UpdateSpeedCore(processedBytes, false, now);
            }
        }

        public void SetStage(BackupProgressStage stage, string message)
        {
            DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
            lock (_stateLock)
            {
                SetStageCore(stage, message, now);
            }
        }

        public void SetCurrentFile(
            string currentFile,
            string currentPath,
            int total,
            bool isEnumerationCompleted)
        {
            lock (_stateLock)
            {
                _currentFile = currentFile;
                _currentPath = currentPath;
                _total = total;
                _isEnumerationCompleted = isEnumerationCompleted;
            }
        }

        public void SetEnumeration(int total, bool isCompleted)
        {
            lock (_stateLock)
            {
                _total = total;
                _isEnumerationCompleted = isCompleted;
            }
        }

        public async Task PublishFinalAsync(
            int processedFiles,
            string message,
            ScheduleStatus status,
            BackupProgressStage stage,
            CancellationToken cancellationToken)
        {
            Update(processedFiles, message, status: status, stage: stage);
            await StopBackgroundReportingAsync().ConfigureAwait(false);
            ScheduleReportDto report = CreateSnapshot();
            await PublishSafelyAsync(report, cancellationToken).ConfigureAwait(false);
            LogAggregate(report, true);
        }

        private async Task BackgroundReportingAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (true)
                {
                    await Task.Delay(_publishInterval, cancellationToken).ConfigureAwait(false);
                    ScheduleReportDto report = CreateSnapshot();
                    await PublishSafelyAsync(report, cancellationToken).ConfigureAwait(false);
                    LogAggregate(report, false);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
        }

        private ScheduleReportDto CreateSnapshot()
        {
            DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
            lock (_stateLock)
            {
                UpdateSpeedCore(0, true, now);
                return new ScheduleReportDto
                {
                    ProcessedBytes = _processedBytes,
                    UserId = UserId,
                    BackupId = BackupId,
                    ScheduleId = ScheduleId,
                    Status = _status,
                    Stage = _stage,
                    Timestamp = now,
                    LastProgressAt = _lastProgressAt,
                    NoProgressFor = now - _lastProgressAt,
                    Elapsed = _stopwatch.Elapsed,
                    Message = _message,
                    Processed = _processed,
                    Speed = _speed,
                    Total = _total,
                    IsEnumerationCompleted = _isEnumerationCompleted,
                    CurrentPath = _currentPath,
                    CurrentFile = _currentFile
                };
            }
        }

        private async Task PublishSafelyAsync(
            ScheduleReportDto report,
            CancellationToken cancellationToken)
        {
            try
            {
                await _publisher.PublishAsync(report, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _logger.LogDebug(
                    ex,
                    "Failed to publish progress for schedule {ScheduleId}.",
                    ScheduleId);
            }
        }

        private void LogAggregate(ScheduleReportDto report, bool force)
        {
            if (!force && _aggregateLogStopwatch.Elapsed < _aggregateLogInterval)
            {
                return;
            }

            _aggregateLogStopwatch.Restart();
            _logger.LogInformation(
                "Schedule {ScheduleId}: stage {Stage}, files {Processed}/{Total}, bytes {ProcessedBytes}, " +
                "speed {Speed:F0} B/s, elapsed {Elapsed}, no progress for {NoProgressFor}.",
                report.ScheduleId,
                report.Stage,
                report.Processed,
                report.Total,
                report.ProcessedBytes,
                report.Speed,
                report.Elapsed,
                report.NoProgressFor);
        }

        private void SetStageCore(
            BackupProgressStage stage,
            string message,
            DateTime now)
        {
            _stage = stage;
            _message = message;
            _lastProgressAt = now;
        }

        private void UpdateSpeedCore(long processedBytes, bool forceSample, DateTime now)
        {
            if (processedBytes > 0)
            {
                _processedBytes += processedBytes;
            }

            if (forceSample || processedBytes > 0 || _speedSamples.Count == 0)
            {
                _speedSamples.Enqueue((now, _processedBytes));
            }

            while (_speedSamples.Count > 0 &&
                now - _speedSamples.Peek().Timestamp > SpeedWindow)
            {
                _speedSamples.Dequeue();
            }

            if (_speedSamples.Count < 2)
            {
                _speed = 0;
                return;
            }

            (DateTime Timestamp, long Bytes) first = _speedSamples.Peek();
            (DateTime Timestamp, long Bytes) last = _speedSamples.Last();
            long deltaBytes = last.Bytes - first.Bytes;
            double deltaSeconds = (last.Timestamp - first.Timestamp).TotalSeconds;
            _speed = deltaSeconds > 0 ? deltaBytes / deltaSeconds : 0;
        }

        private async Task StopBackgroundReportingAsync()
        {
            CancellationTokenSource? cancellationTokenSource;
            Task? backgroundTask;
            lock (_lifecycleLock)
            {
                cancellationTokenSource = _backgroundTaskCts;
                backgroundTask = _backgroundTask;
                _backgroundTaskCts = null;
                _backgroundTask = null;
            }

            if (cancellationTokenSource is null || backgroundTask is null)
            {
                return;
            }

            await cancellationTokenSource.CancelAsync().ConfigureAwait(false);
            try
            {
                await backgroundTask.ConfigureAwait(false);
            }
            finally
            {
                cancellationTokenSource.Dispose();
            }
        }

        public async ValueTask DisposeAsync()
        {
            await StopBackgroundReportingAsync().ConfigureAwait(false);
            _stopwatch.Stop();
            GC.SuppressFinalize(this);
        }
    }
}
