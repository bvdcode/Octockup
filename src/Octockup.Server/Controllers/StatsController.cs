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
            IQueryable<Module> storages = _dbContext.Modules.Where(m => m.UserId == userId && m.Destination == ModuleDestination.Target);
            HashSet<string> chunkHashes = [];
            List<StorageStatsDto> storageStats = [];
            foreach (Module? storage in storages)
            {
                StorageStatsDto stats = storage.Adapt<StorageStatsDto>();
                IQueryable<Backup> backups = _dbContext.Backups.Where(b => b.StorageId == storage.Id);
                IQueryable<UploadedHash> chunks = _dbContext.UploadedHashes.Where(c => c.ModuleId == storage.Id);
                stats.TotalBackups = backups.Count();
                stats.TotalOriginalSize = chunks.Sum(c => c.OriginalSize);
                stats.TotalStoredSize = chunks.Sum(c => c.StoredSize);

                foreach (Backup? backup in backups)
                {
                    IQueryable<Snapshot> snapshots = _dbContext.Snapshots
                        .Include(x => x.Files)
                        .OrderBy(x => x.CreatedAt)
                        .Where(s => s.BackupId == backup.Id);
                    foreach (Snapshot? snapshot in snapshots)
                    {
                        foreach (SnapshotFile file in snapshot.Files)
                        {
                            foreach (string chunkHash in file.ChunkHashes)
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
