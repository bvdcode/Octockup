// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Octockup.Server.Collections;

namespace Octockup.Tests
{
    public class LazyLoaderTests
    {
        private const int BatchSize = 4_096;
        private const int TotalItems = 5_086;

        [Test]
        public void GetBatches_WhenEnumerationPauses_YieldsBufferedPartialBatch()
        {
            using ManualResetEventSlim allItemsYielded = new();
            using ManualResetEventSlim allowCompletion = new();
            using LazyLoader<int> loader = new(Enumerate(allItemsYielded, allowCompletion));
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
