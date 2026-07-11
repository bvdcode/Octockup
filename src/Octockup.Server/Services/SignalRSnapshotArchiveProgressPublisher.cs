// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.AspNetCore.SignalR;
using Octockup.Server.Abstractions;
using Octockup.Server.Hubs;
using Octockup.Server.Models.Dto;

namespace Octockup.Server.Services
{
    public class SignalRSnapshotArchiveProgressPublisher(
        IHubContext<EventHub> _hubContext,
        ILogger<SignalRSnapshotArchiveProgressPublisher> _logger) :
        ISnapshotArchiveProgressPublisher
    {
        public async Task PublishAsync(
            SnapshotArchiveJobDto progress,
            CancellationToken cancellationToken)
        {
            try
            {
                await _hubContext.Clients
                    .User(progress.UserId.ToString())
                    .SendAsync("SnapshotArchiveProgress", progress, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(
                    ex,
                    "Failed to publish snapshot archive progress for job {JobId}.",
                    progress.JobId);
            }
        }
    }
}
