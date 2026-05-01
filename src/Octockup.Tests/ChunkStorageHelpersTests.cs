// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.Models.Enums;
using Octockup.Server.Helpers;

namespace Octockup.Tests
{
    public class ChunkStorageHelpersTests
    {
        private const string Hash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        [Test]
        public void CreateKey_DefaultFormat_UsesLegacyHash()
        {
            string key = ChunkStorageHelpers.CreateKey(
                Hash,
                CompressionHelpers.Algorithm,
                isEncrypted: true);

            var descriptor = ChunkStorageHelpers.Parse(key);

            Assert.Multiple(() =>
            {
                Assert.That(key, Is.EqualTo(Hash));
                Assert.That(descriptor.ContentHash, Is.EqualTo(Hash));
                Assert.That(descriptor.CompressionAlgorithm, Is.EqualTo(CompressionHelpers.Algorithm));
                Assert.That(descriptor.IsEncrypted, Is.True);
            });
        }

        [Test]
        public void CreateKey_NonDefaultFormat_StoresCompressionAndEncryptionMarkers()
        {
            string key = ChunkStorageHelpers.CreateKey(
                Hash,
                CompressionAlgorithm.None,
                isEncrypted: false);

            var descriptor = ChunkStorageHelpers.Parse(key);
            string path = ChunkStorageHelpers.GetStoragePath(key, '/');

            Assert.Multiple(() =>
            {
                Assert.That(key, Is.EqualTo("v2-none-plain-" + Hash));
                Assert.That(descriptor.ContentHash, Is.EqualTo(Hash));
                Assert.That(descriptor.CompressionAlgorithm, Is.EqualTo(CompressionAlgorithm.None));
                Assert.That(descriptor.IsEncrypted, Is.False);
                Assert.That(path, Is.EqualTo("v2/none/plain/01/23/456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef.oct"));
            });
        }
    }
}
