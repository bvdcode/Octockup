// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Octockup.Server.Archives;
using System.IO.Compression;
using System.Text;

namespace Octockup.Tests
{
    public class StoredZipArchiveWriterTests
    {
        [Test]
        public async Task WriteAsync_StoredZip64Archive_ContentLengthMatchesAndZipIsReadable()
        {
            byte[] first = Encoding.UTF8.GetBytes("hello");
            byte[] second = Encoding.UTF8.GetBytes("world!");
            StoredZipArchiveEntry[] entries =
            [
                CreateEntry("folder/first.txt", first),
                CreateEntry("folder/nested/second.txt", second),
            ];

            long expectedLength = StoredZipArchiveWriter.CalculateContentLength(entries);
            using var archive = new MemoryStream();

            await StoredZipArchiveWriter.WriteAsync(archive, entries);

            Assert.That(archive.Length, Is.EqualTo(expectedLength));

            archive.Position = 0;
            using var zip = new ZipArchive(archive, ZipArchiveMode.Read, leaveOpen: true);

            Assert.That(zip.Entries.Select(x => x.FullName), Is.EquivalentTo(new[]
            {
                "folder/first.txt",
                "folder/nested/second.txt",
            }));
            Assert.That(await ReadEntryAsync(zip, "folder/first.txt"), Is.EqualTo("hello"));
            Assert.That(await ReadEntryAsync(zip, "folder/nested/second.txt"), Is.EqualTo("world!"));
        }

        [Test]
        public void CalculateContentLength_LargeEntry_UsesZip64SizedHeaders()
        {
            const long largeSize = (long)uint.MaxValue + 1;
            var entry = new StoredZipArchiveEntry(
                "large.bin",
                largeSize,
                null,
                _ => throw new InvalidOperationException("The stream should not be opened for length calculation."));

            long length = StoredZipArchiveWriter.CalculateContentLength([entry]);

            Assert.That(length, Is.EqualTo(largeSize + 264));
        }

        [Test]
        public void NormalizeEntryName_RemovesAbsoluteAndTraversalSegments()
        {
            string result = StoredZipArchiveWriter.NormalizeEntryName("/root/../file.txt", "fallback.txt");

            Assert.That(result, Is.EqualTo("root/__/file.txt"));
        }

        private static StoredZipArchiveEntry CreateEntry(string name, byte[] data)
        {
            return new StoredZipArchiveEntry(
                name,
                data.Length,
                new DateTime(2026, 6, 16, 12, 30, 0, DateTimeKind.Utc),
                _ => Task.FromResult<Stream>(new MemoryStream(data, writable: false)));
        }

        private static async Task<string> ReadEntryAsync(ZipArchive zip, string name)
        {
            var entry = zip.GetEntry(name);
            Assert.That(entry, Is.Not.Null);

            using var reader = new StreamReader(entry!.Open(), Encoding.UTF8);
            return await reader.ReadToEndAsync();
        }
    }
}
