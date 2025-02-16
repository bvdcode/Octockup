﻿using Octockup.Server.Models;

namespace Octockup.Server.Providers.Storage
{
    public class BlinkCameraProvider : IStorageProvider<BlinkCameraParameters>
    {
        public BlinkCameraParameters Parameters { get; set; } = null!;

        public string Name => "Blink Camera Storage";

        public IEnumerable<RemoteFileInfo> GetAllFiles(Action<int>? progressCallback = null, CancellationToken cancellationToken = default)
        {
            return [];
        }

        public Stream GetFileStream(RemoteFileInfo fileInfo)
        {
            throw new NotImplementedException();
        }
    }

    public class BlinkCameraParameters
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
