using EasyExtensions.EntityFrameworkCore.Abstractions;

namespace Octockup.Server.Database
{
    public class Snapshot : BaseEntity<Guid>
    {
        public Guid BackupId { get; set; }
        public virtual Backup Backup { get; set; } = null!;

        public ICollection<SnapshotFile> Files { get; set; } = [];
    }

    public record SnapshotFile(string Path, long Size, string Hashsum, ICollection<string> ChunkHashes);
}