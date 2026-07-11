// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Octockup.Server.Abstractions;
using Octockup.Server.Models;
using System.Runtime.CompilerServices;

namespace Octockup.Tests
{
    internal class TestStorage : IBackupStorage, IBackupStorageInventory
    {
        public string Id => GetType().FullName!;
        public string Name => nameof(TestStorage);
        public char PathSeparator => '/';
        public IEnumerable<string> RequiredParameters => [];
        public Dictionary<string, BackupFileInfo> Files { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, byte[]> Contents { get; } = new(StringComparer.Ordinal);
        public List<string?> InventoryCursors { get; } = [];
        public Action<BackupFileInfo>? BeforeInventoryFileYielded { get; set; }

        public void SetParameters(IReadOnlyDictionary<string, string> parameters)
        {
        }

        public void SetIgnoredPaths(ICollection<string>? ignoredPaths)
        {
        }

        public Task<BackupFileInfo?> GetFileInfoAsync(
            string path,
            CancellationToken cancellationToken) => Task.FromResult(Files.GetValueOrDefault(path));

        public Task<Stream> GetFileStreamAsync(
            BackupFileInfo file,
            CancellationToken cancellationToken = default)
        {
            Stream stream = Contents.TryGetValue(file.Path, out byte[]? content)
                ? new MemoryStream(content, writable: false)
                : Stream.Null;
            return Task.FromResult(stream);
        }

        public IEnumerable<string> GetDirectories(
            bool recursive = false,
            CancellationToken cancellationToken = default) => [];

        public IEnumerable<BackupFileInfo> GetFiles(
            bool recursive = false,
            CancellationToken cancellationToken = default) => Files.Values.ToList();

        public async IAsyncEnumerable<BackupFileInfo> GetFilesAsync(
            bool recursive = false,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (BackupFileInfo file in GetFilesAfterAsync(
                null,
                recursive,
                cancellationToken))
            {
                yield return file;
            }
        }

        public async IAsyncEnumerable<BackupFileInfo> GetFilesAfterAsync(
            string? afterPath,
            bool recursive = false,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            InventoryCursors.Add(afterPath);
            foreach (BackupFileInfo file in Files.Values
                .Where(x => string.IsNullOrEmpty(afterPath) ||
                    string.CompareOrdinal(x.Path, afterPath) > 0)
                .OrderBy(x => x.Path, StringComparer.Ordinal)
                .ToList())
            {
                cancellationToken.ThrowIfCancellationRequested();
                BeforeInventoryFileYielded?.Invoke(file);
                cancellationToken.ThrowIfCancellationRequested();
                yield return file;
                await Task.Yield();
            }
        }

        public Task<bool?> ExistsAsync(
            string path,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<bool?>(Files.ContainsKey(path));

        public Task<bool?> DeleteAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            bool removed = Files.Remove(path);
            Contents.Remove(path);
            return Task.FromResult<bool?>(removed);
        }

        public Task UploadAsync(
            string path,
            Stream data,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
