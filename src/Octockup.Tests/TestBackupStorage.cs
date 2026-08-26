// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Octockup.Server.Abstractions;
using Octockup.Server.Models;

namespace Octockup.Tests
{
    public class TestBackupStorage : IBackupStorage
    {
        public const string EmptyMode = "empty";
        public const string OutOfMemoryMode = "out-of-memory";
        private string _mode = EmptyMode;

        public string Id => typeof(TestBackupStorage).FullName!;
        public string Name => "Backup runner state test provider";
        public char PathSeparator => '/';
        public IEnumerable<string> RequiredParameters => ["mode"];

        public void SetParameters(IReadOnlyDictionary<string, string> parameters)
        {
            _mode = parameters["mode"];
        }

        public void SetIgnoredPaths(ICollection<string>? ignoredPaths)
        {
        }

        public Task<BackupFileInfo?> GetFileInfoAsync(
            string path,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<BackupFileInfo?>(null);
        }

        public Task<Stream> GetFileStreamAsync(
            BackupFileInfo file,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Stream>(Stream.Null);
        }

        public IEnumerable<string> GetDirectories(
            bool recursive = false,
            CancellationToken cancellationToken = default)
        {
            return [];
        }

        public IEnumerable<BackupFileInfo> GetFiles(
            bool recursive = false,
            CancellationToken cancellationToken = default)
        {
            if (_mode == OutOfMemoryMode)
            {
                throw new OutOfMemoryException("Synthetic out-of-memory failure.");
            }

            return [];
        }

        public Task<bool?> ExistsAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<bool?>(false);
        }

        public Task<bool?> DeleteAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<bool?>(false);
        }

        public Task UploadAsync(
            string path,
            Stream data,
            CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
