// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.Mediator;
using EasyExtensions.Mediator.Contracts;
using Microsoft.EntityFrameworkCore;
using Octockup.Server.Database;
using Octockup.Server.Models.Dto;
using Octockup.Server.Models.Enums;

namespace Octockup.Server.Handlers.Administration
{
    public class GetStorageCleanupsQueryHandler(AppDbContext dbContext)
        : IRequestHandler<GetStorageCleanupsQuery, IReadOnlyCollection<StorageCleanupDto>>
    {
        public async Task<IReadOnlyCollection<StorageCleanupDto>> Handle(
            GetStorageCleanupsQuery request,
            CancellationToken cancellationToken)
        {
            List<Module> modules = await dbContext.Modules
                .AsNoTracking()
                .Where(x => x.Destination == ModuleDestination.Target)
                .OrderBy(x => x.Tag)
                .ToListAsync(cancellationToken);
            Dictionary<Guid, StorageCleanup> cleanups = await dbContext.StorageCleanups
                .AsNoTracking()
                .ToDictionaryAsync(x => x.ModuleId, cancellationToken);
            Dictionary<Guid, long> pendingChunks = await dbContext.StorageCleanupChunks
                .AsNoTracking()
                .GroupBy(x => x.ModuleId)
                .Select(x => new { ModuleId = x.Key, Count = x.LongCount() })
                .ToDictionaryAsync(x => x.ModuleId, x => x.Count, cancellationToken);

            List<StorageCleanupDto> result = [];
            foreach (Module module in modules)
            {
                cleanups.TryGetValue(module.Id, out StorageCleanup? cleanup);
                pendingChunks.TryGetValue(module.Id, out long pending);
                result.Add(CreateDto(module, cleanup, pending));
            }
            return result;
        }

        private static StorageCleanupDto CreateDto(
            Module module,
            StorageCleanup? cleanup,
            long pendingChunks)
        {
            return new StorageCleanupDto
            {
                Id = cleanup?.Id ?? Guid.Empty,
                CreatedAt = cleanup?.CreatedAt ?? module.CreatedAt,
                UpdatedAt = cleanup?.UpdatedAt ?? module.UpdatedAt,
                ModuleId = module.Id,
                ModuleTag = module.Tag,
                Status = cleanup?.Status ?? StorageCleanupStatus.Idle,
                Speed = cleanup?.Speed ?? StorageCleanupSpeed.Normal,
                ScannedChunks = cleanup?.ScannedChunks ?? 0,
                PendingChunks = pendingChunks,
                TotalDeletedChunks = cleanup?.TotalDeletedChunks ?? 0,
                TotalReclaimedBytes = cleanup?.TotalReclaimedBytes ?? 0,
                LastStartedAt = cleanup?.LastStartedAt,
                LastCompletedAt = cleanup?.LastCompletedAt,
                ErrorMessage = cleanup?.ErrorMessage,
            };
        }
    }
}
