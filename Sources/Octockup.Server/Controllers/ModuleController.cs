using EasyExtensions;
using Microsoft.AspNetCore.Mvc;
using Octockup.Server.Database;
using Octockup.Server.Models.Enums;
using Octockup.Server.Abstractions;
using Octockup.Server.Models.Requests;
using Microsoft.AspNetCore.Authorization;
using EasyExtensions.AspNetCore.Extensions;
using Octockup.Server.Models.Dto;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Mapster;
using Octockup.Server.Helpers;

namespace Octockup.Server.Controllers
{
    [ApiController]
    public class ModuleController(
        AppDbContext _dbContext,
        IEnumerable<IBackupModule> _modules) : ControllerBase
    {
        [Authorize]
        [HttpDelete("/api/v1/modules/{moduleId:guid}")]
        public async Task<IActionResult> DeleteUserBackupStorage([FromRoute] Guid moduleId)
        {
            var found = await _dbContext.Modules.FindAsync(moduleId);
            if (found == null)
            {
                return this.ApiNotFound("Module not found: " + moduleId);
            }
            _dbContext.Modules.Remove(found);
            await _dbContext.SaveChangesAsync();
            return Ok(new { message = "Module deleted successfully." });
        }

        [Authorize]
        [HttpPost("/api/v1/modules")]
        public IActionResult CreateBackupStorage([FromRoute] string backupStorageId, [FromBody] CreateModuleRequest request)
        {
            var foundStorage = _backupStorages.FirstOrDefault(x => x.Id == backupStorageId);
            if (foundStorage == null)
            {
                return this.ApiNotFound("Backup storage not found: " + backupStorageId);
            }
            var user = _userDataStorage.GetUser(User.GetUserName());
            Module newStorage = new()
            {
                UserId = user.Id,
                Tag = request.Tag,
                CreatedAt = DateTime.UtcNow,
                Parameters = request.Parameters,
                BackupModuleId = foundStorage.Id,
            };
            _userDataStorage.AddSavedStorage(newStorage);
            return Ok(new { message = "Backup storage created successfully." });
        }

        [Authorize]
        [HttpPost("/api/v1/modules/{backupStorageId:required}/directories")]
        public IActionResult GetBackupStorageDirectories([FromRoute] string backupStorageId, [FromBody] CreateModuleRequest request)
        {
            var foundStorage = _backupStorages.FirstOrDefault(x => x.Id == backupStorageId);
            if (foundStorage == null)
            {
                return this.ApiNotFound("Backup storage not found: " + backupStorageId);
            }

            foundStorage.SetParameters(request.Parameters);
            try
            {
                var result = foundStorage.GetDirectories(recursive: false);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return this.ApiBadRequest("Failed to connect to backup storage with provided parameters: " + ex.Message);
            }
        }

        [Authorize]
        [HttpPost("/api/v1/modules/{backupStorageId:required}/test")]
        public async Task<IActionResult> TestBackupStorage([FromRoute] string backupStorageId, [FromBody] CreateModuleRequest request)
        {
            var foundModule = _modules.FirstOrDefault(x => x.Id == backupStorageId);
            if (foundModule == null)
            {
                return this.ApiNotFound("Backup storage not found: " + backupStorageId);
            }

            foundModule.SetParameters(request.Parameters);
            if (foundModule is IBackupStorage storage)
            {
                return await TestHelpers.TestStorageAsync(this, storage);
            }
            if (foundModule is IBackupSource source)
            {
                return await TestHelpers.TestSourceAsync(this, source);
            }
            return this.ApiBadRequest("Module is not able to pass backup storage/source tests.");
        }

        [Authorize]
        [HttpGet("/api/v1/modules")]
        public async Task<IEnumerable<ModuleDto>> GetUserBackupStorages()
        {
            Guid userId = User.GetUserId();
            return await _dbContext.Modules
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .ProjectToType<ModuleDto>()
                .ToListAsync();
        }

        [Authorize]
        [HttpGet("/api/v1/modules/available")]
        public IEnumerable<AvailableModuleInfo> GetBackupStorages()
        {
            return _modules.Select(x => new AvailableModuleInfo()
            {
                Name = x.Name,
                Id = x.GetType().FullName,
                PathSeparator = x.PathSeparator,
                RequiredParameters = x.RequiredParameters,
            });
        }
    }
}
