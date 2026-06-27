// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.Extensions.Logging.Abstractions;
using Octockup.Server.Models;
using Octockup.Server.Modules;
using Renci.SshNet.Common;
using System.Net.Sockets;

namespace Octockup.Tests
{
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
                { "host", GetRequiredEnvironmentVariable("OCTOCKUP_TEST_SFTP_HOST") },
                { "port", Environment.GetEnvironmentVariable("OCTOCKUP_TEST_SFTP_PORT") ?? "22" },
                { "username", GetRequiredEnvironmentVariable("OCTOCKUP_TEST_SFTP_USERNAME") },
                { "password", GetRequiredEnvironmentVariable("OCTOCKUP_TEST_SFTP_PASSWORD") },
                { "path", Environment.GetEnvironmentVariable("OCTOCKUP_TEST_SFTP_PATH") ?? "/" },
                { "skipPermissionDenied", "true" }
            };
            _storage.SetParameters(parameters);
        }

        [Test]
        public void SftpStorage_GetFiles_Root_NotEmpty()
        {
            List<BackupFileInfo> files = GetOrSkip(() => _storage.GetFiles(recursive: true).ToList());
            Assert.That(files.Any());
        }

        [Test]
        public async Task SftpStorage_GetFileStream_Success()
        {
            List<BackupFileInfo> files = GetOrSkip(() => _storage.GetFiles(recursive: true).ToList());
            Assert.That(files.Any());
            BackupFileInfo firstFile = files.First();
            using Stream stream = await GetOrSkipAsync(() => _storage.GetFileStreamAsync(firstFile));
            Assert.That(stream.Length, Is.GreaterThan(0));
        }

        private static string GetRequiredEnvironmentVariable(string name)
        {
            string? value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrWhiteSpace(value))
            {
                Assert.Ignore($"{name} is not configured.");
            }

            return value;
        }

        private static T GetOrSkip<T>(Func<T> action)
        {
            try
            {
                return action();
            }
            catch (SshAuthenticationException ex)
            {
                Assert.Ignore("SFTP credentials are invalid: " + ex.Message);
                throw;
            }
            catch (SshConnectionException ex)
            {
                Assert.Ignore("SFTP service is unavailable: " + ex.Message);
                throw;
            }
            catch (SocketException ex)
            {
                Assert.Ignore("SFTP service is unavailable: " + ex.Message);
                throw;
            }
        }

        private static async Task<T> GetOrSkipAsync<T>(Func<Task<T>> action)
        {
            try
            {
                return await action();
            }
            catch (SshAuthenticationException ex)
            {
                Assert.Ignore("SFTP credentials are invalid: " + ex.Message);
                throw;
            }
            catch (SshConnectionException ex)
            {
                Assert.Ignore("SFTP service is unavailable: " + ex.Message);
                throw;
            }
            catch (SocketException ex)
            {
                Assert.Ignore("SFTP service is unavailable: " + ex.Message);
                throw;
            }
        }
    }
}
