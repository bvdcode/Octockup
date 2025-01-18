using Quartz;
using MediatR;
using Octockup.Server.Hubs;
using Octockup.Server.Models;
using Octockup.Server.Database;
using Microsoft.AspNetCore.SignalR;
using Octockup.Server.Models.Enums;
using Microsoft.EntityFrameworkCore;
using EasyExtensions.Quartz.Attributes;

namespace Octockup.Server.Jobs
{
    [JobTrigger(minutes: 15)]
    public class HandleBackupJob(AppDbContext _dbContext, ILogger<HandleBackupJob> _logger,
        IMediator _mediator, IHubContext<BackupHub> _hub) : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            var pendingJobs = await GetPendingJobsAsync();
            _logger.LogInformation("Found {count} pending jobs.", pendingJobs.Count());
            foreach (var job in pendingJobs)
            {
                _logger.LogInformation("Executing job {jobId} for user {userId}.", job.Id, job.UserId);
                try
                {
                    await _mediator.Send(new HandleBackupRequest(job.Id));
                }
                catch (Exception ex)
                {
                    if (ex is OperationCanceledException)
                    {
                        _logger.LogWarning("Job {jobId} was cancelled.", job.Id);
                    }
                    else
                    {
                        _logger.LogError(ex, "Job {jobId} failed.", job.Id);
                    }
                    job.LastMessage = ex.Message;
                    job.Status = BackupTaskStatus.Failed;
                    job.CompletedAt = DateTime.UtcNow;
                    await _dbContext.SaveChangesAsync();
                }
                await _hub.Clients.User(job.UserId.ToString()).SendAsync("Progress", job.Progress);
            }
        }

        private async Task<IEnumerable<BackupTask>> GetPendingJobsAsync()
        {
            var allJobs = await _dbContext.BackupTasks
                .Where(x => !x.IsDeleted)
                .ToListAsync();
            List<BackupTask> result = [];
            foreach (var job in allJobs)
            {
                if (job.Status == BackupTaskStatus.Running)
                {
                    job.Status = BackupTaskStatus.Failed;
                    job.LastMessage = "Job was interrupted by unexpected shutdown.";
                    job.CompletedAt = null;
                    job.ForceRun = true;
                    await _dbContext.SaveChangesAsync();
                    _logger.LogWarning("Job {jobId} was interrupted by unexpected shutdown.", job.Id);
                }
                if (job.ForceRun)
                {
                    _logger.LogInformation("Job {jobId} is forced to run.", job.Id);
                    result.Add(job);
                    continue;
                }
                if (!job.IsEnabled)
                {
                    _logger.LogDebug("Job {jobId} is disabled, skipping.", job.Id);
                    continue;
                }
                if (job.CompletedAt != null && job.Interval == TimeSpan.Zero)
                {
                    _logger.LogDebug("Job {jobId} is running only once, skipping.", job.Id);
                    continue;
                }
                DateTime now = DateTime.UtcNow;
                DateTime nextRun = job.CompletedAt?.Add(job.Interval) ?? job.StartAt;
                string interval = GetIntervalText(nextRun - now);
                if (nextRun <= now)
                {
                    _logger.LogDebug("Job {jobId} is pending, next run at {nextRun} ({interval})",
                        job.Id, nextRun, interval);
                    result.Add(job);
                }
                else
                {
                    _logger.LogDebug("Job {jobId} is not pending, next run at {nextRun} ({interval})",
                        job.Id, nextRun, interval);
                }
            }
            return result;
        }

        private static string GetIntervalText(TimeSpan timeSpan)
        {
            TimeSpan duration = timeSpan.Duration();
            string body = duration switch
            {
                { TotalDays: > 1 } => $"{duration.TotalDays:F1} days",
                { TotalHours: > 1 } => $"{duration.TotalHours:F1} hours",
                { TotalMinutes: > 1 } => $"{duration.TotalMinutes:F0} minutes",
                { TotalSeconds: > 1 } => $"{duration.TotalSeconds:F0} seconds",
                _ => "now"
            };
            return timeSpan > TimeSpan.Zero ? $"in {body}" : $"delayed by {body}";
        }
    }
}
