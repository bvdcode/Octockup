// SPDX-License-Identifier: AGPL-3.0-only
// Copyright (c) 2025 Vadim Belov

using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;

namespace Octockup.Server.Hubs
{
    [Authorize]
    [EnableCors]
    public class EventHub(ILogger<EventHub> _logger) : Hub
    {
        public override Task OnConnectedAsync()
        {
            _logger.LogInformation("Client connected: {connectionId}", Context.ConnectionId);
            return Task.Run(() =>
            {
                while (true)
                {
                    Thread.Sleep(125);
                    Clients.Caller.SendAsync("Time", DateTime.UtcNow);
                    if (Context.ConnectionAborted.IsCancellationRequested)
                    {
                        _logger.LogInformation("Client disconnected: {connectionId}", Context.ConnectionId);
                        break;
                    }
                }
            });
        }
    }
}
