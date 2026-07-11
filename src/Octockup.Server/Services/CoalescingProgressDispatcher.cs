// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using System.Collections.Concurrent;

namespace Octockup.Server.Services
{
    internal class CoalescingProgressDispatcher<TKey, TProgress>(
        Func<TProgress, TKey> _selectKey,
        Func<TProgress, bool> _isTerminal,
        Func<TProgress, CancellationToken, Task> _sendAsync,
        Action<Exception, TKey> _logFailure,
        TimeSpan _transportTimeout) : IAsyncDisposable
        where TKey : notnull
    {
        private readonly ConcurrentDictionary<TKey, CoalescingProgressPump<TProgress>>
            _publishers = new();
        private readonly Lock _lifecycleLock = new();
        private bool _disposed;

        public async Task PublishAsync(
            TProgress progress,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TKey key = _selectKey(progress);
            CoalescingProgressPump<TProgress> publisher;
            Task? terminalCompletion = null;
            lock (_lifecycleLock)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                publisher = _publishers.GetOrAdd(
                    key,
                    currentKey => new CoalescingProgressPump<TProgress>(
                        _sendAsync,
                        ex => _logFailure(ex, currentKey),
                        _transportTimeout));

                if (_isTerminal(progress))
                {
                    publisher.Complete(progress);
                    terminalCompletion = publisher.Completion;
                }
                else
                {
                    publisher.Publish(progress);
                }
            }

            if (terminalCompletion is null)
            {
                return;
            }

            try
            {
                await terminalCompletion
                    .WaitAsync(cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                lock (_lifecycleLock)
                {
                    if (_publishers.TryGetValue(key, out CoalescingProgressPump<TProgress>? current) &&
                        ReferenceEquals(current, publisher))
                    {
                        _publishers.TryRemove(key, out _);
                    }
                }

                await publisher.DisposeAsync().ConfigureAwait(false);
            }
        }

        public async ValueTask DisposeAsync()
        {
            CoalescingProgressPump<TProgress>[] publishers;
            lock (_lifecycleLock)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                publishers = _publishers.Values.ToArray();
                _publishers.Clear();
            }

            foreach (CoalescingProgressPump<TProgress> publisher in publishers)
            {
                await publisher.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
