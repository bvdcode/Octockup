// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore;
using Octockup.Server.Database;

namespace Octockup.Server.Services
{
    public class PreviousSnapshotFileLookup(AppDbContext _dbContext)
    {
        public const int MaxBatchSize = 500;
        private Guid? _snapshotId;

        public int PreviousFileCount { get; private set; }
        public Guid? SnapshotId => _snapshotId;

        public async Task InitializeAsync(
            Guid backupId,
            CancellationToken cancellationToken)
        {
            Snapshot? previousSnapshot = await _dbContext.Snapshots
                .AsNoTracking()
                .Where(x => x.BackupId == backupId && x.CompletedAt != null)
                .OrderByDescending(x => x.CompletedAt)
                .ThenByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.Id)
                .FirstOrDefaultAsync(cancellationToken);
            _snapshotId = previousSnapshot?.Id;
            PreviousFileCount = previousSnapshot?.FilesCount ?? 0;
        }

        public async Task<IReadOnlyDictionary<string, SnapshotFile>> LoadBatchAsync(
            IReadOnlyCollection<string> paths,
            CancellationToken cancellationToken)
        {
            ArgumentOutOfRangeException.ThrowIfGreaterThan(paths.Count, MaxBatchSize);
            if (_snapshotId is null || paths.Count == 0)
            {
                return new Dictionary<string, SnapshotFile>(StringComparer.Ordinal);
            }

            string[] distinctPaths = paths
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            List<SnapshotFile> files = await _dbContext.SnapshotFiles
                .AsNoTracking()
                .Where(x => x.SnapshotId == _snapshotId && distinctPaths.Contains(x.Path))
                .ToListAsync(cancellationToken);
            return files.ToDictionary(x => x.Path, StringComparer.Ordinal);
        }
    }
}
