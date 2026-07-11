// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.Quartz.Attributes;
using Octockup.Server.Services;
using Quartz;

namespace Octockup.Server.Jobs
{
    [JobTrigger(minutes: 1)]
    public class ExecuteStorageCleanupJob(StorageCleanupJobExecutor _executor) : IJob
    {
        public Task Execute(IJobExecutionContext context)
        {
            return _executor.ExecutePendingAsync(context.CancellationToken);
        }
    }
}
