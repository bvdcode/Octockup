// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using EasyExtensions.Quartz.Extensions;
using Microsoft.EntityFrameworkCore;
using Octockup.Server.Database;
using Octockup.Server.Jobs;
using Octockup.Server.Models.Dto;
using Octockup.Server.Models.Enums;
using Octockup.Server.Services;
using Quartz;

namespace Octockup.Server.Handlers.Administration
{
    public class StartStorageCleanupCommandHandler(
        AppDbContext dbContext,
        ISchedulerFactory schedulerFactory)
        : IRequestHandler<StartStorageCleanupCommand, StorageCleanupDto>
    {
        public async Task<StorageCleanupDto> Handle(
            StartStorageCleanupCommand request,
            CancellationToken cancellationToken)
        {
            Module? module = await dbContext.Modules
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.Id == request.ModuleId && x.Destination == ModuleDestination.Target,
                    cancellationToken);
            if (module is null)
            {
                throw new AuthApiException(
                    StatusCodes.Status404NotFound,
                    $"Storage module not found: {request.ModuleId}");
            }

            StorageCleanup? cleanup = await dbContext.StorageCleanups
                .FirstOrDefaultAsync(x => x.ModuleId == request.ModuleId, cancellationToken);
            if (cleanup is null)
            {
                cleanup = new StorageCleanup
                {
                    ModuleId = request.ModuleId,
                };
                await dbContext.StorageCleanups.AddAsync(cleanup, cancellationToken);
            }

            if (cleanup.Status != StorageCleanupStatus.Running)
            {
                DateTime startedAt = DateTime.UtcNow;
                cleanup.Status = StorageCleanupStatus.Running;
                cleanup.CursorHash = null;
                cleanup.ScanUpperBoundHash = null;
                cleanup.ScannedChunks = 0;
                cleanup.LastStartedAt = startedAt;
                cleanup.ErrorMessage = null;
                StorageCleanupRun run = new()
                {
                    ModuleId = request.ModuleId,
                    Status = StorageCleanupStatus.Running,
                    StartedAt = startedAt,
                };
                await dbContext.StorageCleanupRuns.AddAsync(run, cancellationToken);
                cleanup.LastRun = run;
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            await schedulerFactory.TriggerJobAsync<StorageCleanupJob>();
            long pendingChunks = await dbContext.StorageCleanupChunks
                .LongCountAsync(x => x.ModuleId == request.ModuleId, cancellationToken);
            return new StorageCleanupDto
            {
                Id = cleanup.Id,
                CreatedAt = cleanup.CreatedAt,
                UpdatedAt = cleanup.UpdatedAt,
                ModuleId = module.Id,
                ModuleTag = module.Tag,
                Status = cleanup.Status,
                ScannedChunks = cleanup.ScannedChunks,
                PendingChunks = pendingChunks,
                TotalDeletedChunks = cleanup.TotalDeletedChunks,
                TotalReclaimedBytes = cleanup.TotalReclaimedBytes,
                LastStartedAt = cleanup.LastStartedAt,
                LastCompletedAt = cleanup.LastCompletedAt,
                ErrorMessage = cleanup.ErrorMessage,
            };
        }
    }
}
