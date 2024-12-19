using System.ComponentModel.DataAnnotations.Schema;
using EasyExtensions.EntityFrameworkCore.Abstractions;

namespace Octockup.Server.Database
{
    [Table("backup_tasks")]
    public class BackupTask : BaseEntity
    {
        [Column("name")]
        public string Name { get; set; } = null!;

        [Column("interval")]
        public TimeSpan Interval { get; set; }

        [Column("first_run")]
        public DateTime FirstRun { get; set; }

        [Column("last_run")]
        public DateTime? LastRun { get; set; }

        [Column("is_running")]
        public bool IsRunning { get; set; }

        [Column("is_enabled")]
        public bool IsEnabled { get; set; }

        [Column("is_deleted")]
        public bool IsDeleted { get; set; }
    }
}
