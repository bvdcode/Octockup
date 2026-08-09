// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using EasyExtensions.Quartz.Extensions;
using Microsoft.EntityFrameworkCore;
using Octockup.Server.Database;
using Octockup.Server.Jobs;
using Octockup.Server.Models.Enums;
using Octockup.Server.Services;
using Quartz;

namespace Octockup.Server.Handlers.Administration
{
    public class SetStorageCleanupSpeedCommandHandler(
        AppDbContext dbContext,
        ISchedulerFactory schedulerFactory)
        : IRequestHandler<SetStorageCleanupSpeedCommand, StorageCleanupSpeed>
    {
        public async Task<StorageCleanupSpeed> Handle(
            SetStorageCleanupSpeedCommand request,
            CancellationToken cancellationToken)
        {
            if (!Enum.IsDefined(request.Speed))
            {
                throw new AuthApiException(
                    StatusCodes.Status400BadRequest,
                    $"Invalid storage cleanup speed: {request.Speed}");
            }

            bool moduleExists = await dbContext.Modules
                .AsNoTracking()
                .AnyAsync(
                    x => x.Id == request.ModuleId && x.Destination == ModuleDestination.Target,
                    cancellationToken);
            if (!moduleExists)
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
                    Speed = request.Speed,
                };
                await dbContext.StorageCleanups.AddAsync(cleanup, cancellationToken);
            }
            else
            {
                cleanup.Speed = request.Speed;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            if (cleanup.Status == StorageCleanupStatus.Running)
            {
                await schedulerFactory.TriggerJobAsync<StorageCleanupJob>();
            }

            return cleanup.Speed;
        }
    }
}
