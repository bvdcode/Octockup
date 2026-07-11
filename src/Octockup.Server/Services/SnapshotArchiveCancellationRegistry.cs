// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using System.Collections.Concurrent;

namespace Octockup.Server.Services
{
    public class SnapshotArchiveCancellationRegistry(
        ILogger<SnapshotArchiveCancellationRegistry> _logger)
    {
        private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _cancellations = [];

        public bool TryRegister(Guid jobId, CancellationTokenSource cancellationTokenSource)
        {
            return _cancellations.TryAdd(jobId, cancellationTokenSource);
        }

        public void Unregister(Guid jobId, CancellationTokenSource cancellationTokenSource)
        {
            _cancellations.TryRemove(
                new KeyValuePair<Guid, CancellationTokenSource>(jobId, cancellationTokenSource));
        }

        public bool Cancel(Guid jobId)
        {
            if (!_cancellations.TryGetValue(jobId, out CancellationTokenSource? cancellation))
            {
                return false;
            }

            try
            {
                cancellation.Cancel();
                return true;
            }
            catch (ObjectDisposedException ex)
            {
                _logger.LogDebug(
                    ex,
                    "Snapshot archive job {JobId} already finished during cancellation.",
                    jobId);
                return false;
            }
        }
    }
}
