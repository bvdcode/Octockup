// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Octockup.Server.Abstractions;
using Octockup.Server.Models;
using Octockup.Server.Models.Dto;
using Octockup.Server.Models.Enums;
using Octockup.Server.Models.Options;
using System.Collections.Concurrent;

namespace Octockup.Tests
{
    public class ScheduleReportTests
    {
        [Test]
        public async Task BackgroundReporting_WhenPublisherIsSlow_CoalescesUpdatesWithoutConcurrentSends()
        {
            BlockingPublisher publisher = new();
            await using ScheduleReport report = CreateReport(publisher);
            report.Update(0, "Starting", stage: BackupProgressStage.Listing);
            report.StartBackgroundReporting(CancellationToken.None);

            await publisher.FirstSendStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
            for (int index = 1; index <= 10_000; index++)
            {
                report.Update(
                    index,
                    "Processing",
                    processedBytes: 1,
                    stage: BackupProgressStage.Uploading);
            }

            Assert.That(publisher.SendCount, Is.EqualTo(1));
            publisher.ReleaseFirstSend.TrySetResult(true);
            await Task.Delay(40);
            await report.PublishFinalAsync(
                10_000,
                "Completed",
                ScheduleStatus.Completed,
                BackupProgressStage.Completed,
                CancellationToken.None);

            ScheduleReportDto finalReport = publisher.Reports.Last();
            Assert.Multiple(() =>
            {
                Assert.That(publisher.MaxConcurrentSends, Is.EqualTo(1));
                Assert.That(publisher.SendCount, Is.LessThan(10));
                Assert.That(finalReport.Processed, Is.EqualTo(10_000));
                Assert.That(finalReport.ProcessedBytes, Is.EqualTo(10_000));
                Assert.That(finalReport.Status, Is.EqualTo(ScheduleStatus.Completed));
                Assert.That(finalReport.Stage, Is.EqualTo(BackupProgressStage.Completed));
            });
        }

        [Test]
        public async Task PublishFinalAsync_WhenTransportFails_DoesNotFailBackupProgressLifecycle()
        {
            ThrowingPublisher publisher = new();
            await using ScheduleReport report = CreateReport(publisher);
            report.Update(1, "Processing", stage: BackupProgressStage.Uploading);
            report.StartBackgroundReporting(CancellationToken.None);
            await Task.Delay(30);

            Assert.DoesNotThrowAsync(async () => await report.PublishFinalAsync(
                1,
                "Failed",
                ScheduleStatus.Failed,
                BackupProgressStage.Failed,
                CancellationToken.None));
            Assert.That(publisher.SendCount, Is.GreaterThanOrEqualTo(2));
        }

        private static ScheduleReport CreateReport(IScheduleProgressPublisher publisher)
        {
            BackupProgressOptions options = new()
            {
                PublishInterval = TimeSpan.FromMilliseconds(10),
                AggregateLogInterval = TimeSpan.FromMinutes(1)
            };
            return new ScheduleReport(
                Guid.NewGuid(),
                Guid.NewGuid(),
                Guid.NewGuid(),
                publisher,
                Options.Create(options),
                TimeProvider.System,
                NullLogger<ScheduleReport>.Instance);
        }

        private class BlockingPublisher : IScheduleProgressPublisher
        {
            private int _activeSends;
            private int _maxConcurrentSends;
            private int _sendCount;

            public TaskCompletionSource<bool> FirstSendStarted { get; } =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            public TaskCompletionSource<bool> ReleaseFirstSend { get; } =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            public ConcurrentQueue<ScheduleReportDto> Reports { get; } = new();
            public int SendCount => Volatile.Read(ref _sendCount);
            public int MaxConcurrentSends => Volatile.Read(ref _maxConcurrentSends);

            public async Task PublishAsync(
                ScheduleReportDto report,
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

                    Reports.Enqueue(report);
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

        private class ThrowingPublisher : IScheduleProgressPublisher
        {
            private int _sendCount;
            public int SendCount => Volatile.Read(ref _sendCount);

            public Task PublishAsync(
                ScheduleReportDto report,
                CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref _sendCount);
                throw new InvalidOperationException("Transport unavailable.");
            }
        }
    }
}
