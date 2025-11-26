using Microsoft.EntityFrameworkCore;
using EasyExtensions.EntityFrameworkCore.Abstractions;

namespace Octockup.Server.Database
{
    [Index(nameof(Tag), IsUnique = true)]
    public class Backup : BaseEntity<Guid>
    {
        public Guid UserId { get; set; }
        public Guid SourceId { get; set; }
        public Guid StorageId { get; set; }
        public string Tag { get; set; } = string.Empty;
        public ICollection<string> IgnoredPaths { get; set; } = [];

        public virtual User User { get; set; } = null!;
        public virtual Module Source { get; set; } = null!;
        public virtual Module Storage { get; set; } = null!;
    }
}
