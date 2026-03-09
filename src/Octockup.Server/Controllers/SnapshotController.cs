// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.Abstractions;
using EasyExtensions.Models.Enums;
using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using Octockup.Server.Abstractions;
using Octockup.Server.Database;
using Octockup.Server.Helpers;
using Octockup.Server.Models.Dto;
using Octockup.Server.Streams;

namespace Octockup.Server.Controllers
{
    [ApiController]
    public class SnapshotController(
        IStreamCipher _crypto,
        AppDbContext _dbContext,
        ILogger<SnapshotController> _logger,
        IEnumerable<IBackupProvider> _providers) : ControllerBase
    {
        [Authorize]
        [HttpGet("/api/v1/snapshots/{snapshotId:guid}/files/{fileId:guid}/download")]
        public async Task<IActionResult> DownloadSnapshotFile([FromRoute] Guid snapshotId, [FromRoute] Guid fileId)
        {
            var snapshotFile = await _dbContext.SnapshotFiles
                .AsNoTracking()
                .Include(sf => sf.Snapshot)
                    .ThenInclude(s => s.Backup)
                        .ThenInclude(b => b.Storage)
                .FirstOrDefaultAsync(sf => sf.SnapshotId == snapshotId && sf.Id == fileId);

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

            var fileHashes = snapshotFile.ChunkHashes?.ToList() ?? [];
            List<(string, CompressionAlgorithm)> hashes = [];
            foreach (var fileHash in fileHashes)
            {
                var found = await _dbContext.UploadedHashes.FirstOrDefaultAsync(x => x.Hash == fileHash);
                if (found == null)
                {
                    _logger.LogWarning("Chunk hash metadata not found in DB: {FileHash}", fileHash);
                    _logger.LogWarning("Trying to proceed with the download, but it may fail if the chunk is not found in storage or if the chunk is compressed with an unsupported algorithm");
                    hashes.Add((fileHash, CompressionHelpers.Algorithm));
                    continue;
                }
                hashes.Add((found.Hash, found.CompressionAlgorithm));
            }

            var stream = new SnapshotConcatStream(
                _logger,
                storage,
                hashes,
                snapshotFile,
                _crypto,
                HttpContext.RequestAborted
            );

            string contentType = MimeTypes.GetMimeType(snapshotFile.Name) ?? "application/octet-stream";
            var result = new FileStreamResult(stream, contentType)
            {
                FileDownloadName = snapshotFile.Name,
                EnableRangeProcessing = false,
            };

            Response.ContentLength = snapshotFile.Size;
            return result;
        }

        [Authorize]
        [HttpGet("/api/v1/snapshots/{snapshotId:guid}/files")]
        public IActionResult GetSnapshot([FromRoute] Guid snapshotId)
        {
            var snapshotFiles = _dbContext.SnapshotFiles
                .AsNoTracking()
                .Where(sf => sf.SnapshotId == snapshotId)
                .OrderBy(sf => sf.Path)
                .ThenBy(sf => sf.Name)
                .ToList()
                .Adapt<List<SnapshotFileDto>>();
            return Ok(snapshotFiles);
        }

        [Authorize]
        [HttpDelete("/api/v1/snapshots/{snapshotId:guid}")]
        public async Task<IActionResult> DeleteSnapshot([FromRoute] Guid snapshotId)
        {
            var snapshot = _dbContext.Snapshots
                .FirstOrDefault(s => s.Id == snapshotId);
            if (snapshot == null)
            {
                return NotFound();
            }
            await _dbContext.SnapshotFiles
                .Where(sf => sf.SnapshotId == snapshotId)
                .ExecuteDeleteAsync();
            _dbContext.Snapshots.Remove(snapshot);
            _dbContext.SaveChanges();
            return NoContent();
        }

        [Authorize]
        [HttpGet("/api/v1/snapshots")]
        public IActionResult GetSnapshots([FromQuery] Guid backupId)
        {
            var snapshots = _dbContext.Snapshots
                .Where(s => s.BackupId == backupId)
                .OrderBy(s => s.CreatedAt)
                .ToList();

            List<SnapshotDto> result = [];
            foreach (var snapshot in snapshots)
            {
                var dto = snapshot.Adapt<SnapshotDto>();
                dto.FilesCount = _dbContext.SnapshotFiles
                    .Count(sf => sf.SnapshotId == snapshot.Id);
                dto.TotalSize = _dbContext.SnapshotFiles
                    .Where(sf => sf.SnapshotId == snapshot.Id)
                    .Sum(sf => (long?)sf.Size) ?? 0;
                result.Add(dto);
            }
            return Ok(result);
        }
    }
}
