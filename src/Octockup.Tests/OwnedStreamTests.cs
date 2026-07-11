// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Octockup.Server.Streams;

namespace Octockup.Tests
{
    public class OwnedStreamTests
    {
        [Test]
        public void Dispose_DisposesInnerStreamAndOwner()
        {
            MemoryStream inner = new();
            CancellationTokenSource owner = new();
            OwnedStream stream = new(inner, owner);

            stream.Dispose();

            Assert.Multiple(() =>
            {
                Assert.That(inner.CanRead, Is.False);
                Assert.Throws<ObjectDisposedException>(owner.Cancel);
            });
        }

        [Test]
        public async Task DisposeAsync_DisposesInnerStreamAndOwner()
        {
            MemoryStream inner = new();
            CancellationTokenSource owner = new();
            OwnedStream stream = new(inner, owner);

            await stream.DisposeAsync();

            Assert.Multiple(() =>
            {
                Assert.That(inner.CanRead, Is.False);
                Assert.Throws<ObjectDisposedException>(owner.Cancel);
            });
        }
    }
}
