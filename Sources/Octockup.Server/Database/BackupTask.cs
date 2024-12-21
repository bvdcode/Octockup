using Octockup.Server.Models.Enums;
using System.ComponentModel.DataAnnotations.Schema;
using EasyExtensions.EntityFrameworkCore.Abstractions;

namespace Octockup.Server.Database
{
    [Table("backup_tasks")]
    public class BackupTask : BaseEntity
    {
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("progress")]
        public double Progress { get; set; }

        [Column("interval")]
        public TimeSpan Interval { get; set; }

        [Column("first_run")]
        public DateTime FirstRun { get; set; }

        [Column("last_run")]
        public DateTime? LastRun { get; set; }

        [Column("is_enabled")]
        public bool IsEnabled { get; set; }

        [Column("is_deleted")]
        public bool IsDeleted { get; set; }

        [Column("status")]
        public BackupTaskStatus Status { get; set; }

        [Column("user_id")]
        public int UserId { get; set; }

        [Column("provider")]
        public string Provider { get; set; } = string.Empty;

        public virtual User User { get; set; } = null!;

    }
}
