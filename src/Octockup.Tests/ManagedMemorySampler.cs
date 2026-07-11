// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

namespace Octockup.Tests
{
    internal class ManagedMemorySampler : IAsyncDisposable
    {
        private static readonly TimeSpan SampleInterval = TimeSpan.FromMilliseconds(5);

        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private readonly long _baselineBytes;
        private readonly Task _samplingTask;
        private long _maximumBytes;
        private long _retainedBytes;
        private int _stopped;

        public ManagedMemorySampler()
        {
            _baselineBytes = GC.GetTotalMemory(true);
            _maximumBytes = _baselineBytes;
            _samplingTask = SampleAsync(_cancellationTokenSource.Token);
        }

        public long MaximumGrowthBytes =>
            Math.Max(0, Interlocked.Read(ref _maximumBytes) - _baselineBytes);

        public long RetainedGrowthBytes =>
            Math.Max(0, Interlocked.Read(ref _retainedBytes) - _baselineBytes);

        public async Task StopAsync()
        {
            if (Interlocked.Exchange(ref _stopped, 1) != 0)
            {
                await _samplingTask.ConfigureAwait(false);
                return;
            }

            await _cancellationTokenSource.CancelAsync().ConfigureAwait(false);
            await _samplingTask.ConfigureAwait(false);
            Sample(GC.GetTotalMemory(true));
            Interlocked.Exchange(ref _retainedBytes, GC.GetTotalMemory(false));
            _cancellationTokenSource.Dispose();
        }

        private async Task SampleAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (true)
                {
                    Sample(GC.GetTotalMemory(false));
                    await Task.Delay(SampleInterval, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
        }

        private void Sample(long bytes)
        {
            long current = Interlocked.Read(ref _maximumBytes);
            while (bytes > current)
            {
                long observed = Interlocked.CompareExchange(
                    ref _maximumBytes,
                    bytes,
                    current);
                if (observed == current)
                {
                    return;
                }

                current = observed;
            }
        }

        public ValueTask DisposeAsync()
        {
            return new ValueTask(StopAsync());
        }
    }
}
