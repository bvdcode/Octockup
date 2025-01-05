﻿using Octockup.Server.Models;

namespace Octockup.Server.Providers.Storage
{
    public class ScpProvider : IStorageProvider<BaseStorageParameters>
    {
        public string Name => "SCP - Secure Copy Protocol";
        public BaseStorageParameters Parameters { get; set; } = null!;

        public IEnumerable<RemoteFileInfo> GetAllFiles()
        {
            throw new NotImplementedException();
        }
    }
}
