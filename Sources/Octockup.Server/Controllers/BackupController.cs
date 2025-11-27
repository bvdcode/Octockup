using Mapster;
using EasyExtensions;
using Microsoft.AspNetCore.Mvc;
using Octockup.Server.Database;
using Octockup.Server.Models.Dto;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

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
                .Where(x => x.Source.UserId == userId)
                .ProjectToType<BackupDto>()
                .ToListAsync();
        }
    }
}
