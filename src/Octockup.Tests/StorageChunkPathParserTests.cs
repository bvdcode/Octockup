// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.Models.Enums;
using Octockup.Server.Helpers;

namespace Octockup.Tests
{
    public class StorageChunkPathParserTests
    {
        private const string Hash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        [Test]
        public void TryParse_WhenPathUsesLegacyLayout_ReturnsPlainHashKey()
        {
            string path = ChunkStorageHelpers.GetStoragePath(Hash, '/');

            bool result = StorageChunkPathParser.TryParse(path, '/', out string? chunkKey);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(chunkKey, Is.EqualTo(Hash));
            });
        }

        [Test]
        public void TryParse_WhenPathUsesVersionedLayout_ReturnsVersionedChunkKey()
        {
            string key = ChunkStorageHelpers.CreateKey(
                Hash,
                CompressionAlgorithm.None,
                isEncrypted: false);
            string path = ChunkStorageHelpers.GetStoragePath(key, '/');

            bool result = StorageChunkPathParser.TryParse(path, '/', out string? chunkKey);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(chunkKey, Is.EqualTo(key));
            });
        }

        [Test]
        public void TryParse_WhenPathIsNotChunk_ReturnsFalse()
        {
            bool result = StorageChunkPathParser.TryParse("exports/readme.txt", '/', out string? chunkKey);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.False);
                Assert.That(chunkKey, Is.Null);
            });
        }
    }
}
