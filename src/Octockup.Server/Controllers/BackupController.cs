// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions;
using EasyExtensions.AspNetCore.Extensions;
using EasyExtensions.Quartz.Extensions;
using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Octockup.Server.Database;
using Octockup.Server.Helpers;
using Octockup.Server.Jobs;
using Octockup.Server.Models.Dto;
using Octockup.Server.Models.Enums;
using Octockup.Server.Models.Requests;
using Octockup.Server.Models.Results;
using Octockup.Server.Services;
using Quartz;

namespace Octockup.Server.Controllers
{
    [ApiController]
    public class BackupController(
        AppDbContext _dbContext,
        ISchedulerFactory _schedulerFactory,
        BackupDeletionService _backupDeletionService,
        DownloadTicketService _downloadTickets,
        ServerBackupExportService _serverBackupExport,
        ServerBackupUploadService _serverBackupUpload,
        ILogger<BackupController> _logger) : ControllerBase
    {
        [AllowAnonymous]
        [HttpGet("/api/v1/backups/server")]
        public async Task<IActionResult> GetServerBackup(
            [FromQuery] string? ticket,
            CancellationToken ct = default)
        {
            DownloadTicketGrant? grant = await _downloadTickets
                .ConsumeServerBackupAsync(ticket, ct);
            if (grant is null)
            {
                return Unauthorized();
            }

            Guid userId = grant.UserId;
            bool includeFiles = grant.IncludeFiles;

            var user = await _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId, ct);

            if (user == null)
            {
                return this.ApiNotFound("User not found: " + userId);
            }

            Response.ContentType = "application/octet-stream";
            Response.Headers.ContentDisposition =
                $"attachment; filename=\"server-backup-{userId}.{CompressionHelpers.Extension}\"";

            Response.Headers.CacheControl = "no-store";
            Response.Headers.XContentTypeOptions = "nosniff";
            await _serverBackupExport.WriteAsync(
                userId,
                includeFiles,
                Response.Body,
                ct).ConfigureAwait(false);
            return new EmptyResult();
        }


        [Authorize]
        [HttpPatch("/api/v1/backups/{backupId:guid}/ignored-paths")]
        public async Task<IActionResult> UpdateIgnoredPaths([FromRoute] Guid backupId, [FromBody] List<string> ignoredPaths)
        {
            Guid userId = User.GetUserId();
            Backup? backup = await _dbContext.Backups.FirstOrDefaultAsync(
                x => x.Id == backupId && x.UserId == userId,
                HttpContext.RequestAborted);
            if (backup == null)
            {
                return this.ApiNotFound("Backup not found: " + backupId);
            }
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
            backup.IgnoredPaths = ignoredPaths;
            _dbContext.Backups.Update(backup);
            await _dbContext.SaveChangesAsync();
            return Ok(new { message = "Ignored paths updated successfully." });
        }

        [Authorize]
        [HttpPatch("/api/v1/backups/{backupId:guid}/rename")]
        public async Task<IActionResult> RenameBackup([FromRoute] Guid backupId, [FromBody] RenameModuleRequest request)
        {
            Guid userId = User.GetUserId();
            Backup? backup = await _dbContext.Backups.FirstOrDefaultAsync(
                x => x.Id == backupId && x.UserId == userId,
                HttpContext.RequestAborted);
            if (backup == null)
            {
                return this.ApiNotFound("Backup not found: " + backupId);
            }
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
            var existsTag = await _dbContext.Backups.AnyAsync(
                b => b.UserId == userId &&
                    b.Tag == request.NewTag &&
                    b.Id != backupId);
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
                .Include(x => x.Schedules)
                .Where(x => x.UserId == userId)
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
            var existsTag = await _dbContext.Backups.AnyAsync(
                b => b.UserId == userId && b.Tag == request.Tag);
            if (existsTag)
            {
                return this.ApiBadRequest("Tag already exists: " + request.Tag);
            }
            Backup backup = new()
            {
                UserId = userId,
                Tag = request.Tag,
                SourceId = source.Id,
                StorageId = storage.Id,
                IgnoredPaths = request.IgnoredPaths ?? [],
                DisableCompression = request.DisableCompression,
                DisableEncryption = request.DisableEncryption
            };
            await _dbContext.Backups.AddAsync(backup);
            await _dbContext.SaveChangesAsync();
            return Ok(new { message = "Backup created successfully." });
        }

        [Authorize]
        [HttpDelete("/api/v1/backups/{backupId:guid}")]
        public async Task<IActionResult> DeleteBackup([FromRoute] Guid backupId)
        {
            var result = await _backupDeletionService.DeleteAsync(
                User.GetUserId(),
                backupId,
                HttpContext.RequestAborted);

            if (result.Deleted)
            {
                return Ok(result);
            }

            return this.ApiBadRequest(result.ErrorMessage ?? "Backup could not be deleted.");
        }

        [Authorize]
        [DisableRequestSizeLimit]
        [Consumes("application/octet-stream")]
        [HttpPost("/api/v1/backups/server/import")]
        public async Task<IActionResult> ImportServerBackup(CancellationToken ct)
        {
            Guid userId = User.GetUserId();
            var user = await _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId, ct);

            if (user == null)
            {
                return this.ApiNotFound("User not found: " + userId);
            }

            ServerBackupUploadResult upload = await _serverBackupUpload
                .SaveAsync(userId, Request.Body, Request.ContentLength, ct)
                .ConfigureAwait(false);
            if (upload.Status == ServerBackupUploadStatus.Empty)
            {
                return this.ApiBadRequest("File is required");
            }

            if (upload.Status == ServerBackupUploadStatus.TooLarge)
            {
                return Problem(
                    statusCode: StatusCodes.Status413PayloadTooLarge,
                    title: "Server backup import is too large",
                    detail: "The upload exceeds the configured server backup import limit.");
            }

            _logger.LogInformation(
                "User {UserId} uploaded a server backup import with {FileSize} bytes.",
                userId,
                upload.BytesWritten);

            await _schedulerFactory.TriggerJobAsync<ImportBackupJob>();

            return Ok(new { message = "Import file uploaded successfully. Processing will begin shortly." });
        }
    }
}
