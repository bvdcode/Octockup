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
        public IActionResult GetStats()
        {
            int totalUsers = _dbContext.Users.Count();
            Guid userId = User.GetUserId();
            var storages = _dbContext.Modules.Where(m => m.UserId == userId && m.Destination == ModuleDestination.Target);
            HashSet<string> chunkHashes = [];
            List<StorageStatsDto> storageStats = [];
            foreach (var storage in storages)
            {
                StorageStatsDto stats = storage.Adapt<StorageStatsDto>();
                var backups = _dbContext.Backups.Where(b => b.StorageId == storage.Id);
                var chunks = _dbContext.UploadedHashes.Where(c => c.ModuleId == storage.Id);
                stats.TotalBackups = backups.Count();
                stats.TotalOriginalSize = chunks.Sum(c => c.OriginalSize);
                stats.TotalStoredSize = chunks.Sum(c => c.StoredSize);

                foreach (var backup in backups)
                {
                    var snapshots = _dbContext.Snapshots
                        .Include(x => x.Files)
                        .OrderBy(x => x.CreatedAt)
                        .Where(s => s.BackupId == backup.Id);
                    foreach (var snapshot in snapshots)
                    {
                        foreach (var file in snapshot.Files)
                        {
                            foreach (var chunkHash in file.ChunkHashes)
                            {
                                bool added = chunkHashes.Add(chunkHash);
                                if (!added)
                                {
                                    stats.DeduplicatedChunks++;
                                }
                            }
                        }
                    }
                }
                storageStats.Add(stats);
            }
            return Ok(new
            {
                TotalUsers = totalUsers,
                StorageStats = storageStats
            });
        }
    }
}
