// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore;
using Octockup.Server.Database;
using Octockup.Server.Helpers;
using Octockup.Server.Models.Dto;
using Octockup.Server.Models.Requests;

namespace Octockup.Server.Services
{
    public class SnapshotFilePageService(AppDbContext _dbContext)
    {
        public async Task<SnapshotFilePageDto?> GetPageAsync(
            Guid userId,
            Guid snapshotId,
            SnapshotFilePageRequest request,
            CancellationToken cancellationToken)
        {
            bool snapshotExists = await _dbContext.Snapshots
                .AsNoTracking()
                .AnyAsync(
                    x => x.Id == snapshotId && x.Backup.Source.UserId == userId,
                    cancellationToken)
                .ConfigureAwait(false);
            if (!snapshotExists)
            {
                return null;
            }

            string? search = request.Search?.Trim().ToLowerInvariant();
            IQueryable<SnapshotFile> filteredQuery = _dbContext.SnapshotFiles
                .AsNoTracking()
                .Where(x => x.SnapshotId == snapshotId);
            if (!string.IsNullOrEmpty(search))
            {
                filteredQuery = filteredQuery.Where(x =>
                    x.Path.ToLower().Contains(search) ||
                    x.Name.ToLower().Contains(search));
            }

            long totalCount = await filteredQuery
                .LongCountAsync(cancellationToken)
                .ConfigureAwait(false);
            if (!string.IsNullOrEmpty(request.Cursor))
            {
                string cursorPath = SnapshotFileCursorCodec.Decode(request.Cursor);
                filteredQuery = filteredQuery.Where(x => x.Path.CompareTo(cursorPath) > 0);
            }

            List<SnapshotFileDto> rows = await filteredQuery
                .OrderBy(x => x.Path)
                .Take(request.PageSize + 1)
                .Select(x => new SnapshotFileDto
                {
                    Id = x.Id,
                    SnapshotId = x.SnapshotId,
                    Size = x.Size,
                    LastModified = x.LastModified,
                    Name = x.Name,
                    Path = x.Path,
                    Hashsum = x.Hashsum
                })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            bool hasNextPage = rows.Count > request.PageSize;
            if (hasNextPage)
            {
                rows.RemoveAt(rows.Count - 1);
            }

            return new SnapshotFilePageDto
            {
                Items = rows,
                TotalCount = totalCount,
                HasNextPage = hasNextPage,
                NextCursor = hasNextPage && rows.Count > 0
                    ? SnapshotFileCursorCodec.Encode(rows[^1].Path)
                    : null
            };
        }
    }
}
