﻿using MediatR;
using EasyExtensions;
using System.Text.Json;
using Octockup.Server.Models;
using Octockup.Server.Helpers;
using Octockup.Server.Database;
using Octockup.Server.Services;
using Octockup.Server.Extensions;
using Octockup.Server.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Octockup.Server.Providers.Storage;

namespace Octockup.Server.Handlers
{
    public class BackupRequestHandler(AppDbContext _dbContext, JobCancellationService _jobCancellations,
        IEnumerable<IStorageProvider> _storageProviders, ProgressTracker progressTracker,
        ILogger<BackupRequestHandler> _logger, IFileService _files) : IRequestHandler<HandleBackupRequest>
    {
        private static readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

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

        private async Task CreateBackupAsync(BackupTask backupTask, IStorageProvider storageProvider,
            ProgressTracker progressTracker, CancellationToken merged)
        {
            progressTracker.ReportProgress(0.01, "Requesting files", force: true);
            var files = storageProvider.GetAllFiles(p => progressTracker.ReportProgress(0.01, $"Got files: {p}"), cancellationToken: merged).ToList();
            progressTracker.ReportProgress(0.02, $"Got {files.Count} files", force: true);
            int processed = 0;
            int updated = 0;
            BackupSnapshot snapshot = new() { BackupTaskId = backupTask.Id };
            await _dbContext.BackupSnapshots.AddAsync(snapshot, merged);
            await _dbContext.SaveChangesAsync(merged);
            foreach (var remoteFileInfo in files)
            {
                merged.ThrowIfCancellationRequested();
                processed++;
                double progress = (double)processed / files.Count;
                progressTracker.ReportProgress(progress, "Checking file: " + remoteFileInfo.Name);
                snapshot.TotalSize += remoteFileInfo.Size;
                snapshot.Elapsed = progressTracker.Elapsed;
                await _dbContext.SaveChangesAsync(merged);

                _logger.LogDebug("Trying to get saved file: {file}", remoteFileInfo.Path);
                SavedFile? savedFile = await GetSavedFileAsync(remoteFileInfo, snapshot);
                _logger.LogDebug("Got saved file: {success}", savedFile != null);
                if (savedFile == null)
                {
                    _logger.LogDebug("File not found in database: {file}", remoteFileInfo.Path);
                    await SaveNewFileAsync(storageProvider, remoteFileInfo, snapshot, progressTracker, progress, merged);
                    continue;
                }
                bool filesEqual = CompareFiles(storageProvider, remoteFileInfo, savedFile, backupTask);
                if (filesEqual)
                {
                    _logger.LogDebug("Cloning file: {file}", remoteFileInfo.Path);
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
                await SaveNewFileAsync(storageProvider, remoteFileInfo, snapshot, progressTracker, progress, merged);
                updated++;
            }
            long totalSize = snapshot.SavedFiles.Sum(x => x.Size);
            snapshot.TotalSize = totalSize;
            snapshot.Log = progressTracker.Log;
            snapshot.Elapsed = progressTracker.Elapsed;
            await _dbContext.SaveChangesAsync(merged);
            string size = FileSystemHelpers.FormatSize(totalSize);
            progressTracker.ReportProgress(0.5, $"Processed {processed} files, {updated} updated, {size} total", force: true);
        }

        private async Task<SavedFile?> GetSavedFileAsync(RemoteFileInfo remoteFileInfo, BackupSnapshot snapshot)
        {
            var saved = await _dbContext.SavedFiles
                .Where(x => x.BackupSnapshot.BackupTaskId == snapshot.BackupTaskId
                    && x.SourcePath == remoteFileInfo.Path)
                .OrderByDescending(x => x.Id)
                .FirstOrDefaultAsync();
            if (saved == null)
            {
                return null;
            }
            bool isSavedFileExists = _files.SavedFileExists(saved.BackupSnapshot.BackupTaskId, saved.FileId);
            bool isFileBackupInfoExists = _files.FileBackupInfoExists(saved.BackupSnapshot.BackupTaskId, saved.FileId);
            if (!isSavedFileExists || !isFileBackupInfoExists)
            {
                _logger.LogWarning("File not found in local storage: {file}", remoteFileInfo.Name);
                return null;
            }
            return saved;
        }

        private bool CompareFiles(IStorageProvider storageProvider, RemoteFileInfo remoteFileInfo, SavedFile savedFile, BackupTask job)
        {
            if (remoteFileInfo.Path != savedFile.SourcePath)
            {
                _logger.LogDebug("Paths do not match: {remote} != {saved}", remoteFileInfo.Path, savedFile.SourcePath);
                return false;
            }
            if (remoteFileInfo.Size != savedFile.Size)
            {
                _logger.LogDebug("Sizes do not match: {remote} != {saved}", remoteFileInfo.Size, savedFile.Size);
                return false;
            }
            if (remoteFileInfo.FileCreatedAt != savedFile.MetadataCreatedAt)
            {
                _logger.LogDebug("Created dates do not match: {remote} != {saved}", remoteFileInfo.FileCreatedAt, savedFile.MetadataCreatedAt);
                return false;
            }
            if (remoteFileInfo.LastModified != savedFile.MetadataUpdatedAt)
            {
                _logger.LogDebug("Updated dates do not match: {remote} != {saved}", remoteFileInfo.LastModified, savedFile.MetadataUpdatedAt);
                return false;
            }
            if (job.StrictMode)
            {
                _logger.LogDebug("Strict mode enabled, checking SHA512 hash");
                string remoteHash = CalculateSHA512(storageProvider, remoteFileInfo);
                if (remoteHash != savedFile.SHA512)
                {
                    _logger.LogDebug("Hashes do not match: {remote} != {saved}", remoteHash, savedFile.SHA512);
                    return false;
                }
            }
            _logger.LogDebug("Files are equal: {remote} == {saved}", remoteFileInfo.Path, savedFile.SourcePath);
            return true;
        }

        private static string CalculateSHA512(IStorageProvider storageProvider, RemoteFileInfo item)
        {
            using var stream = storageProvider.GetFileStream(item);
            return stream.SHA512();
        }

        private async Task SaveNewFileAsync(IStorageProvider storageProvider, RemoteFileInfo item,
            BackupSnapshot snapshot, ProgressTracker progressTracker, double progress, CancellationToken merged)
        {
            progressTracker.ReportProgress(progress, "Saving file: " + item.Name);
            Guid newFileId = Guid.NewGuid();
            using var sourceStream = storageProvider.GetFileStream(item);
            await _files.SaveFileAsync(snapshot.BackupTaskId, newFileId, sourceStream,
                p => progressTracker.ReportProgress(progress, $"Saving file: {item.Name} - {p / item.Size:P0}"), merged);
            using Stream savedFs = _files.GetSavedFileStream(snapshot.BackupTaskId, newFileId);
            string hash = savedFs.SHA512();
            SavedFile savedFile = new()
            {
                FileId = newFileId,
                BackupSnapshotId = snapshot.Id,
                SourcePath = item.Path,
                Size = item.Size,
                SHA512 = hash,
                MetadataCreatedAt = item.FileCreatedAt,
                MetadataUpdatedAt = item.LastModified
            };
            await _dbContext.SavedFiles.AddAsync(savedFile, merged);
            await _dbContext.SaveChangesAsync(merged);
            progressTracker.ReportProgress(progress, "Saved file: " + item.Name);
            string fileInfoJson = JsonSerializer.Serialize(new
            {
                savedFile.FileId,
                savedFile.BackupSnapshotId,
                savedFile.SourcePath,
                savedFile.Size,
                savedFile.SHA512,
                savedFile.MetadataCreatedAt,
                savedFile.MetadataUpdatedAt
            }, _jsonOptions);
            await _files.SaveBackupInfoAsync(snapshot.BackupTaskId, newFileId, fileInfoJson, merged);
        }
    }
}
