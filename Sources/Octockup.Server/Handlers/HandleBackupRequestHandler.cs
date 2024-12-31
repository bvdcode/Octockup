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
            job.LastMessage = null;
            var storageProvider = _storageProviders.FirstOrDefault(x => x.Name == job.Provider)
                ?? throw new InvalidOperationException("Storage provider not found: " + job.Provider);
            await progressTracker.SetJobIdAsync(job.Id);
            SetParameters(storageProvider, job.GetParameters());
            await CreateBackupAsync(job, storageProvider, progressTracker, merged);
            job.Progress = 1;
            job.CompletedAt = DateTime.UtcNow;
            job.Elapsed = progressTracker.Elapsed;
            job.Status = BackupTaskStatus.Completed;
            job.ForceRun = false;
            await _dbContext.SaveChangesAsync(merged);
        }

        private static void SetParameters(IStorageProvider storageProvider, Dictionary<string, string> dictionary)
        {
            var property = storageProvider.GetType().GetProperty("Parameters");
            if (property == null)
            {
                return;
            }
            object? propertyValue = property.GetValue(storageProvider);
            if (propertyValue == null)
            {
                propertyValue = Activator.CreateInstance(property.PropertyType);
                property.SetValue(storageProvider, propertyValue);
            }
            foreach (var pair in dictionary)
            {
                var prop = property.PropertyType.GetProperty(pair.Key);
                if (prop == null)
                {
                    continue;
                }
                if (prop.PropertyType != typeof(string))
                {
                    prop.SetValue(propertyValue, Convert.ChangeType(pair.Value, prop.PropertyType));
                }
                else
                {
                    prop.SetValue(propertyValue, pair.Value);
                }
            }
        }

        private static Task CreateBackupAsync(BackupTask job, IStorageProvider storageProvider,
            ProgressTracker progressTracker, CancellationToken merged)
        {
            progressTracker.ReportProgress(0.01, "Requesting files");
            var files = storageProvider.GetAllFiles();
            int counter = 0;
            long size = 0;
            foreach (var item in files)
            {
                merged.ThrowIfCancellationRequested();
                progressTracker.ReportProgress(0.01, "Copying: " + item.Name);
                counter++;
                size += item.Size;
            }
            double mb = size / 1024.0 / 1024.0;
            mb = Math.Round(mb, 2);
            progressTracker.ReportProgress(0.5, $"Processed {counter} files, {counter / 2} updated, {mb} MB total", force: true);

            _ = job;
            return Task.CompletedTask;
        }
    }
}
