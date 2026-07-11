// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.SignalR;

namespace Octockup.Server.Hubs
{
    [Authorize]
    [EnableCors]
    public class EventHub(ILogger<EventHub> _logger) : Hub
    {
        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation(
                exception,
                "Client disconnected: {ConnectionId}",
                Context.ConnectionId);
            await base.OnDisconnectedAsync(exception);
        }
    }
}
