// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using Quartz;
using Octockup.Server.Database;
using Octockup.Server.Abstractions;
using EasyExtensions.Quartz.Attributes;

namespace Octockup.Server.Jobs
{
    [JobTrigger(minutes: 1)]
    public class RetentionJob(
        AppDbContext _dbContext,
        ILogger<RetentionJob> _logger,
        IEnumerable<IBackupProvider> _providers) : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            _logger.LogInformation(GetType().FullName);
        }
    }
}
