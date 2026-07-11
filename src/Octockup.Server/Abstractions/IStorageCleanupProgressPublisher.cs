// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Octockup.Server.Models.Dto;

namespace Octockup.Server.Abstractions
{
    public interface IStorageCleanupProgressPublisher
    {
        Task PublishAsync(
            StorageCleanupJobDto progress,
            CancellationToken cancellationToken);
    }
}
