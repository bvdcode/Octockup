// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Octockup.Server.Abstractions;

namespace Octockup.Tests
{
    internal class RecordingJobScheduler : IStorageCleanupJobScheduler
    {
        public int TriggerCount { get; private set; }

        public Task TriggerAsync()
        {
            TriggerCount++;
            return Task.CompletedTask;
        }
    }
}
