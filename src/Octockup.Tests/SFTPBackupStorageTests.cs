// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.Extensions.Logging.Abstractions;
using Octockup.Server.Models;
using Octockup.Server.Modules;

namespace Octockup.Tests
{
    [Explicit("Requires configured external SFTP credentials.")]
    [Category("External")]
    public class SFTPBackupStorageTestsTests
    {
        private SFTPBackupStorage _storage;

        [TearDown]
        public void DisposeStorage()
        {
            if (_storage is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        [SetUp]
        public void Setup()
        {
            _storage = new SFTPBackupStorage(new NullLogger<SFTPBackupStorage>());
            Dictionary<string, string> parameters = new()
            {
                { "host", "1.2.3.4" },
                { "port", "22" },
                { "username", "test" },
                { "password", "123" },
                { "path", "/" },
                { "skipPermissionDenied", "true" }
            };
            _storage.SetParameters(parameters);
        }

        [Test]
        public void SftpStorage_GetFiles_Root_NotEmpty()
        {
            IEnumerable<BackupFileInfo> files = _storage.GetFiles(recursive: true);
            Assert.That(files.Any());
        }

        [Test]
        public async Task SftpStorage_GetFileStream_Success()
        {
            IEnumerable<BackupFileInfo> files = _storage.GetFiles(recursive: true);
            Assert.That(files.Any());
            BackupFileInfo firstFile = files.First();
            using Stream stream = await _storage.GetFileStreamAsync(firstFile);
            Assert.That(stream.Length, Is.GreaterThan(0));
        }
    }
}
