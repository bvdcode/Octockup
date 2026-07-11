// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Octockup.Server.Models.Options
{
    public class BackupProgressOptions
    {
        public TimeSpan PublishInterval { get; set; } = TimeSpan.FromSeconds(1);
        public TimeSpan AggregateLogInterval { get; set; } = TimeSpan.FromSeconds(30);
    }
}
