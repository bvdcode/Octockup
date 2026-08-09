// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.Quartz.Attributes;
using Microsoft.EntityFrameworkCore;
using Octockup.Server.Database;
using Octockup.Server.Models.Enums;
using Octockup.Server.Services;
using Quartz;

namespace Octockup.Server.Jobs
{
    [JobTrigger(minutes: 1)]
    public class StorageCleanupJob(
        AppDbContext dbContext,
        StorageCleanupProcessor processor,
        StorageOperationCoordinator storageOperations,
        ILogger<StorageCleanupJob> logger) : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            StorageCleanup? cleanup = await dbContext.StorageCleanups
                .Include(x => x.Module)
                .Where(x => x.Status == StorageCleanupStatus.Running)
                .OrderBy(x => x.UpdatedAt)
                .FirstOrDefaultAsync(context.CancellationToken);
            if (cleanup is null)
            {
                return;
            }

            await using StorageOperationLease? lease = storageOperations.TryAcquireCleanup(cleanup.ModuleId);
            if (lease is null)
            {
                logger.LogDebug(
                    "Storage cleanup {CleanupId} is waiting for active backups on storage {StorageId}.",
                    cleanup.Id,
                    cleanup.ModuleId);
                return;
            }

            try
            {
                await processor.ProcessAsync(cleanup, context.CancellationToken);
            }
            catch (StorageCleanupConfigurationException ex)
            {
                cleanup.Status = StorageCleanupStatus.Failed;
                cleanup.ErrorMessage = ex.Message;
                await dbContext.SaveChangesAsync(CancellationToken.None);
                logger.LogError(ex, "Storage cleanup {CleanupId} has invalid configuration.", cleanup.Id);
                throw;
            }
            catch (Exception ex)
            {
                cleanup.ErrorMessage = ex.Message;
                await dbContext.SaveChangesAsync(CancellationToken.None);
                logger.LogError(ex, "Storage cleanup {CleanupId} failed.", cleanup.Id);
                throw;
            }
        }
    }
}
