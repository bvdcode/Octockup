// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using Mapster;
using EasyExtensions;
using Microsoft.AspNetCore.Mvc;
using Octockup.Server.Database;
using Octockup.Server.Models.Dto;
using Octockup.Server.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Octockup.Server.Models.Requests;
using Microsoft.AspNetCore.Authorization;
using EasyExtensions.AspNetCore.Extensions;

namespace Octockup.Server.Controllers
{
    [ApiController]
    public class BackupController(AppDbContext _dbContext) : ControllerBase
    {
        [Authorize]
        [HttpPatch("/api/v1/backups/{backupId:guid}/rename")]
        public async Task<IActionResult> RenameBackup([FromRoute] Guid backupId, [FromBody] RenameModuleRequest request)
        {
            var backup = await _dbContext.Backups.FindAsync(backupId);
            if (backup == null)
            {
                return this.ApiNotFound("Backup not found: " + backupId);
            }
            var userId = User.GetUserId();
            var source = await _dbContext.Modules.FirstOrDefaultAsync(m => m.Id == backup.SourceId && m.UserId == userId && m.Destination == ModuleDestination.Source);
            if (source == null)
            {
                return this.ApiNotFound("Source module not found for backup: " + backupId);
            }
            var storage = await _dbContext.Modules.FirstOrDefaultAsync(m => m.Id == backup.StorageId && m.UserId == userId && m.Destination == ModuleDestination.Target);
            if (storage == null)
            {
                return this.ApiNotFound("Storage module not found for backup: " + backupId);
            }
            var existsTag = await _dbContext.Backups.AnyAsync(b => b.Tag == request.NewTag && b.Id != backupId);
            if (existsTag)
            {
                return this.ApiBadRequest("Tag already exists: " + request.NewTag);
            }
            backup.Tag = request.NewTag;
            _dbContext.Backups.Update(backup);
            await _dbContext.SaveChangesAsync();
            return Ok(new { message = "Backup renamed successfully." });
        }

        [Authorize]
        [HttpGet("/api/v1/backups")]
        public async Task<IEnumerable<BackupDto>> GetUserBackups()
        {
            var userId = User.GetUserId();
            return await _dbContext.Backups
                .AsNoTracking()
                .Include(x => x.Source)
                .Include(x => x.Storage)
                .Include(x => x.Snapshots)
                .Where(x => x.Source.UserId == userId)
                .ProjectToType<BackupDto>()
                .ToListAsync();
        }

        [Authorize]
        [HttpPost("/api/v1/backups")]
        public async Task<IActionResult> CreateBackup([FromBody] CreateBackupRequest request)
        {
            var userId = User.GetUserId();
            var source = await _dbContext.Modules.FirstOrDefaultAsync(m => m.Id == request.SourceId && m.UserId == userId && m.Destination == ModuleDestination.Source);
            if (source == null)
            {
                return this.ApiNotFound("Source module not found: " + request.SourceId);
            }
            var storage = await _dbContext.Modules.FirstOrDefaultAsync(m => m.Id == request.StorageId && m.UserId == userId && m.Destination == ModuleDestination.Target);
            if (storage == null)
            {
                return this.ApiNotFound("Storage module not found: " + request.StorageId);
            }
            var existsTag = await _dbContext.Backups.AnyAsync(b => b.Tag == request.Tag);
            if (existsTag)
            {
                return this.ApiBadRequest("Tag already exists: " + request.Tag);
            }
            Backup backup = new()
            {
                Tag = request.Tag,
                SourceId = source.Id,
                StorageId = storage.Id,
                IgnoredPaths = request.IgnoredPaths ?? []
            };
            await _dbContext.Backups.AddAsync(backup);
            await _dbContext.SaveChangesAsync();
            return Ok(new { message = "Backup created successfully." });
        }

        [Authorize]
        [HttpDelete("/api/v1/backups/{backupId:guid}")]
        public async Task<IActionResult> DeleteBackup([FromRoute] Guid backupId)
        {
            var backup = await _dbContext.Backups.FindAsync(backupId);
            if (backup == null)
            {
                return this.ApiNotFound("Backup not found: " + backupId);
            }
            _dbContext.Backups.Remove(backup);
            await _dbContext.SaveChangesAsync();
            return Ok(new { message = "Backup deleted successfully." });
        }
    }
}
