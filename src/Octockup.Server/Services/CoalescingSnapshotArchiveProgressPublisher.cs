// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.Extensions.Options;
using Octockup.Server.Abstractions;
using Octockup.Server.Models.Dto;
using Octockup.Server.Models.Enums;
using Octockup.Server.Models.Options;

namespace Octockup.Server.Services
{
    public class CoalescingSnapshotArchiveProgressPublisher(
        ISnapshotArchiveProgressTransport _transport,
        IOptions<BackupProgressOptions> options,
        ILogger<CoalescingSnapshotArchiveProgressPublisher> _logger) :
        ISnapshotArchiveProgressPublisher,
        IAsyncDisposable
    {
        private readonly CoalescingProgressDispatcher<Guid, SnapshotArchiveJobDto>
            _dispatcher = new(
                progress => progress.JobId,
                progress => progress.Status is not (
                    SnapshotArchiveStatus.Pending or SnapshotArchiveStatus.Running),
                _transport.SendAsync,
                (exception, jobId) => _logger.LogDebug(
                    exception,
                    "Failed to publish snapshot archive progress for job {JobId}.",
                    jobId),
                options.Value.TransportTimeout);

        public Task PublishAsync(
            SnapshotArchiveJobDto progress,
            CancellationToken cancellationToken)
        {
            return _dispatcher.PublishAsync(progress, cancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            return _dispatcher.DisposeAsync();
        }
    }
}
