using Quartz;
using Octockup.Server.Database;
using EasyExtensions.Quartz.Attributes;

namespace Octockup.Server.Jobs
{
    [JobTrigger(minutes: 5)]
    public class HandleBackupJob(AppDbContext _dbContext, ILogger<HandleBackupJob> _logger) : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            var pendingJobs = GetPendingJobs();
            _logger.LogInformation("Found {count} pending jobs.", pendingJobs.Count());
        }

        private IEnumerable<BackupTask> GetPendingJobs()
        {
            foreach (var job in _dbContext.BackupTasks)
            {
                DateTime now = DateTime.UtcNow;
                DateTime nextRun = job.CompletedAt?.Add(job.Interval) ?? job.StartAt;
                string interval = GetIntervalText(nextRun - now);
                if (nextRun <= now)
                {
                    _logger.LogDebug("Job {jobId} is pending, next run at {nextRun} ({interval})",
                        job.Id, nextRun, interval);
                    yield return job;
                }
                else
                {
                    _logger.LogDebug("Job {jobId} is not pending, next run at {nextRun} ({interval})",
                        job.Id, nextRun, interval);
                }
            }
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
