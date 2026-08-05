// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Octockup.Server.Helpers;

namespace Octockup.Tests
{
    public class ChunkKeyIdentityTests
    {
        private const string Hash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        [TestCase(Hash)]
        [TestCase("v2-none-enc-" + Hash)]
        [TestCase("v2-none-plain-" + Hash)]
        [TestCase("v2-zstd-enc-" + Hash)]
        [TestCase("v2-zstd-plain-" + Hash)]
        public void Parse_WhenStorageKeyIsValid_ProducesStableIdentity(string storageKey)
        {
            ChunkKeyIdentity first = ChunkKeyIdentity.Parse(storageKey);
            ChunkKeyIdentity second = ChunkKeyIdentity.Parse(storageKey);

            Assert.That(first, Is.EqualTo(second));
        }

        [Test]
        public void Parse_WhenStorageKeyVariantsDiffer_PreservesExactKeyIdentity()
        {
            ChunkKeyIdentity legacy = ChunkKeyIdentity.Parse(Hash);
            ChunkKeyIdentity versioned = ChunkKeyIdentity.Parse("v2-zstd-enc-" + Hash);

            Assert.That(legacy, Is.Not.EqualTo(versioned));
        }

        [TestCase("not-a-hash")]
        [TestCase("0123456789ABCDEF0123456789abcdef0123456789abcdef0123456789abcdef")]
        public void Parse_WhenStorageKeyIsNotCanonical_Throws(string storageKey)
        {
            Assert.That(
                () => ChunkKeyIdentity.Parse(storageKey),
                Throws.TypeOf<FormatException>());
        }
    }
}
