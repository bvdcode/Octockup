// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Octockup.Server.Collections;

namespace Octockup.Tests
{
    public class LazyLoaderTests
    {
        private const int BatchSize = 4_096;
        private const int TotalItems = 5_086;
        private const int BufferedBatches = 2;

        [Test]
        public void GetBatches_WhenEnumerationPauses_YieldsBufferedPartialBatch()
        {
            using ManualResetEventSlim allItemsYielded = new();
            using ManualResetEventSlim allowCompletion = new();
            using LazyLoader<int> loader = new(
                Enumerate(allItemsYielded, allowCompletion),
                BatchSize * BufferedBatches);
            using IEnumerator<int[]> batches = loader
                .GetBatches(BatchSize, TimeSpan.FromSeconds(1))
                .GetEnumerator();
            List<int> received = [];

            try
            {
                Assert.That(batches.MoveNext(), Is.True);
                Assert.That(batches.Current, Has.Length.EqualTo(BatchSize));
                received.AddRange(batches.Current);
                Assert.That(allItemsYielded.Wait(TimeSpan.FromSeconds(5)), Is.True);

                Assert.That(batches.MoveNext(), Is.True);
                Assert.That(batches.Current, Has.Length.EqualTo(TotalItems - BatchSize));
                received.AddRange(batches.Current);

                Assert.Multiple(() =>
                {
                    Assert.That(received, Has.Count.EqualTo(TotalItems));
                    Assert.That(received, Is.EqualTo(Enumerable.Range(0, TotalItems)));
                    Assert.That(loader.IsEnumerationCompleted, Is.False);
                });
            }
            finally
            {
                allowCompletion.Set();
                Assert.That(
                    SpinWait.SpinUntil(() => loader.IsEnumerationCompleted, TimeSpan.FromSeconds(5)),
                    Is.True);
            }
        }

        [Test]
        public void GetBatches_WhenConsumerIsSlower_KeepsOnlyConfiguredItemsBuffered()
        {
            const int maxBufferedItems = 8;
            const int itemCount = 1_000;
            using LazyLoader<int> loader = new(Enumerable.Range(0, itemCount), maxBufferedItems);
            using IEnumerator<int[]> batches = loader
                .GetBatches(1, TimeSpan.FromSeconds(1))
                .GetEnumerator();

            Assert.That(batches.MoveNext(), Is.True);
            Assert.That(
                SpinWait.SpinUntil(
                    () => loader.Total >= maxBufferedItems + 1,
                    TimeSpan.FromSeconds(5)),
                Is.True);

            Thread.Sleep(100);

            Assert.Multiple(() =>
            {
                Assert.That(batches.Current, Is.EqualTo(new[] { 0 }));
                Assert.That(loader.Total, Is.LessThanOrEqualTo(maxBufferedItems + 1));
                Assert.That(loader.IsEnumerationCompleted, Is.False);
            });
        }

        [Test]
        public void GetBatches_WhenSequenceExceedsBuffer_ReturnsEveryItemInOrder()
        {
            const int maxBufferedItems = 8;
            const int maxBatchSize = 7;
            const int itemCount = 1_000;
            using LazyLoader<int> loader = new(Enumerable.Range(0, itemCount), maxBufferedItems);

            int[] received = loader
                .GetBatches(maxBatchSize, TimeSpan.FromSeconds(1))
                .SelectMany(batch => batch)
                .ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(received, Is.EqualTo(Enumerable.Range(0, itemCount)));
                Assert.That(loader.Total, Is.EqualTo(itemCount));
                Assert.That(loader.IsEnumerationCompleted, Is.True);
            });
        }

        [Test]
        public void Constructor_WhenBufferSizeIsInvalid_Throws()
        {
            Assert.That(
                () => new LazyLoader<int>([], 0),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        private static IEnumerable<int> Enumerate(
            ManualResetEventSlim allItemsYielded,
            ManualResetEventSlim allowCompletion)
        {
            for (int index = 0; index < TotalItems; index++)
            {
                yield return index;
            }

            allItemsYielded.Set();
            allowCompletion.Wait();
        }
    }
}
