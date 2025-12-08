using Octockup.Server.Modules;
using Microsoft.Extensions.Logging;

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
                { "host", "imap.gmail.com" },
                { "port", "993" },
                { "username", "test@gmail.com" },
                { "password", "1234" },
                { "path", "/" },
                { "skipPermissionDenied", "true" }
            };
            _imap.SetParameters(parameters);
        }

        [Test]
        public void IMAPSource_GetDirectories_Root_NotEmpty()
        {
            var directories = _imap.GetDirectories(recursive: true);
            Assert.That(directories.Any());
        }

        [Test]
        public async Task IMAPSource_GetFiles_Root_NotEmpty()
        {
            var files = _imap.GetFiles(recursive: true);
            Assert.That(files.Any());
        }

        [Test]
        public async Task IMAPSource_GetFileStream_Success()
        {
            var files = _imap.GetFiles(recursive: true);
            Assert.That(files.Any());
            var firstFile = files.First();
            using var stream = await _imap.GetFileStreamAsync(firstFile);
            Assert.That(stream.Length, Is.GreaterThan(0));
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
