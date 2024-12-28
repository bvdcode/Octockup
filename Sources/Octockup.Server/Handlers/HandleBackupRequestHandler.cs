using MediatR;
using Octockup.Server.Models;
using Octockup.Server.Database;
using Octockup.Server.Services;
using Octockup.Server.Models.Enums;
using Octockup.Server.Providers.Storage;

namespace Octockup.Server.Handlers
{
    public class HandleBackupRequestHandler(AppDbContext _dbContext, JobCancellationService _jobCancellations,
        IEnumerable<IStorageProvider> _storageProviders, ProgressTracker progressTracker) : IRequestHandler<HandleBackupRequest>
    {
        public async Task Handle(HandleBackupRequest request, CancellationToken cancellationToken)
        {
            CancellationToken token = _jobCancellations.GetCancellationToken(request.BackupTaskId);
            CancellationToken merged = CancellationTokenSource.CreateLinkedTokenSource(token, cancellationToken).Token;
            BackupTask job = await _dbContext.BackupTasks.FindAsync([request.BackupTaskId], cancellationToken: merged)
                ?? throw new InvalidOperationException("Backup task with the specified ID not found: " + request.BackupTaskId);
            job.Status = BackupTaskStatus.Running;
            job.LastError = null;
            var storageProvider = _storageProviders.FirstOrDefault(x => x.Name == job.Provider)
                ?? throw new InvalidOperationException("Storage provider not found: " + job.Provider);
            await progressTracker.SetJobIdAsync(job.Id);

            await CreateBackupAsync(job, storageProvider, merged, progressTracker);

            job.Progress = 1;
            job.CompletedAt = DateTime.UtcNow;
            job.Elapsed = progressTracker.Elapsed;
            job.Status = BackupTaskStatus.Completed;
            await _dbContext.SaveChangesAsync(merged);
        }

        private async Task CreateBackupAsync(BackupTask job, IStorageProvider storageProvider,
            CancellationToken merged, ProgressTracker progressTracker)
        {
            for (int i = 0; i < 50; i++)
            {
                double progress = 0.01 * i;
                progressTracker.ReportProgress(progress);
                await Task.Delay(1000, merged);
            }
            throw new InvalidOperationException("Backup failed because of Merry Christmas!");
        }
    }
}
