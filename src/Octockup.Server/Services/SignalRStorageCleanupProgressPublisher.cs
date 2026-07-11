// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.AspNetCore.SignalR;
using Octockup.Server.Abstractions;
using Octockup.Server.Hubs;
using Octockup.Server.Models.Dto;

namespace Octockup.Server.Services
{
    public class SignalRStorageCleanupProgressPublisher(
        IHubContext<EventHub> _hubContext,
        ILogger<SignalRStorageCleanupProgressPublisher> _logger) : IStorageCleanupProgressPublisher
    {
        public async Task PublishAsync(
            StorageCleanupJobDto progress,
            CancellationToken cancellationToken)
        {
            try
            {
                await _hubContext.Clients
                    .User(progress.UserId.ToString())
                    .SendAsync("StorageCleanupProgress", progress, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(
                    ex,
                    "Failed to publish storage cleanup progress for job {JobId}.",
                    progress.JobId);
            }
        }
    }
}
