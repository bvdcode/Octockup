using Mapster;
using Octockup.Server.Models;
using Octockup.Server.Helpers;
using Microsoft.AspNetCore.Mvc;
using Octockup.Server.Database;
using Octockup.Server.Models.Dto;
using Octockup.Server.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace Octockup.Server.Controllers
{
    [ApiController]
    public class SnapshotController(AppDbContext _dbContext, IEnumerable<IBackupProvider> _providers) : ControllerBase
    {
        [Authorize]
        [HttpGet("/api/v1/snapshots/{snapshotId:guid}/files/{fileId:guid}/download")]
        public async Task<IActionResult> DownloadSnapshotFile([FromRoute] Guid snapshotId, [FromRoute] Guid fileId)
        {
            var snapshotFile = _dbContext.SnapshotFiles
                .AsNoTracking()
                .Include(sf => sf.Snapshot)
                    .ThenInclude(s => s.Backup)
                        .ThenInclude(b => b.Storage)
                .FirstOrDefault(sf => sf.SnapshotId == snapshotId && sf.Id == fileId);

            if (snapshotFile == null)
            {
                return NotFound();
            }

            var provider = _providers.FirstOrDefault(p => p.Id == snapshotFile.Snapshot.Backup.Storage.BackupModuleId);
            if (provider == null)
            {
                return NotFound();
            }

            provider.SetParameters(snapshotFile.Snapshot.Backup.Storage.Parameters);
            if (provider is not IBackupStorage storage)
            {
                return BadRequest("Storage provider is not a backup storage");
            }

            var hashes = snapshotFile.ChunkHashes?.ToList() ?? [];
            if (hashes.Count == 0)
            {
                return NotFound("No chunks for this file.");
            }

            Response.ContentType = "application/octet-stream";
            Response.Headers.ContentDisposition = $"attachment; filename=\"{snapshotFile.Name}\"";
            Response.ContentLength = snapshotFile.Size;

            foreach (var hash in hashes)
            {
                var path = PathHelpers.GetPath(hash);
                bool? exists = await storage.ExistsAsync(path);
                if (exists != true)
                {
                    return NotFound($"Chunk {hash} not found.");
                }
            }

            foreach (var hash in hashes)
            {
                var path = PathHelpers.GetPath(hash);

                var fileInfo = new BackupFileInfo
                {
                    Path = path,
                    Name = snapshotFile.Name,
                    Size = snapshotFile.Size,
                    LastModified = snapshotFile.LastModified,
                };

                await using var chunkStream = await storage.GetFileStreamAsync(fileInfo);
                await chunkStream.CopyToAsync(Response.Body, HttpContext.RequestAborted);
                await Response.Body.FlushAsync();
            }

            return new EmptyResult();
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
