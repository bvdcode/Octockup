using Quartz;
using Octockup.Server.Services;
using Octockup.Server.Database;
using Microsoft.EntityFrameworkCore;
using EasyExtensions.Quartz.Attributes;

namespace Octockup.Server.Jobs
{
    [JobTrigger(days: 1)]
    public class CleanupJob(AppDbContext _dbContext, ILogger<CleanupJob> _logger,
        IFileService _files) : IJob
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
            _logger.LogInformation("Cleanup job completed - deleted {count} snapshots.", snapshots.Count);
        }

        private async Task DeleteSnapshotAsync(BackupSnapshot snapshot)
        {
            var snapshotFiles = await _dbContext.SavedFiles
                .Where(x => x.BackupSnapshotId == snapshot.Id)
                .ToListAsync();
            foreach (var file in snapshotFiles)
            {
                await _files.DeleteFileAsync(snapshot.Id, file.FileId);
                _dbContext.SavedFiles.Remove(file);
                await _dbContext.SaveChangesAsync();
                _logger.LogInformation("Deleted file {fileId} for snapshot {snapshotId}.", file.FileId, snapshot.Id);
            }
        }
    }
}
