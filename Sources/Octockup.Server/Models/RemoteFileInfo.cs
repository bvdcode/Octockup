namespace Octockup.Server.Models
{
    public class RemoteFileInfo
    {
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
        public DateTime FileCreatedAt { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
    }
}