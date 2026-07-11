// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.Extensions.Logging.Abstractions;
using Octockup.Server.Abstractions;
using Octockup.Server.Models.Dto;
using Octockup.Server.Models.Enums;
using Octockup.Server.Services;
using System.Collections.Concurrent;

namespace Octockup.Tests
{
    public class CoalescingStorageCleanupProgressPublisherTests
    {
        [Test]
        public async Task PublishAsync_WhenTransportIsSlow_KeepsOnlyLatestUpdatePerJob()
        {
            BlockingTransport transport = new();
            await using CoalescingStorageCleanupProgressPublisher publisher = new(
                transport,
                NullLogger<CoalescingStorageCleanupProgressPublisher>.Instance);
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
            StorageCleanupJobDto terminal = CreateProgress(jobId, 1_000);
            terminal.Status = StorageCleanupStatus.Completed;
            Task terminalPublish = publisher.PublishAsync(terminal, CancellationToken.None);
            transport.ReleaseFirstSend.TrySetResult(true);
            await terminalPublish;

            StorageCleanupJobDto finalProgress = transport.Reports.Last();
            Assert.Multiple(() =>
            {
                Assert.That(transport.MaxConcurrentSends, Is.EqualTo(1));
                Assert.That(transport.SendCount, Is.EqualTo(2));
                Assert.That(finalProgress.Status, Is.EqualTo(StorageCleanupStatus.Completed));
                Assert.That(finalProgress.StorageObjectsScanned, Is.EqualTo(1_000));
            });
        }

        [Test]
        public async Task PublishAsync_WhenTransportFails_CompletesTerminalLifecycle()
        {
            ThrowingTransport transport = new();
            await using CoalescingStorageCleanupProgressPublisher publisher = new(
                transport,
                NullLogger<CoalescingStorageCleanupProgressPublisher>.Instance);
            StorageCleanupJobDto terminal = CreateProgress(Guid.NewGuid(), 1);
            terminal.Status = StorageCleanupStatus.Failed;

            Assert.DoesNotThrowAsync(async () =>
                await publisher.PublishAsync(terminal, CancellationToken.None));
            Assert.That(transport.SendCount, Is.EqualTo(1));
        }

        private static StorageCleanupJobDto CreateProgress(Guid jobId, long scanned)
        {
            return new StorageCleanupJobDto
            {
                JobId = jobId,
                UserId = Guid.NewGuid(),
                StorageId = Guid.NewGuid(),
                Status = StorageCleanupStatus.Running,
                Phase = StorageCleanupPhase.ScanningStorage,
                StartedAt = DateTime.UtcNow,
                StorageObjectsScanned = scanned
            };
        }

        private class BlockingTransport : IStorageCleanupProgressTransport
        {
            private int _activeSends;
            private int _maxConcurrentSends;
            private int _sendCount;

            public TaskCompletionSource<bool> FirstSendStarted { get; } =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            public TaskCompletionSource<bool> ReleaseFirstSend { get; } =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            public ConcurrentQueue<StorageCleanupJobDto> Reports { get; } = new();
            public int SendCount => Volatile.Read(ref _sendCount);
            public int MaxConcurrentSends => Volatile.Read(ref _maxConcurrentSends);

            public async Task SendAsync(
                StorageCleanupJobDto progress,
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

        private class ThrowingTransport : IStorageCleanupProgressTransport
        {
            private int _sendCount;
            public int SendCount => Volatile.Read(ref _sendCount);

            public Task SendAsync(
                StorageCleanupJobDto progress,
                CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref _sendCount);
                throw new InvalidOperationException("Transport unavailable.");
            }
        }
    }
}
