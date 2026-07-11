// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions;
using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Octockup.Server.Database;
using Octockup.Server.Models.Dto;
using Octockup.Server.Models.Enums;

namespace Octockup.Server.Controllers
{
    [ApiController]
    public class StatsController(AppDbContext _dbContext) : ControllerBase
    {
        [Authorize]
        [HttpGet("/api/v1/stats")]
        public async Task<IActionResult> GetStats(CancellationToken cancellationToken)
        {
            int totalUsers = await _dbContext.Users.CountAsync(cancellationToken);
            Guid userId = User.GetUserId();
            List<Module> storages = await _dbContext.Modules
                .AsNoTracking()
                .Where(m => m.UserId == userId && m.Destination == ModuleDestination.Target)
                .ToListAsync(cancellationToken);

            List<StorageStatsDto> storageStats = [];
            foreach (Module storage in storages)
            {
                StorageStatsDto stats = storage.Adapt<StorageStatsDto>();
                stats.TotalBackups = await _dbContext.Backups
                    .Where(b => b.StorageId == storage.Id)
                    .CountAsync(cancellationToken);

                var chunkSizes = await _dbContext.UploadedHashes
                    .AsNoTracking()
                    .Where(c => c.ModuleId == storage.Id)
                    .GroupBy(c => c.ModuleId)
                    .Select(g => new
                    {
                        TotalOriginalSize = g.Sum(c => (long?)c.OriginalSize) ?? 0,
                        TotalStoredSize = g.Sum(c => (long?)c.StoredSize) ?? 0
                    })
                    .FirstOrDefaultAsync(cancellationToken);

                stats.TotalOriginalSize = chunkSizes?.TotalOriginalSize ?? 0;
                stats.TotalStoredSize = chunkSizes?.TotalStoredSize ?? 0;

                StorageCleanupJob? latestCompletedCleanup = await _dbContext.StorageCleanupJobs
                    .AsNoTracking()
                    .Where(x =>
                        x.UserId == userId &&
                        x.StorageId == storage.Id &&
                        x.Status == StorageCleanupStatus.Completed)
                    .OrderByDescending(x => x.FinishedAt)
                    .ThenByDescending(x => x.StartedAt)
                    .FirstOrDefaultAsync(cancellationToken);
                if (latestCompletedCleanup is not null)
                {
                    stats.DeduplicatedChunks = Math.Max(
                        0,
                        latestCompletedCleanup.ReferenceCount -
                            latestCompletedCleanup.ReferencedChunks);
                }
                storageStats.Add(stats);
            }
            return Ok(new StatsDto
            {
                TotalUsers = totalUsers,
                StorageStats = storageStats
            });
        }
    }
}
