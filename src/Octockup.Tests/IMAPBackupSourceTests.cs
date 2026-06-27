// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using MailKit.Security;
using Microsoft.Extensions.Logging;
using Octockup.Server.Models;
using Octockup.Server.Modules;

namespace Octockup.Tests
{
    public class IMAPBackupSourceTests
    {
        private IMAPSource _imap;
        private ILogger<IMAPSource> _logger;

        [TearDown]
        public void DisposeStorage()
        {
            if (_imap is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        [SetUp]
        public void Setup()
        {
            _logger = new TestContextLogger<IMAPSource>();
            _imap = new IMAPSource(_logger);
            Dictionary<string, string> parameters = new()
            {
                { "host", GetRequiredEnvironmentVariable("OCTOCKUP_TEST_IMAP_HOST") },
                { "port", Environment.GetEnvironmentVariable("OCTOCKUP_TEST_IMAP_PORT") ?? "993" },
                { "username", GetRequiredEnvironmentVariable("OCTOCKUP_TEST_IMAP_USERNAME") },
                { "password", GetRequiredEnvironmentVariable("OCTOCKUP_TEST_IMAP_PASSWORD") },
                { "path", Environment.GetEnvironmentVariable("OCTOCKUP_TEST_IMAP_PATH") ?? "/" },
                { "skipPermissionDenied", "true" }
            };
            _imap.SetParameters(parameters);
        }

        [Test]
        public void IMAPSource_GetDirectories_Root_NotEmpty()
        {
            List<string> directories = GetOrSkip(() => _imap.GetDirectories(recursive: true).ToList());
            Assert.That(directories.Any());
        }

        [Test]
        public void IMAPSource_GetFiles_Root_NotEmpty()
        {
            List<BackupFileInfo> files = GetOrSkip(() => _imap.GetFiles(recursive: true).ToList());
            Assert.That(files.Any());
        }

        [Test]
        public async Task IMAPSource_GetFileStream_Success()
        {
            List<BackupFileInfo> files = GetOrSkip(() => _imap.GetFiles(recursive: true).ToList());
            Assert.That(files.Any());
            BackupFileInfo firstFile = files.First();
            using Stream stream = await GetOrSkipAsync(() => _imap.GetFileStreamAsync(firstFile));
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
            catch (AuthenticationException ex)
            {
                Assert.Ignore("IMAP credentials are invalid: " + ex.Message);
                throw;
            }
            catch (IOException ex)
            {
                Assert.Ignore("IMAP service is unavailable: " + ex.Message);
                throw;
            }
        }

        private static async Task<T> GetOrSkipAsync<T>(Func<Task<T>> action)
        {
            try
            {
                return await action();
            }
            catch (AuthenticationException ex)
            {
                Assert.Ignore("IMAP credentials are invalid: " + ex.Message);
                throw;
            }
            catch (IOException ex)
            {
                Assert.Ignore("IMAP service is unavailable: " + ex.Message);
                throw;
            }
        }

        private class TestContextLogger<T> : ILogger<T>
        {
            IDisposable ILogger.BeginScope<TState>(TState state) => NullScope.Instance;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                try
                {
                    var message = formatter(state, exception);
                    var timestamp = DateTime.UtcNow.ToString("o");
                    var combined = $"{timestamp} [{logLevel}] {message}" + (exception != null ? $"\n{exception}" : string.Empty);

                    // Write to multiple outputs because different NUnit runners capture different streams
                    NUnit.Framework.TestContext.Progress.WriteLine(combined);
                    NUnit.Framework.TestContext.Out.WriteLine(combined);
                }
                catch
                {
                    // swallow logging errors in tests
                }
            }

            private class NullScope : IDisposable
            {
                public static NullScope Instance { get; } = new NullScope();
                public void Dispose() { }
            }
        }
    }
}
