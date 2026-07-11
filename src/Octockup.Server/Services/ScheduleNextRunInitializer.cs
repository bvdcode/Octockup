// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore;
using Octockup.Server.Database;
using Octockup.Server.Helpers;
using Octockup.Server.Models.Enums;

namespace Octockup.Server.Services
{
    public class ScheduleNextRunInitializer(
        AppDbContext _dbContext,
        ILogger<ScheduleNextRunInitializer> _logger)
    {
        private const int BatchSize = 500;

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            int updated = 0;
            while (true)
            {
                List<Schedule> schedules = await _dbContext.Schedules
                    .Where(x =>
                        x.NextRunAt == null &&
                        (x.Status == ScheduleStatus.Running ||
                            x.FinishedAt == null ||
                            x.Interval != null))
                    .OrderBy(x => x.Id)
                    .Take(BatchSize)
                    .ToListAsync(cancellationToken);
                if (schedules.Count == 0)
                {
                    break;
                }

                DateTime now = DateTime.UtcNow;
                foreach (Schedule schedule in schedules)
                {
                    schedule.NextRunAt = ScheduleHelpers.CalculateNextRun(schedule, now);
                }

                await _dbContext.SaveChangesAsync(cancellationToken);
                updated += schedules.Count;
                _dbContext.ChangeTracker.Clear();
            }

            if (updated > 0)
            {
                _logger.LogInformation(
                    "Initialized next-run timestamps for {ScheduleCount} schedules.",
                    updated);
            }
        }
    }
}
