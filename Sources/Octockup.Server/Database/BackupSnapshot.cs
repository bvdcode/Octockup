using System.ComponentModel.DataAnnotations.Schema;
using EasyExtensions.EntityFrameworkCore.Abstractions;

namespace Octockup.Server.Database
{
    [Table("backup_snapshots")]
    public class BackupSnapshot : BaseEntity
    {
        [Column("files")]
        public string[] Files { get; set; } = [];

        [Column("backup_task_id")]
        public int BackupTaskId { get; set; }

        [Column("size")]
        public long Size { get; set; }

        [Column("log")]
        public string Log { get; set; } = string.Empty;

        public virtual BackupTask BackupTask { get; set; } = null!;
    }
}
