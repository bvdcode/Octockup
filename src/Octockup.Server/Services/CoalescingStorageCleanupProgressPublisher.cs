// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Octockup.Server.Abstractions;
using Octockup.Server.Models.Dto;
using Octockup.Server.Models.Enums;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Octockup.Server.Services
{
    public class CoalescingStorageCleanupProgressPublisher(
        IStorageCleanupProgressTransport _transport,
        ILogger<CoalescingStorageCleanupProgressPublisher> _logger) :
        IStorageCleanupProgressPublisher,
        IAsyncDisposable
    {
        private readonly ConcurrentDictionary<Guid, JobPublisher> _publishers = new();

        public async Task PublishAsync(
            StorageCleanupJobDto progress,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            JobPublisher publisher = _publishers.GetOrAdd(
                progress.JobId,
                jobId => new JobPublisher(jobId, _transport, _logger));

            if (progress.Status is StorageCleanupStatus.Pending or StorageCleanupStatus.Running)
            {
                publisher.Publish(progress);
                return;
            }

            try
            {
                await publisher
                    .PublishTerminalAsync(progress, cancellationToken)
                    .ConfigureAwait(false);
            }
            finally
            {
                _publishers.TryRemove(progress.JobId, out _);
                await publisher.DisposeAsync().ConfigureAwait(false);
            }
        }

        public async ValueTask DisposeAsync()
        {
            JobPublisher[] publishers = _publishers.Values.ToArray();
            _publishers.Clear();
            foreach (JobPublisher publisher in publishers)
            {
                await publisher.DisposeAsync().ConfigureAwait(false);
            }
        }

        private class JobPublisher : IAsyncDisposable
        {
            private readonly Guid _jobId;
            private readonly IStorageCleanupProgressTransport _transport;
            private readonly ILogger _logger;
            private readonly Channel<StorageCleanupJobDto> _channel;
            private readonly CancellationTokenSource _cancellationTokenSource = new();
            private readonly Task _pumpTask;
            private int _terminalPublished;

            public JobPublisher(
                Guid jobId,
                IStorageCleanupProgressTransport transport,
                ILogger logger)
            {
                _jobId = jobId;
                _transport = transport;
                _logger = logger;
                _channel = Channel.CreateBounded<StorageCleanupJobDto>(
                    new BoundedChannelOptions(1)
                    {
                        AllowSynchronousContinuations = false,
                        FullMode = BoundedChannelFullMode.DropOldest,
                        SingleReader = true,
                        SingleWriter = false
                    });
                _pumpTask = PumpAsync(_cancellationTokenSource.Token);
            }

            public void Publish(StorageCleanupJobDto progress)
            {
                _channel.Writer.TryWrite(progress);
            }

            public async Task PublishTerminalAsync(
                StorageCleanupJobDto progress,
                CancellationToken cancellationToken)
            {
                if (Interlocked.Exchange(ref _terminalPublished, 1) == 0)
                {
                    _channel.Writer.TryWrite(progress);
                    _channel.Writer.TryComplete();
                }

                await _pumpTask.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            private async Task PumpAsync(CancellationToken cancellationToken)
            {
                try
                {
                    await foreach (StorageCleanupJobDto progress in _channel.Reader
                        .ReadAllAsync(cancellationToken))
                    {
                        StorageCleanupJobDto latest = progress;
                        while (_channel.Reader.TryRead(out StorageCleanupJobDto? newer))
                        {
                            latest = newer;
                        }

                        try
                        {
                            await _transport
                                .SendAsync(latest, cancellationToken)
                                .ConfigureAwait(false);
                        }
                        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                        {
                            return;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogDebug(
                                ex,
                                "Failed to publish storage cleanup progress for job {JobId}.",
                                _jobId);
                        }
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                }
            }

            public async ValueTask DisposeAsync()
            {
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
}
