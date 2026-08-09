// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Octockup.Server.Jobs;
using Octockup.Server.Models.Enums;

namespace Octockup.Tests
{
    public class StorageCleanupExecutionOptionsTests
    {
        [TestCase(StorageCleanupSpeed.Normal, 250, 50)]
        [TestCase(StorageCleanupSpeed.Faster, 10_000, 5)]
        public void Create_ReturnsConfiguredLimits(
            StorageCleanupSpeed speed,
            int expectedBatchSize,
            int expectedDelayMilliseconds)
        {
            StorageCleanupExecutionOptions options = StorageCleanupExecutionOptions.Create(speed);

            Assert.Multiple(() =>
            {
                Assert.That(options.DeleteBatchSize, Is.EqualTo(expectedBatchSize));
                Assert.That(
                    options.DeleteDelay,
                    Is.EqualTo(TimeSpan.FromMilliseconds(expectedDelayMilliseconds)));
            });
        }

        [Test]
        public void Create_WhenSpeedIsInvalid_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                StorageCleanupExecutionOptions.Create((StorageCleanupSpeed)99));
        }
    }
}
