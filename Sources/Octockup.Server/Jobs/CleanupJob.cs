using Quartz;
using Octockup.Server.Database;
using Microsoft.EntityFrameworkCore;
using EasyExtensions.Quartz.Attributes;

namespace Octockup.Server.Jobs
{
    [JobTrigger(days: 1)]
    public class CleanupJob(AppDbContext _dbContext, ILogger<CleanupJob> _logger) : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            var snapshots = await _dbContext.BackupSnapshots
                .Where(x => x.IsDeleted)
                .ToListAsync();
            foreach (var snapshot in snapshots)
            {
                _logger.LogInformation("Deleting snapshot {snapshotId} for task {taskId}.", snapshot.Id, snapshot.BackupTaskId);
                await DeleteSnapshotAsync(snapshot);
            }
        }

        private async Task DeleteSnapshotAsync(BackupSnapshot snapshot)
        {

        }
    }
}
