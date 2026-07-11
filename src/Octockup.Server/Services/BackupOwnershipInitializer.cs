// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore;
using Octockup.Server.Database;

namespace Octockup.Server.Services
{
    public class BackupOwnershipInitializer(
        AppDbContext _dbContext,
        ILogger<BackupOwnershipInitializer> _logger)
    {
        private const int BatchSize = 500;

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            int updated = 0;
            while (true)
            {
                List<Backup> backups = await _dbContext.Backups
                    .Include(x => x.Source)
                    .Where(x => x.UserId == Guid.Empty)
                    .OrderBy(x => x.Id)
                    .Take(BatchSize)
                    .ToListAsync(cancellationToken);
                if (backups.Count == 0)
                {
                    break;
                }

                foreach (Backup backup in backups)
                {
                    backup.UserId = backup.Source.UserId;
                }

                await _dbContext.SaveChangesAsync(cancellationToken);
                updated += backups.Count;
                _dbContext.ChangeTracker.Clear();
            }

            if (updated > 0)
            {
                _logger.LogInformation(
                    "Backfilled ownership for {BackupCount} backups.",
                    updated);
            }
        }
    }
}
