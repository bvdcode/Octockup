using MediatR;
using Octockup.Server.Models;
using Octockup.Server.Database;
using Octockup.Server.Services;
using Octockup.Server.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Octockup.Server.Providers.Storage;
using Octockup.Server.Extensions;

namespace Octockup.Server.Handlers
{
    public class BackupRequestHandler(AppDbContext _dbContext, JobCancellationService _jobCancellations,
        IEnumerable<IStorageProvider> _storageProviders, ProgressTracker progressTracker,
        ILogger<BackupRequestHandler> _logger) : IRequestHandler<HandleBackupRequest>
    {
        public async Task Handle(HandleBackupRequest request, CancellationToken cancellationToken)
        {
            _logger.LogDebug("Handling backup task: {request}", request.BackupTaskId);
            CancellationToken token = _jobCancellations.GetCancellationToken(request.BackupTaskId);
            CancellationToken merged = CancellationTokenSource.CreateLinkedTokenSource(token, cancellationToken).Token;
            BackupTask job = await _dbContext.BackupTasks.FindAsync([request.BackupTaskId], cancellationToken: merged)
                ?? throw new InvalidOperationException("Backup task with the specified ID not found: " + request.BackupTaskId);
            job.Status = BackupTaskStatus.Running;
            job.LastMessage = null;
            var storageProvider = _storageProviders.FirstOrDefault(x => x.GetClassName() == job.ProviderClass)
                ?? throw new InvalidOperationException("Storage provider not found: " + job.ProviderClass);
            await progressTracker.SetJobIdAsync(job.Id);
            SetParameters(storageProvider, job.GetParameters());
            await CreateBackupAsync(job, storageProvider, progressTracker, merged);
            job.Progress = 1;
            job.CompletedAt = DateTime.UtcNow;
            job.Elapsed = progressTracker.Elapsed;
            job.Status = BackupTaskStatus.Completed;
            job.ForceRun = false;
            await _dbContext.SaveChangesAsync(merged);
            _logger.LogInformation("Backup task {id} completed", job.Id);
        }

        private void SetParameters(IStorageProvider storageProvider, Dictionary<string, string> dictionary)
        {
            var property = storageProvider
                .GetType()
                .GetProperty("Parameters");
            if (property == null)
            {
                _logger.LogDebug("Storage provider does not have Parameters property: {provider}", storageProvider.Name);
                return;
            }
            object? propertyValue = property.GetValue(storageProvider);
            if (propertyValue == null)
            {
                propertyValue = Activator.CreateInstance(property.PropertyType);
                property.SetValue(storageProvider, propertyValue);
                _logger.LogDebug("Created new Parameters object for storage provider: {provider}", storageProvider.Name);
            }
            foreach (var pair in dictionary)
            {
                var prop = property.PropertyType.GetProperty(pair.Key);
                if (prop == null)
                {
                    _logger.LogDebug("Property not found: {key}", pair.Key);
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
                _logger.LogDebug("Set property {key} to {value}", pair.Key, pair.Value);
            }
        }

        private async Task CreateBackupAsync(BackupTask job, IStorageProvider storageProvider,
            ProgressTracker progressTracker, CancellationToken merged)
        {
            progressTracker.ReportProgress(0.01, "Requesting files", force: true);
            var files = storageProvider.GetAllFiles(p => progressTracker.ReportProgress(0.01, $"Got files: {p}"), cancellationToken: merged).ToList();
            progressTracker.ReportProgress(0.02, $"Got {files.Count} files", force: true);
            int counter = 0;
            BackupSnapshot snapshot = new() { BackupTaskId = job.Id };
            await _dbContext.BackupSnapshots.AddAsync(snapshot, merged);
            await _dbContext.SaveChangesAsync(merged);
            foreach (var remoteFileInfo in files)
            {
                merged.ThrowIfCancellationRequested();
                counter++;
                double progress = (double)counter / files.Count;
                progressTracker.ReportProgress(progress, "Checking file: " + remoteFileInfo.Name);

                SavedFile? savedFile = await GetSavedFileAsync(remoteFileInfo, snapshot);
                if (savedFile == null)
                {
                    await SaveNewFileAsync(storageProvider, remoteFileInfo, snapshot, job, progressTracker, progress, merged);
                    continue;
                }
                bool filesEqual = CompareFiles(storageProvider, remoteFileInfo, savedFile, job);
                if (filesEqual)
                {
                    SavedFile clone = new()
                    {
                        Size = savedFile.Size,
                        FileId = savedFile.FileId,
                        SHA512 = savedFile.SHA512,
                        BackupSnapshotId = snapshot.Id,
                        SourcePath = savedFile.SourcePath,
                        MetadataCreatedAt = savedFile.MetadataCreatedAt,
                        MetadataUpdatedAt = savedFile.MetadataUpdatedAt,
                    };
                    await _dbContext.SavedFiles.AddAsync(clone, merged);
                    await _dbContext.SaveChangesAsync(merged);
                    progressTracker.ReportProgress(progress, "File is up to date: " + remoteFileInfo.Name);
                    continue;
                }
                await SaveNewFileAsync(storageProvider, remoteFileInfo, snapshot, job, progressTracker, progress, merged);
            }
            double mb = 100000000 / 1024.0 / 1024.0;
            mb = Math.Round(mb, 2);
            progressTracker.ReportProgress(0.5, $"Processed {counter} files, {counter / 2} updated, {mb} MB total", force: true);
        }

        private async Task<SavedFile?> GetSavedFileAsync(RemoteFileInfo remoteFileInfo, BackupSnapshot snapshot)
        {
            return await _dbContext.SavedFiles
                .Where(x => x.BackupSnapshot.BackupTaskId == snapshot.BackupTaskId
                    && x.SourcePath == remoteFileInfo.Path)
                .OrderByDescending(x => x.Id)
                .FirstOrDefaultAsync();
        }

        private bool CompareFiles(IStorageProvider storageProvider, RemoteFileInfo remoteFileInfo, SavedFile savedFile, BackupTask job)
        {
            if (remoteFileInfo.Path != savedFile.SourcePath)
            {
                return false;
            }
            if (remoteFileInfo.Size != savedFile.Size)
            {
                return false;
            }
            if (remoteFileInfo.FileCreatedAt != savedFile.MetadataCreatedAt)
            {
                return false;
            }
            if (remoteFileInfo.LastModified != savedFile.MetadataUpdatedAt)
            {
                return false;
            }
            if (job.StrictMode)
            {
                string remoteHash = CalculateSHA512(storageProvider, remoteFileInfo);
                if (remoteHash != savedFile.SHA512)
                {
                    return false;
                }
            }
            return true;
        }

        private string CalculateSHA512(IStorageProvider storageProvider, RemoteFileInfo item)
        {
            throw new NotImplementedException();
        }

        private async Task SaveNewFileAsync(IStorageProvider storageProvider, RemoteFileInfo item, BackupSnapshot snapshot, BackupTask job, ProgressTracker progressTracker, double progress, CancellationToken merged)
        {
            throw new NotImplementedException();
        }
    }
}
