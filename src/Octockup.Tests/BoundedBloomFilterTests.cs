// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Octockup.Server.Collections;

namespace Octockup.Tests
{
    public class BoundedBloomFilterTests
    {
        [Test]
        public void AddedValues_NeverProduceFalseNegative()
        {
            BoundedBloomFilter filter = new(20_000, 10_000);
            string[] values = Enumerable.Range(0, 10_000)
                .Select(index => "chunk-" + index.ToString("D8"))
                .ToArray();

            foreach (string value in values)
            {
                filter.Add(value);
            }

            Assert.Multiple(() =>
            {
                Assert.That(values, Has.All.Matches<string>(filter.MightContain));
                Assert.That(filter.ByteCount, Is.EqualTo(20_000));
                Assert.That(filter.HashFunctionCount, Is.InRange(1, 12));
            });
        }

        [Test]
        public void MemorySize_RemainsFixedForLargeExpectedItemCount()
        {
            BoundedBloomFilter filter = new(1_048_576, 100_000_000);

            Assert.Multiple(() =>
            {
                Assert.That(filter.ByteCount, Is.EqualTo(1_048_576));
                Assert.That(filter.HashFunctionCount, Is.InRange(1, 12));
            });
        }
    }
}
