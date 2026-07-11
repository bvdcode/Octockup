// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.Quartz.Attributes;
using Microsoft.EntityFrameworkCore;
using Octockup.Server.Database;
using Quartz;

namespace Octockup.Server.Jobs
{
    [JobTrigger(minutes: 60)]
    public class PurgeRefreshSessionsJob(
        AppDbContext _dbContext,
        TimeProvider _timeProvider,
        ILogger<PurgeRefreshSessionsJob> _logger) : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
            int deleted = await _dbContext.RefreshSessions
                .Where(x => x.ExpiresAt <= now)
                .ExecuteDeleteAsync(context.CancellationToken);
            if (deleted > 0)
            {
                _logger.LogInformation("Purged {SessionCount} expired refresh sessions.", deleted);
            }
        }
    }
}
