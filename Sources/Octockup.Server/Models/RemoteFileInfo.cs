namespace Octockup.Server.Models
{
    public record RemoteFileInfo
    {
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
    }
}