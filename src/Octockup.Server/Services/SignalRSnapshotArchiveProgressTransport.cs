// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.AspNetCore.SignalR;
using Octockup.Server.Abstractions;
using Octockup.Server.Hubs;
using Octockup.Server.Models.Dto;

namespace Octockup.Server.Services
{
    public class SignalRSnapshotArchiveProgressTransport(
        IHubContext<EventHub> _hubContext) : ISnapshotArchiveProgressTransport
    {
        public Task SendAsync(
            SnapshotArchiveJobDto progress,
            CancellationToken cancellationToken)
        {
            return _hubContext.Clients
                .User(progress.UserId.ToString())
                .SendAsync("SnapshotArchiveProgress", progress, cancellationToken);
        }
    }
}
