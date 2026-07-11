// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.AspNetCore.SignalR;
using Octockup.Server.Abstractions;
using Octockup.Server.Hubs;
using Octockup.Server.Models.Dto;

namespace Octockup.Server.Services
{
    public class SignalRScheduleProgressPublisher(
        IHubContext<EventHub> _hubContext) : IScheduleProgressPublisher
    {
        public Task PublishAsync(
            ScheduleReportDto report,
            CancellationToken cancellationToken)
        {
            return _hubContext.Clients
                .User(report.UserId.ToString())
                .SendAsync("ScheduleReport", report, cancellationToken);
        }
    }
}
