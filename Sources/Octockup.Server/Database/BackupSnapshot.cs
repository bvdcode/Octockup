using System.ComponentModel.DataAnnotations.Schema;
using EasyExtensions.EntityFrameworkCore.Abstractions;

namespace Octockup.Server.Database
{
    [Table("backup_snapshots")]
    public class BackupSnapshot : BaseEntity
    {
        [Column("backup_task_id")]
        public int BackupTaskId { get; set; }

        [Column("total_size")]
        public long TotalSize { get; set; }

        [Column("log")]
        public string Log { get; set; } = string.Empty;

        [Column("is_deleted")]
        public bool IsDeleted { get; set; }

        [Column("elapsed")]
        public TimeSpan Elapsed { get; set; }

        public virtual BackupTask BackupTask { get; set; } = null!;

        public virtual ICollection<SavedFile> SavedFiles { get; set; } = null!;
    }
}
