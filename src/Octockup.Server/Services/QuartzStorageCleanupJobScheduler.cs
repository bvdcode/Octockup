// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.Quartz.Extensions;
using Octockup.Server.Abstractions;
using Octockup.Server.Jobs;
using Quartz;

namespace Octockup.Server.Services
{
    public class QuartzStorageCleanupJobScheduler(
        ISchedulerFactory _schedulerFactory) : IStorageCleanupJobScheduler
    {
        public Task TriggerAsync()
        {
            return _schedulerFactory.TriggerJobAsync<ExecuteStorageCleanupJob>();
        }
    }
}
