﻿using System.Text;
using System.Diagnostics;
using Octockup.Server.Hubs;
using Octockup.Server.Database;
using Microsoft.AspNetCore.SignalR;
using Octockup.Server.Models.Enums;

namespace Octockup.Server.Services
{
    public class ProgressTracker(AppDbContext _dbContext, ILogger<ProgressTracker> _logger,
        IHubContext<BackupHub> _hub)
    {
        public string Log => _log.ToString();
        public double Progress { get; private set; }
        public TimeSpan Elapsed => _stopwatch.Elapsed;

        private BackupTask? _job;
        private const int UpdateInterval = 1000;
        private readonly StringBuilder _log = new();
        private readonly Stopwatch _updateSw = Stopwatch.StartNew();
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

        public async Task SetJobIdAsync(int id)
        {
            _job = await _dbContext.BackupTasks.FindAsync(id);
            if (_job == null)
            {
                throw new InvalidOperationException("Job ID not found.");
            }
        }

        public async void ReportProgress(double progress, string? message = "", bool force = false)
        {
            if (_job == null)
            {
                throw new InvalidOperationException("Job ID not set.");
            }
            if (string.IsNullOrWhiteSpace(message))
            {
                // progress is 0 to 1, Progress to percentage
                _log.AppendLine($"[{DateTime.UtcNow:HH:mm:ss}] Progress: {_job.Progress:P2}");
            }
            else
            {
                _log.AppendLine($"[{DateTime.UtcNow:HH:mm:ss}] Progress: {_job.Progress:P2}, Message: {message}");
            }
            if (_updateSw.ElapsedMilliseconds < UpdateInterval && !force)
            {
                return;
            }
            _updateSw.Restart();
            _job.Elapsed = _stopwatch.Elapsed;
            _job.Status = BackupTaskStatus.Running;
            _job.Progress = Math.Round(progress, 2);
            if (!string.IsNullOrWhiteSpace(message))
            {
                _job.LastMessage = message;
            }
            try
            {
                await _dbContext.SaveChangesAsync();
                await _hub.Clients.User(_job.UserId.ToString()).SendAsync("Progress", _job.Progress);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update job progress.");
            }
            _logger.LogInformation("Job {jobId} progress: {progress}%", _job.Id, _job.Progress.ToString("P2"));
            _logger.LogDebug("Job {jobId} progress updated for {elapsed}.", _job.Id, _job.Elapsed);
        }
    }
}