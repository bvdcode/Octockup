using Mapster;
using EasyExtensions;
using Microsoft.AspNetCore.Mvc;
using Octockup.Server.Database;
using Octockup.Server.Models.Dto;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Octockup.Server.Models.Requests;
using Octockup.Server.Models.Enums;
using EasyExtensions.AspNetCore.Extensions;

namespace Octockup.Server.Controllers
{
    [ApiController]
    public class BackupController(AppDbContext _dbContext) : ControllerBase
    {
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
