
namespace Octockup.Server.Models
{
    public class BackupFileInfo
    {
        public string Path { get; set; } = null!;
        public string? Name { get; set; } = null!;
		public long? Size { get; set; }
        public DateTime? LastModified { get; set; }
    }
}
