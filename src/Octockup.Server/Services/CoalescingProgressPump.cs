// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using System.Threading.Channels;

namespace Octockup.Server.Services
{
    internal class CoalescingProgressPump<TProgress> : IAsyncDisposable
    {
        private readonly Func<TProgress, CancellationToken, Task> _sendAsync;
        private readonly Action<Exception> _logFailure;
        private readonly TimeSpan _transportTimeout;
        private readonly Channel<TProgress> _channel;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Task _pumpTask;
        private int _terminalPublished;
        private int _disposeStarted;

        public CoalescingProgressPump(
            Func<TProgress, CancellationToken, Task> sendAsync,
            Action<Exception> logFailure,
            TimeSpan transportTimeout)
        {
            if (transportTimeout <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(transportTimeout));
            }

            _sendAsync = sendAsync;
            _logFailure = logFailure;
            _transportTimeout = transportTimeout;
            _channel = Channel.CreateBounded<TProgress>(new BoundedChannelOptions(1)
            {
                AllowSynchronousContinuations = false,
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            });
            _cancellationTokenSource = new CancellationTokenSource();
            _pumpTask = PumpAsync();
        }

        public Task Completion => _pumpTask;

        public void Publish(TProgress progress)
        {
            _channel.Writer.TryWrite(progress);
        }

        public void Complete(TProgress progress)
        {
            if (Interlocked.Exchange(ref _terminalPublished, 1) != 0)
            {
                return;
            }

            _channel.Writer.TryWrite(progress);
            _channel.Writer.TryComplete();
        }

        private async Task PumpAsync()
        {
            CancellationToken cancellationToken = _cancellationTokenSource.Token;
            try
            {
                await foreach (TProgress progress in _channel.Reader
                    .ReadAllAsync(cancellationToken))
                {
                    TProgress latest = progress;
                    while (_channel.Reader.TryRead(out TProgress? newer))
                    {
                        latest = newer;
                    }

                    try
                    {
                        using CancellationTokenSource sendCancellation =
                            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        sendCancellation.CancelAfter(_transportTimeout);
                        await _sendAsync(latest, sendCancellation.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (Exception ex)
                    {
                        _logFailure(ex);
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposeStarted, 1) != 0)
            {
                await _pumpTask.ConfigureAwait(false);
                return;
            }

            _channel.Writer.TryComplete();
            await _cancellationTokenSource.CancelAsync().ConfigureAwait(false);
            try
            {
                await _pumpTask.ConfigureAwait(false);
            }
            finally
            {
                _cancellationTokenSource.Dispose();
            }
        }
    }
}
