// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Octockup.Server.Models.Enums;

namespace Octockup.Server.Jobs
{
    public record StorageCleanupExecutionOptions(int DeleteBatchSize, TimeSpan DeleteDelay)
    {
        public static StorageCleanupExecutionOptions Create(StorageCleanupSpeed speed)
        {
            return speed switch
            {
                StorageCleanupSpeed.Normal => new(250, TimeSpan.FromMilliseconds(50)),
                StorageCleanupSpeed.Faster => new(10_000, TimeSpan.FromMilliseconds(5)),
                _ => throw new ArgumentOutOfRangeException(nameof(speed), speed, null),
            };
        }
    }
}
