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
        public double Progress { get; private set; }
        public TimeSpan Elapsed => _stopwatch.Elapsed;

        private BackupTask? _job;
        private const int UpdateInterval = 1000;
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

        public async void ReportProgress(double progress)
        {
            if (_job == null)
            {
                throw new InvalidOperationException("Job ID not set.");
            }
            if (_updateSw.ElapsedMilliseconds < UpdateInterval)
            {
                return;
            }
            _updateSw.Restart();
            _job.Elapsed = _stopwatch.Elapsed;
            _job.Status = BackupTaskStatus.Running;
            _job.Progress = Math.Round(progress, 2);
            try
            {
                _dbContext.SaveChanges();
                await _hub.Clients.User(_job.UserId.ToString()).SendAsync("Progress", _job.Progress);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update job progress.");
            }
        }
    }
}