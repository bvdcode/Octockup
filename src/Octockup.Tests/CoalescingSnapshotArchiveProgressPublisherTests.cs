// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Octockup.Server.Abstractions;
using Octockup.Server.Models.Dto;
using Octockup.Server.Models.Enums;
using Octockup.Server.Models.Options;
using Octockup.Server.Services;
using System.Collections.Concurrent;

namespace Octockup.Tests
{
    public class CoalescingSnapshotArchiveProgressPublisherTests
    {
        [Test]
        public async Task PublishAsync_WhenTransportIsSlow_DoesNotBlockArchiveProgress()
        {
            BlockingTransport transport = new();
            await using CoalescingSnapshotArchiveProgressPublisher publisher = new(
                transport,
                CreateOptions(),
                NullLogger<CoalescingSnapshotArchiveProgressPublisher>.Instance);
            Guid jobId = Guid.NewGuid();

            await publisher.PublishAsync(CreateProgress(jobId, 0), CancellationToken.None);
            await transport.FirstSendStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
            for (int index = 1; index <= 1_000; index++)
            {
                await publisher.PublishAsync(
                    CreateProgress(jobId, index),
                    CancellationToken.None);
            }

            Assert.That(transport.SendCount, Is.EqualTo(1));
            SnapshotArchiveJobDto terminal = CreateProgress(jobId, 1_000);
            terminal.Status = SnapshotArchiveStatus.Completed;
            Task terminalPublish = publisher.PublishAsync(terminal, CancellationToken.None);
            transport.ReleaseFirstSend.TrySetResult(true);
            await terminalPublish;

            SnapshotArchiveJobDto finalProgress = transport.Reports.Last();
            Assert.Multiple(() =>
            {
                Assert.That(transport.MaxConcurrentSends, Is.EqualTo(1));
                Assert.That(transport.SendCount, Is.EqualTo(2));
                Assert.That(finalProgress.Status, Is.EqualTo(SnapshotArchiveStatus.Completed));
                Assert.That(finalProgress.ProcessedFiles, Is.EqualTo(1_000));
            });
        }

        [Test]
        public async Task PublishAsync_WhenClientIsDisconnected_CompletesTerminalLifecycle()
        {
            ThrowingTransport transport = new();
            await using CoalescingSnapshotArchiveProgressPublisher publisher = new(
                transport,
                CreateOptions(),
                NullLogger<CoalescingSnapshotArchiveProgressPublisher>.Instance);
            SnapshotArchiveJobDto terminal = CreateProgress(Guid.NewGuid(), 1);
            terminal.Status = SnapshotArchiveStatus.Failed;

            Assert.DoesNotThrowAsync(async () =>
                await publisher.PublishAsync(terminal, CancellationToken.None));
            Assert.That(transport.SendCount, Is.EqualTo(1));
        }

        [Test]
        public async Task PublishAsync_WhenTransportTimesOut_DoesNotHoldTerminalLifecycle()
        {
            CancelableTransport transport = new();
            await using CoalescingSnapshotArchiveProgressPublisher publisher = new(
                transport,
                CreateOptions(TimeSpan.FromMilliseconds(25)),
                NullLogger<CoalescingSnapshotArchiveProgressPublisher>.Instance);
            SnapshotArchiveJobDto terminal = CreateProgress(Guid.NewGuid(), 1);
            terminal.Status = SnapshotArchiveStatus.Completed;

            await publisher
                .PublishAsync(terminal, CancellationToken.None)
                .WaitAsync(TimeSpan.FromSeconds(1));

            await transport.CancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(1));
        }

        private static SnapshotArchiveJobDto CreateProgress(Guid jobId, long processedFiles)
        {
            return new SnapshotArchiveJobDto
            {
                JobId = jobId,
                UserId = Guid.NewGuid(),
                SnapshotId = Guid.NewGuid(),
                Status = SnapshotArchiveStatus.Running,
                Phase = SnapshotArchivePhase.Streaming,
                StartedAt = DateTime.UtcNow,
                ProcessedFiles = processedFiles
            };
        }

        private static IOptions<BackupProgressOptions> CreateOptions(
            TimeSpan? transportTimeout = null)
        {
            return Options.Create(new BackupProgressOptions
            {
                TransportTimeout = transportTimeout ?? TimeSpan.FromSeconds(1)
            });
        }

        private class BlockingTransport : ISnapshotArchiveProgressTransport
        {
            private int _activeSends;
            private int _maxConcurrentSends;
            private int _sendCount;

            public TaskCompletionSource<bool> FirstSendStarted { get; } =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            public TaskCompletionSource<bool> ReleaseFirstSend { get; } =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            public ConcurrentQueue<SnapshotArchiveJobDto> Reports { get; } = new();
            public int SendCount => Volatile.Read(ref _sendCount);
            public int MaxConcurrentSends => Volatile.Read(ref _maxConcurrentSends);

            public async Task SendAsync(
                SnapshotArchiveJobDto progress,
                CancellationToken cancellationToken)
            {
                int activeSends = Interlocked.Increment(ref _activeSends);
                UpdateMaximum(ref _maxConcurrentSends, activeSends);
                int sendNumber = Interlocked.Increment(ref _sendCount);
                try
                {
                    if (sendNumber == 1)
                    {
                        FirstSendStarted.TrySetResult(true);
                        await ReleaseFirstSend.Task.WaitAsync(cancellationToken);
                    }

                    Reports.Enqueue(progress);
                }
                finally
                {
                    Interlocked.Decrement(ref _activeSends);
                }
            }

            private static void UpdateMaximum(ref int target, int candidate)
            {
                int current = Volatile.Read(ref target);
                while (candidate > current)
                {
                    int observed = Interlocked.CompareExchange(ref target, candidate, current);
                    if (observed == current)
                    {
                        return;
                    }

                    current = observed;
                }
            }
        }

        private class ThrowingTransport : ISnapshotArchiveProgressTransport
        {
            private int _sendCount;
            public int SendCount => Volatile.Read(ref _sendCount);

            public Task SendAsync(
                SnapshotArchiveJobDto progress,
                CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref _sendCount);
                throw new InvalidOperationException("Client disconnected.");
            }
        }

        private class CancelableTransport : ISnapshotArchiveProgressTransport
        {
            public TaskCompletionSource<bool> CancellationObserved { get; } =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            public async Task SendAsync(
                SnapshotArchiveJobDto progress,
                CancellationToken cancellationToken)
            {
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    CancellationObserved.TrySetResult(true);
                    throw;
                }
            }
        }
    }
}
