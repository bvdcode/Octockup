// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;
using Octockup.Server.Database;
using Octockup.Server.Models.Dto;

namespace Octockup.Server.Handlers.Administration
{
    public class GetStorageCleanupRunsQueryHandler(AppDbContext dbContext)
        : IRequestHandler<GetStorageCleanupRunsQuery, IReadOnlyCollection<StorageCleanupRunDto>>
    {
        private const int MaximumRuns = 100;

        public async Task<IReadOnlyCollection<StorageCleanupRunDto>> Handle(
            GetStorageCleanupRunsQuery request,
            CancellationToken cancellationToken)
        {
            int limit = Math.Clamp(request.Limit, 1, MaximumRuns);
            return await dbContext.StorageCleanupRuns
                .AsNoTracking()
                .OrderByDescending(x => x.StartedAt)
                .Take(limit)
                .Select(x => new StorageCleanupRunDto
                {
                    Id = x.Id,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt,
                    ModuleId = x.ModuleId,
                    ModuleTag = x.Module.Tag,
                    Status = x.Status,
                    StartedAt = x.StartedAt,
                    CompletedAt = x.CompletedAt,
                    ScannedChunks = x.ScannedChunks,
                    DeletedChunks = x.DeletedChunks,
                    ReclaimedBytes = x.ReclaimedBytes,
                    ErrorMessage = x.ErrorMessage,
                })
                .ToListAsync(cancellationToken);
        }
    }
}
