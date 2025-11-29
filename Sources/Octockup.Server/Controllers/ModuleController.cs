// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using Mapster;
using EasyExtensions;
using Octockup.Server.Helpers;
using Microsoft.AspNetCore.Mvc;
using Octockup.Server.Database;
using Octockup.Server.Models.Dto;
using Octockup.Server.Abstractions;
using Microsoft.EntityFrameworkCore;
using Octockup.Server.Models.Requests;
using Microsoft.AspNetCore.Authorization;
using EasyExtensions.AspNetCore.Extensions;
using Octockup.Server.Models.Enums;

namespace Octockup.Server.Controllers
{
    [ApiController]
    public class ModuleController(
        AppDbContext _dbContext,
        IEnumerable<IBackupProvider> _providers) : ControllerBase
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
                Parameters = request.Parameters,
                Destination = request.Destination,
                BackupModuleId = request.BackupModuleId,
            };
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
                return await TestHelpers.TestStorageAsync(this, storage);
            }
            if (foundProvider is IBackupSource source)
            {
                return await TestHelpers.TestSourceAsync(this, source);
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
