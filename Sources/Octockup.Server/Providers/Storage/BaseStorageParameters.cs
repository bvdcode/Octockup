namespace Octockup.Server.Providers.Storage
{
    public class BaseStorageParameters
    {
        public string RemoteHost { get; set; } = string.Empty;
        public int RemotePort { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string RemotePath { get; set; } = string.Empty;
    }
}