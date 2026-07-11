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
                .FirstOrDefaultAsync(
                    x => x.Id == moduleId && x.UserId == userId,
                    HttpContext.RequestAborted);
            if (found is null)
            {
                return this.ApiNotFound("Module not found: " + moduleId);
            }

            bool tagExists = await _dbContext.Modules
                .AnyAsync(
                    x => x.UserId == userId &&
                        x.Tag == request.NewTag &&
                        x.Id != moduleId,
                    HttpContext.RequestAborted);
            if (tagExists)
            {
                return this.ApiConflict("Module with the same tag already exists: " + request.NewTag);
            }

            found.Tag = request.NewTag;
            await _dbContext.SaveChangesAsync(HttpContext.RequestAborted);
            return Ok(new { message = "Module renamed successfully." });
        }

        [Authorize]
        [HttpDelete("/api/v1/modules/{moduleId:guid}")]
        public async Task<IActionResult> DeleteUserBackupStorage([FromRoute] Guid moduleId)
        {
            Guid userId = User.GetUserId();
            Module? found = await _dbContext.Modules
                .FirstOrDefaultAsync(
                    x => x.Id == moduleId && x.UserId == userId,
                    HttpContext.RequestAborted);
            if (found is null)
            {
                return this.ApiNotFound("Module not found: " + moduleId);
            }

            _dbContext.Modules.Remove(found);
            await _dbContext.SaveChangesAsync(HttpContext.RequestAborted);
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
            if (!string.Equals(
                    foundProvider.Id,
                    request.BackupModuleId,
                    StringComparison.Ordinal))
            {
                return this.ApiBadRequest("Backup provider does not match the requested module provider.");
            }

            bool supportsDestination = request.Destination switch
            {
                ModuleDestination.Source =>
                    foundProvider is IBackupSource && foundProvider is not IBackupStorage,
                ModuleDestination.Target => foundProvider is IBackupStorage,
                _ => false
            };
            if (!supportsDestination)
            {
                return this.ApiBadRequest("Backup provider does not support the requested destination.");
            }

            var user = await _dbContext.Users.FindAsync(User.GetUserId()) ?? throw new InvalidOperationException("User not found");
            bool tagExists = await _dbContext.Modules.AnyAsync(
                x => x.UserId == user.Id && x.Tag == request.Tag,
                HttpContext.RequestAborted);
            if (tagExists)
            {
                return this.ApiConflict("Module with the same tag already exists: " + request.Tag);
            }

            Module newStorage = new()
            {
                UserId = user.Id,
                Tag = request.Tag,
                Destination = request.Destination,
                BackupModuleId = foundProvider.Id,
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
            if (!string.Equals(
                    foundProvider.Id,
                    request.BackupModuleId,
                    StringComparison.Ordinal))
            {
                return this.ApiBadRequest("Backup provider does not match the requested module provider.");
            }

            foundProvider.SetParameters(request.Parameters);
            if (foundProvider is not IBackupSource source || foundProvider is IBackupStorage)
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
            if (!string.Equals(
                    foundProvider.Id,
                    request.BackupModuleId,
                    StringComparison.Ordinal))
            {
                return this.ApiBadRequest("Backup provider does not match the requested module provider.");
            }

            foundProvider.SetParameters(request.Parameters);
            if (foundProvider is IBackupStorage storage && request.Destination == ModuleDestination.Target)
            {
                return await TestHelpers.TestStorageAsync(this, storage, _logger);
            }
            if (foundProvider is IBackupSource source &&
                foundProvider is not IBackupStorage &&
                request.Destination == ModuleDestination.Source)
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
                    "source" => provider is IBackupSource && provider is not IBackupStorage,
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
