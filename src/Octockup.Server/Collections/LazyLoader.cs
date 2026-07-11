// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using System.Threading.Channels;

namespace Octockup.Server.Collections
{
    public class LazyLoader<T> : IAsyncDisposable
    {
        private readonly Channel<T> _channel;
        private readonly CancellationTokenSource _loadingCancellationTokenSource;
        private readonly Task _loadingTask;
        private int _totalLoaded;
        private int _isCompleted;

        public LazyLoader(
            IAsyncEnumerable<T> lazyCollection,
            int capacity,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(lazyCollection);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);

            _channel = Channel.CreateBounded<T>(new BoundedChannelOptions(capacity)
            {
                SingleReader = true,
                SingleWriter = true,
                FullMode = BoundedChannelFullMode.Wait
            });
            _loadingCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _loadingTask = LoadAsync(lazyCollection, _loadingCancellationTokenSource.Token);
        }

        public bool IsEnumerationCompleted => Volatile.Read(ref _isCompleted) == 1;

        public int Total => Volatile.Read(ref _totalLoaded);

        public IAsyncEnumerable<T> ReadAllAsync(CancellationToken cancellationToken = default) =>
            _channel.Reader.ReadAllAsync(cancellationToken);

        private async Task LoadAsync(IAsyncEnumerable<T> lazyCollection, CancellationToken cancellationToken)
        {
            Exception? loadingException = null;
            try
            {
                await foreach (T item in lazyCollection
                    .WithCancellation(cancellationToken)
                    .ConfigureAwait(false))
                {
                    await _channel.Writer.WriteAsync(item, cancellationToken).ConfigureAwait(false);
                    Interlocked.Increment(ref _totalLoaded);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                loadingException = ex;
            }
            finally
            {
                Interlocked.Exchange(ref _isCompleted, 1);
                _channel.Writer.TryComplete(loadingException);
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _loadingCancellationTokenSource.CancelAsync().ConfigureAwait(false);
            await _loadingTask.ConfigureAwait(false);
            _loadingCancellationTokenSource.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
