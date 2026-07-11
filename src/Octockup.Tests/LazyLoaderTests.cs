// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Octockup.Server.Collections;
using System.Runtime.CompilerServices;

namespace Octockup.Tests
{
    public class LazyLoaderTests
    {
        [Test]
        public async Task ReadAllAsync_WhenSourceOutrunsConsumer_StopsAtBufferCapacity()
        {
            const int capacity = 3;
            using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(5));
            await using LazyLoader<int> loader = new(
                CreateSequence(100, timeout.Token),
                capacity,
                timeout.Token);

            while (loader.Total < capacity)
            {
                await Task.Delay(1, timeout.Token);
            }

            await Task.Delay(25, timeout.Token);

            Assert.That(loader.Total, Is.EqualTo(capacity));
        }

        [Test]
        public async Task ReadAllAsync_WhenSourceCompletes_ReturnsEveryItemAndFinalCount()
        {
            await using LazyLoader<int> loader = new(
                CreateSequence(5, CancellationToken.None),
                capacity: 2,
                CancellationToken.None);
            List<int> items = [];

            await foreach (int item in loader.ReadAllAsync())
            {
                items.Add(item);
            }

            Assert.Multiple(() =>
            {
                Assert.That(items, Is.EqualTo(new[] { 0, 1, 2, 3, 4 }));
                Assert.That(loader.Total, Is.EqualTo(5));
                Assert.That(loader.IsEnumerationCompleted, Is.True);
            });
        }

        private static async IAsyncEnumerable<int> CreateSequence(
            int count,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            for (int index = 0; index < count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return index;
                await Task.Yield();
            }
        }
    }
}
