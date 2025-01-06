using FluentFTP;
using FluentFTP.Helpers;
using Octockup.Server.Models;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Octockup.Server.Providers.Storage
{
    public class FtpProvider(ILogger<FtpProvider> _logger) : IStorageProvider<BaseStorageParameters>
    {
        public string Name => "FTP - File Transfer Protocol";
        public BaseStorageParameters Parameters { get; set; } = null!;

        private FtpClient? _client;

        private static readonly string[] ignored = ["httpcache", "inventorymsgcache", "StreamingAssets"];

        public IEnumerable<RemoteFileInfo> GetAllFiles(Action<int>? progressCallback = null, CancellationToken cancellationToken = default)
        {
            return GetAllFiles(Parameters.RemotePath, progressCallback, cancellationToken: cancellationToken);
        }

        public Stream GetFileStream(RemoteFileInfo fileInfo)
        {
            _client ??= CreateClient();
            return _client.OpenRead(fileInfo.Path, FtpDataType.Binary);
        }

        private IEnumerable<RemoteFileInfo> GetAllFiles(string remotePath,
            Action<int>? progressCallback = null, CancellationToken cancellationToken = default)
        {
            int counter = 0;
            _client ??= CreateClient();
            _logger.LogDebug("Getting files from {path}", remotePath);
            var files = _client.GetListing(remotePath);
            _logger.LogInformation("Got {count} items from {path}, total files: {total}",
                files.Length, remotePath, counter);
            foreach (var file in files)
            {
                if (ignored.Any(i => Regex.IsMatch(file.Name, i)))
                {
                    _logger.LogInformation("Ignoring item: {file}", file.Name);
                    continue;
                }
                cancellationToken.ThrowIfCancellationRequested();
                if (file.FullName == "."
                    || file.FullName == ".."
                    || file.FullName == remotePath)
                {
                    continue;
                }
                if (file.Type == FtpObjectType.Directory)
                {
                    foreach (var item in GetAllFiles(file.FullName, progressCallback, cancellationToken))
                    {
                        yield return item;
                        counter++;
                        progressCallback?.Invoke(counter);
                    }
                    continue;
                }
                if (file.Type != FtpObjectType.File)
                {
                    continue;
                }
                var local = file.Modified.ToLocalTime();
                yield return new RemoteFileInfo
                {
                    Name = file.Name,
                    Size = file.Size,
                    Path = file.FullName,
                    LastModified = DateTime.SpecifyKind(file.Modified, DateTimeKind.Utc),
                    FileCreatedAt = DateTime.SpecifyKind(file.Created, DateTimeKind.Utc)
                };
                counter++;
                progressCallback?.Invoke(counter);
            }
        }

        private FtpClient CreateClient()
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(Parameters.RemoteHost, nameof(Parameters.RemoteHost));
            ArgumentException.ThrowIfNullOrWhiteSpace(Parameters.Username, nameof(Parameters.Username));
            ArgumentException.ThrowIfNullOrWhiteSpace(Parameters.Password, nameof(Parameters.Password));
            ArgumentOutOfRangeException.ThrowIfLessThan(Parameters.RemotePort, 1, nameof(Parameters.RemotePort));
            var client = new FtpClient(Parameters.RemoteHost, Parameters.Username, Parameters.Password, Parameters.RemotePort);
            client.AutoConnect();
            try
            {
                client.Config.TimeConversion = FtpDate.UTC;
                client.Config.ListingCulture = CultureInfo.InvariantCulture;
                client.Capabilities.Add(FtpCapability.MDTM);

                var files = client.GetListing(Parameters.RemotePath);
                var file = files.FirstOrDefault(x => x.Modified != DateTime.MinValue && x.Type == FtpObjectType.File);
                if (file != null)
                {
                    DateTime serverTime = client.GetModifiedTime(file.FullName);
                    TimeSpan serverTimeZone = file.Modified - serverTime;
                    serverTimeZone = TimeSpan.FromMinutes(Math.Round(serverTimeZone.TotalMinutes / 30) * 30);
                    TimeZoneInfo timeZoneInfo = TimeZoneInfo.CreateCustomTimeZone("ServerTimeZone", serverTimeZone, "ServerTimeZone", "ServerTimeZone");
                    client.Config.ServerTimeZone = timeZoneInfo;
                    _logger.LogInformation("Server timezone set to {timezone}", serverTimeZone);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to set MDTM capability");
            }
            return client;
        }
    }
}