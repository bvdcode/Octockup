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
            SetParameters(storageProvider, job.GetParameters());
            await CreateBackupAsync(job, storageProvider, merged, progressTracker);
            job.Progress = 1;
            job.CompletedAt = DateTime.UtcNow;
            job.Elapsed = progressTracker.Elapsed;
            job.Status = BackupTaskStatus.Completed;
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
                prop.SetValue(propertyValue, pair.Value);
            }
        }

        private async Task CreateBackupAsync(BackupTask job, IStorageProvider storageProvider,
            CancellationToken merged, ProgressTracker progressTracker)
        {
            progressTracker.ReportProgress(0.01);
            var files = storageProvider.GetAllFiles();
            BackupSnapshot snapshot = new BackupSnapshot
            {
                BackupTaskId = job.Id,
                Files = files.Select(x => x.Name).ToArray(),
                Size = files.Sum(x => x.Size)
            };
            progressTracker.ReportProgress(0.02);

            throw new Exception("Got files: " + snapshot.Files.Length);

            await _dbContext.BackupSnapshots.AddAsync(snapshot, merged);
            await _dbContext.SaveChangesAsync(merged);
        }
    }
}
