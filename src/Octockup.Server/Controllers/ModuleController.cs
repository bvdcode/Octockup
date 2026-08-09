// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions;
using EasyExtensions.Abstractions;
using EasyExtensions.AspNetCore.Extensions;
using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Octockup.Server.Abstractions;
using Octockup.Server.Database;
using Octockup.Server.Helpers;
using Octockup.Server.Models.Dto;
using Octockup.Server.Models.Enums;
using Octockup.Server.Models.Requests;

namespace Octockup.Server.Controllers
{
    [ApiController]
    public class ModuleController(
        IStreamCipher _crypto,
        AppDbContext _dbContext,
        ILogger<ModuleController> _logger,
        IEnumerable<IBackupProvider> _providers) : ControllerBase
    {
        [Authorize]
        [HttpPatch("/api/v1/modules/{moduleId:guid}/rename")]
        public async Task<IActionResult> RenameModule([FromRoute] Guid moduleId, [FromBody] RenameModuleRequest request)
        {
            Guid userId = User.GetUserId();
            Module? found = await _dbContext.Modules
                .FirstOrDefaultAsync(x => x.Id == moduleId && x.UserId == userId);
            if (found == null)
            {
                return this.ApiNotFound("Module not found: " + moduleId);
            }
            bool tagExists = await _dbContext.Modules
                .AnyAsync(x => x.UserId == found.UserId && x.Tag == request.NewTag && x.Id != moduleId);
            if (tagExists)
            {
                return this.ApiConflict("Module with the same tag already exists: " + request.NewTag);
            }
            found.Tag = request.NewTag;
            await _dbContext.SaveChangesAsync();
            return Ok(new { message = "Module renamed successfully." });
        }

        [Authorize]
        [HttpDelete("/api/v1/modules/{moduleId:guid}")]
        public async Task<IActionResult> DeleteUserBackupStorage([FromRoute] Guid moduleId)
        {
            Guid userId = User.GetUserId();
            Module? found = await _dbContext.Modules
                .FirstOrDefaultAsync(x => x.Id == moduleId && x.UserId == userId);
            if (found == null)
            {
                return this.ApiNotFound("Module not found: " + moduleId);
            }
            bool cleanupInProgress = await _dbContext.StorageCleanups.AnyAsync(
                x => x.ModuleId == moduleId && x.Status == StorageCleanupStatus.Running);
            bool hasPendingCleanupChunks = await _dbContext.StorageCleanupChunks.AnyAsync(
                x => x.ModuleId == moduleId);
            if (cleanupInProgress || hasPendingCleanupChunks)
            {
                return this.ApiConflict("Storage cleanup is still in progress: " + moduleId);
            }

            await _dbContext.StorageCleanups
                .Where(x => x.ModuleId == moduleId)
                .ExecuteDeleteAsync();
            await _dbContext.StorageCleanupRuns
                .Where(x => x.ModuleId == moduleId)
                .ExecuteDeleteAsync();
            await _dbContext.Modules
                .Where(x => x.Id == moduleId)
                .ExecuteDeleteAsync();
            return Ok(new { message = "Module deleted successfully." });
        }

        [Authorize]
        [HttpPost("/api/v1/modules/providers/{backupProviderId:required}")]
        public async Task<IActionResult> CreateBackupModule([FromRoute] string backupProviderId, [FromBody] CreateModuleRequest request)
        {
            var foundProvider = _providers.FirstOrDefault(x => x.Id == backupProviderId);
            if (foundProvider == null)
            {
                return this.ApiNotFound("Backup provider not found: " + backupProviderId);
            }

            var user = await _dbContext.Users.FindAsync(User.GetUserId()) ?? throw new InvalidOperationException("User not found");
            Module newStorage = new()
            {
                UserId = user.Id,
                Tag = request.Tag,
                Destination = request.Destination,
                BackupModuleId = request.BackupModuleId,
            };
            foreach (var item in request.Parameters)
            {
                newStorage.Params(_crypto)[item.Key] = item.Value;
            }
            await _dbContext.Modules.AddAsync(newStorage);
            await _dbContext.SaveChangesAsync();
            return Ok(new { message = "Backup storage created successfully." });
        }

        [Authorize]
        [HttpPost("/api/v1/modules/providers/{backupProviderId:required}/directories")]
        public IActionResult GetBackupProviderDirectories([FromRoute] string backupProviderId, [FromBody] CreateModuleRequest request)
        {
            var foundProvider = _providers.FirstOrDefault(x => x.Id == backupProviderId);
            if (foundProvider == null)
            {
                return this.ApiNotFound("Backup provider not found: " + backupProviderId);
            }

            foundProvider.SetParameters(request.Parameters);
            if (foundProvider is not IBackupSource source)
            {
                return this.ApiBadRequest("Provider cannot provide directories");
            }
            try
            {
                var result = source.GetDirectories(recursive: false);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return this.ApiBadRequest("Failed to connect to backup provider with provided parameters: " + ex.Message);
            }
        }

        [Authorize]
        [HttpPost("/api/v1/modules/providers/{backupProviderId:required}/test")]
        public async Task<IActionResult> TestBackupProvider(
            [FromRoute] string backupProviderId,
            [FromBody] CreateModuleRequest request)
        {
            var foundProvider = _providers.FirstOrDefault(x => x.Id == backupProviderId);
            if (foundProvider == null)
            {
                return this.ApiNotFound("Backup provider not found: " + backupProviderId);
            }

            foundProvider.SetParameters(request.Parameters);
            if (foundProvider is IBackupStorage storage && request.Destination == ModuleDestination.Target)
            {
                return await TestHelpers.TestStorageAsync(this, storage, _logger);
            }
            if (foundProvider is IBackupSource source)
            {
                return await TestHelpers.TestSourceAsync(this, source, _logger);
            }
            return this.ApiBadRequest("Provider is not able to pass backup storage/source tests.");
        }

        [Authorize]
        [HttpGet("/api/v1/modules")]
        public async Task<IEnumerable<ModuleDto>> GetUserModules()
        {
            Guid userId = User.GetUserId();
            return await _dbContext.Modules
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .ProjectToType<ModuleDto>()
                .ToListAsync();
        }

        [Authorize]
        [HttpGet("/api/v1/modules/providers/{type:required}")]
        public IEnumerable<ProviderInfo> GetBackupProviders([FromRoute] string type)
        {
            return _providers.Where(provider =>
            {
                return type.ToLower() switch
                {
                    "storage" => provider is IBackupStorage,
                    "source" => provider is IBackupSource,
                    _ => false,
                };
            })
            .Select(x => new ProviderInfo()
            {
                Name = x.Name,
                Id = x.GetType().FullName,
                PathSeparator = x.PathSeparator,
                RequiredParameters = x.RequiredParameters,
            });
        }
    }
}
