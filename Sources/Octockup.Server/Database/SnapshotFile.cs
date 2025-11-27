using EasyExtensions.EntityFrameworkCore.Abstractions;

namespace Octockup.Server.Database
{
    public class SnapshotFile : BaseEntity<Guid>
    {
        public long Size { get; set; }
        public string Path { get; set; } = string.Empty;
        public string Hashsum { get; set; } = string.Empty;
        public ICollection<string> ChunkHashes { get; set; } = [];
    }
}