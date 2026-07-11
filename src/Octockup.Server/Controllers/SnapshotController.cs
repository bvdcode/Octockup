// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.Abstractions;
using EasyExtensions;
using EasyExtensions.AspNetCore.Extensions;
using EasyExtensions.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using Octockup.Server.Abstractions;
using Octockup.Server.Database;
using Octockup.Server.Helpers;
using Octockup.Server.Models.Dto;
using Octockup.Server.Models.Requests;
using Octockup.Server.Models.Results;
using Octockup.Server.Services;
using Octockup.Server.Streams;

namespace Octockup.Server.Controllers
{
    [ApiController]
    public class SnapshotController(
        IStreamCipher _crypto,
        AppDbContext _dbContext,
        SnapshotDeletionService _snapshotDeletionService,
        SnapshotFilePageService _snapshotFilePages,
        DownloadTicketService _downloadTickets,
        ILogger<SnapshotController> _logger,
        IEnumerable<IBackupProvider> _providers) : ControllerBase
    {
        [AllowAnonymous]
        [HttpGet("/api/v1/snapshots/{snapshotId:guid}/files/{fileId:guid}/download")]
        public async Task<IActionResult> DownloadSnapshotFile(
            [FromRoute] Guid snapshotId,
            [FromRoute] Guid fileId,
            [FromQuery] string? ticket)
        {
            DownloadTicketGrant? grant = await _downloadTickets
                .ConsumeSnapshotFileAsync(
                    ticket,
                    snapshotId,
                    fileId,
                    HttpContext.RequestAborted);
            if (grant is null)
            {
                return Unauthorized();
            }

            Guid userId = grant.UserId;

            var snapshotFile = await _dbContext.SnapshotFiles
                .AsNoTracking()
                .Include(sf => sf.Snapshot)
                    .ThenInclude(s => s.Backup)
                        .ThenInclude(b => b.Source)
                .Include(sf => sf.Snapshot)
                    .ThenInclude(s => s.Backup)
                        .ThenInclude(b => b.Storage)
                .FirstOrDefaultAsync(
                    sf => sf.SnapshotId == snapshotId &&
                        sf.Id == fileId &&
                        sf.Snapshot.Backup.Source.UserId == userId);

            if (snapshotFile == null)
            {
                return NotFound();
            }

            var provider = _providers
                .FirstOrDefault(p => p.Id == snapshotFile.Snapshot.Backup.Storage.BackupModuleId);

            if (provider == null)
            {
                return NotFound();
            }

            provider.SetParameters(snapshotFile.Snapshot.Backup.Storage.Params(_crypto).Snapshot());
            if (provider is not IBackupStorage storage)
            {
                return BadRequest("Storage provider is not a backup storage");
            }

            var chunkKeys = snapshotFile.ChunkHashes?.ToList() ?? [];
            var uploadedHashes = await _dbContext.UploadedHashes
                .AsNoTracking()
                .Where(x => x.ModuleId == snapshotFile.Snapshot.Backup.StorageId && chunkKeys.Contains(x.Hash))
                .ToDictionaryAsync(x => x.Hash);

            List<ChunkStorageDescriptor> chunks = [];
            foreach (var chunkKey in chunkKeys)
            {
                if (uploadedHashes.TryGetValue(chunkKey, out var found))
                {
                    chunks.Add(ChunkStorageHelpers.Parse(found.Hash, found.CompressionAlgorithm, found.OriginalSize));
                    continue;
                }

                _logger.LogWarning("Chunk hash metadata not found in DB: {ChunkKey}", chunkKey);
                try
                {
                    chunks.Add(ChunkStorageHelpers.Parse(chunkKey));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unsupported chunk key metadata: {ChunkKey}", chunkKey);
                    return BadRequest("Unsupported chunk metadata.");
                }
            }

            long restoredSize = GetRestoredFileSize(snapshotFile, chunks);
            var stream = new SnapshotConcatStream(
                _logger,
                storage,
                chunks,
                snapshotFile,
                _crypto,
                HttpContext.RequestAborted,
                restoredSize
            );

            string contentType = MimeTypes.GetMimeType(snapshotFile.Name) ?? "application/octet-stream";
            var result = new FileStreamResult(stream, contentType)
            {
                FileDownloadName = snapshotFile.Name,
                EnableRangeProcessing = false,
            };

            Response.ContentLength = restoredSize;
            return result;
        }

        [Authorize]
        [HttpGet("/api/v1/snapshots/{snapshotId:guid}/files")]
        public async Task<IActionResult> GetSnapshotFiles(
            [FromRoute] Guid snapshotId,
            [FromQuery] SnapshotFilePageRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                SnapshotFilePageDto? page = await _snapshotFilePages.GetPageAsync(
                    User.GetUserId(),
                    snapshotId,
                    request,
                    cancellationToken);
                if (page is null)
                {
                    return NotFound();
                }

                return Ok(page);
            }
            catch (FormatException)
            {
                return this.ApiBadRequest("Snapshot file cursor is invalid.");
            }
        }

        [Authorize]
        [HttpDelete("/api/v1/snapshots/{snapshotId:guid}")]
        public async Task<IActionResult> DeleteSnapshot([FromRoute] Guid snapshotId)
        {
            SnapshotDeletionResult result = await _snapshotDeletionService.DeleteAsync(
                User.GetUserId(),
                snapshotId,
                HttpContext.RequestAborted);

            if (result.Deleted)
            {
                return Ok(result);
            }

            return this.ApiBadRequest(result.ErrorMessage ?? "Snapshot could not be deleted.");
        }

        [Authorize]
        [HttpGet("/api/v1/snapshots")]
        public async Task<IActionResult> GetSnapshots([FromQuery] Guid backupId, CancellationToken cancellationToken)
        {
            Guid userId = User.GetUserId();

            bool backupExists = await _dbContext.Backups
                .AsNoTracking()
                .AnyAsync(
                    b => b.Id == backupId && b.Source.UserId == userId,
                    cancellationToken);

            if (!backupExists)
            {
                return NotFound();
            }

            List<SnapshotDto> result = await _dbContext.Snapshots
                .AsNoTracking()
                .Where(s => s.BackupId == backupId && s.Backup.Source.UserId == userId)
                .OrderBy(s => s.CreatedAt)
                .Select(s => new SnapshotDto
                {
                    Id = s.Id,
                    BackupId = s.BackupId,
                    CompletedAt = s.CompletedAt,
                    FilesCount = _dbContext.SnapshotFiles.Count(sf => sf.SnapshotId == s.Id),
                    TotalSize = _dbContext.SnapshotFiles
                        .Where(sf => sf.SnapshotId == s.Id)
                        .Sum(sf => (long?)sf.Size) ?? 0
                })
                .ToListAsync(cancellationToken);

            return Ok(result);
        }

        private static long GetRestoredFileSize(
            SnapshotFile snapshotFile,
            IReadOnlyList<ChunkStorageDescriptor> chunks)
        {
            if (chunks.Count > 0 && chunks.All(x => x.OriginalSize.HasValue))
            {
                return chunks.Sum(x => x.OriginalSize!.Value);
            }

            return snapshotFile.Size;
        }
    }
}
