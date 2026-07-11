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
        public async Task WriteAsync_AsyncEntries_SpoolsCentralDirectoryAndReportsProgress()
        {
            StoredZipArchiveEntry[] entries =
            [
                CreateEntry("first.txt", Encoding.UTF8.GetBytes("first")),
                CreateEntry("second.txt", Encoding.UTF8.GetBytes("second")),
                CreateEntry("third.txt", Encoding.UTF8.GetBytes("third"))
            ];
            List<(long Files, long Bytes)> progress = [];
            using MemoryStream archive = new();
            using MemoryStream centralDirectorySpool = new();

            long written = await StoredZipArchiveWriter.WriteAsync(
                archive,
                EnumerateAsync(entries),
                centralDirectorySpool,
                (files, bytes, _) =>
                {
                    progress.Add((files, bytes));
                    return Task.CompletedTask;
                },
                CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(written, Is.EqualTo(archive.Length));
                Assert.That(centralDirectorySpool.Length, Is.GreaterThan(0));
                Assert.That(progress, Is.EqualTo(new[]
                {
                    (1L, 5L),
                    (2L, 11L),
                    (3L, 16L)
                }));
            });

            archive.Position = 0;
            using ZipArchive zip = new(archive, ZipArchiveMode.Read, leaveOpen: true);
            Assert.That(zip.Entries.Select(x => x.FullName), Is.EqualTo(new[]
            {
                "first.txt",
                "second.txt",
                "third.txt"
            }));
        }

        [Test]
        public async Task WriteAsync_LargeEntry_ReportsBytesBeforeEntryCompletes()
        {
            byte[] content = new byte[9 * 1024 * 1024];
            StoredZipArchiveEntry[] entries = [CreateEntry("large.bin", content)];
            List<(long Files, long Bytes)> progress = [];
            using MemoryStream archive = new();
            using MemoryStream centralDirectorySpool = new();

            await StoredZipArchiveWriter.WriteAsync(
                archive,
                EnumerateAsync(entries),
                centralDirectorySpool,
                (files, bytes, _) =>
                {
                    progress.Add((files, bytes));
                    return Task.CompletedTask;
                },
                CancellationToken.None);

            Assert.Multiple(() =>
            {
                Assert.That(progress, Has.Count.EqualTo(2));
                Assert.That(progress[0], Is.EqualTo((0L, 8L * 1024 * 1024)));
                Assert.That(progress[1], Is.EqualTo((1L, (long)content.Length)));
            });
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

        private static async IAsyncEnumerable<StoredZipArchiveEntry> EnumerateAsync(
            IEnumerable<StoredZipArchiveEntry> entries)
        {
            foreach (StoredZipArchiveEntry entry in entries)
            {
                await Task.Yield();
                yield return entry;
            }
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
