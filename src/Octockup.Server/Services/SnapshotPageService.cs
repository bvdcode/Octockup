// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore;
using Octockup.Server.Database;
using Octockup.Server.Helpers;
using Octockup.Server.Models.Dto;
using Octockup.Server.Models.Requests;

namespace Octockup.Server.Services
{
    public class SnapshotPageService(AppDbContext _dbContext)
    {
        public async Task<SnapshotPageDto?> GetPageAsync(
            Guid userId,
            Guid backupId,
            SnapshotPageRequest request,
            CancellationToken cancellationToken)
        {
            bool backupExists = await _dbContext.Backups
                .AsNoTracking()
                .AnyAsync(
                    x => x.Id == backupId && x.UserId == userId,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!backupExists)
            {
                return null;
            }

            IQueryable<Snapshot> query = _dbContext.Snapshots
                .AsNoTracking()
                .Where(x => x.BackupId == backupId);
            long totalCount = await query
                .LongCountAsync(cancellationToken)
                .ConfigureAwait(false);
            if (!string.IsNullOrEmpty(request.Cursor))
            {
                (DateTime? completedAt, Guid id) = SnapshotCursorCodec.Decode(request.Cursor);
                query = completedAt.HasValue
                    ? query.Where(x =>
                        x.CompletedAt == null ||
                        x.CompletedAt < completedAt.Value ||
                        (x.CompletedAt == completedAt.Value && x.Id.CompareTo(id) < 0))
                    : query.Where(x =>
                        x.CompletedAt == null && x.Id.CompareTo(id) < 0);
            }

            List<SnapshotDto> rows = await query
                .OrderByDescending(x => x.CompletedAt != null)
                .ThenByDescending(x => x.CompletedAt)
                .ThenByDescending(x => x.Id)
                .Take(request.PageSize + 1)
                .Select(x => new SnapshotDto
                {
                    Id = x.Id,
                    CreatedAt = x.CreatedAt,
                    BackupId = x.BackupId,
                    CompletedAt = x.CompletedAt,
                    FilesCount = x.FilesCount,
                    TotalSize = x.TotalSize
                })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            bool hasNextPage = rows.Count > request.PageSize;
            if (hasNextPage)
            {
                rows.RemoveAt(rows.Count - 1);
            }

            SnapshotDto? last = rows.LastOrDefault();
            return new SnapshotPageDto
            {
                Items = rows,
                TotalCount = totalCount,
                HasNextPage = hasNextPage,
                NextCursor = hasNextPage && last is not null
                    ? SnapshotCursorCodec.Encode(last.CompletedAt, last.Id)
                    : null
            };
        }
    }
}
