// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.Quartz.Extensions;
using Quartz;

namespace Octockup.Server.Jobs
{
    public class BackupJobScheduler(ISchedulerFactory schedulerFactory) : IBackupJobScheduler
    {
        public Task TriggerAsync()
        {
            return schedulerFactory.TriggerJobAsync<ExecuteBackupJob>();
        }
    }
}
