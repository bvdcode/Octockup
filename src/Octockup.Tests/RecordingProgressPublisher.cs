// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Octockup.Server.Abstractions;
using Octockup.Server.Models.Dto;

namespace Octockup.Tests
{
    internal class RecordingProgressPublisher : IStorageCleanupProgressPublisher
    {
        public List<StorageCleanupJobDto> Updates { get; } = [];

        public Task PublishAsync(
            StorageCleanupJobDto progress,
            CancellationToken cancellationToken)
        {
            Updates.Add(progress);
            return Task.CompletedTask;
        }
    }
}
