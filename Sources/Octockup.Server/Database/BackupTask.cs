using System.Text.Json;
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

        [Column("start_at")]
        public DateTime StartAt { get; set; }

        [Column("completed_at")]
        public DateTime? CompletedAt { get; set; }

        [Column("is_enabled")]
        public bool IsEnabled { get; set; }

        [Column("is_deleted")]
        public bool IsDeleted { get; set; }

        [Column("status")]
        public BackupTaskStatus Status { get; set; }

        [Column("provider")]
        public string Provider { get; set; } = string.Empty;

        [Column("parameters_json")]
        public string ParametersJson { get; set; } = string.Empty;

        [Column("is_notification_enabled")]
        public bool IsNotificationEnabled { get; set; }

        [Column("user_id")]
        public int UserId { get; set; }

        [Column("last_error")]
        public string? LastError { get; set; }

        public virtual User User { get; set; } = null!;

        public Dictionary<string, string> GetParameters()
        {
            if (string.IsNullOrEmpty(ParametersJson))
            {
                return [];
            }
            return JsonSerializer.Deserialize<Dictionary<string, string>>(ParametersJson) ?? [];
        }

        public void SetParameters(Dictionary<string, string> parameters)
        {
            ParametersJson = JsonSerializer.Serialize(parameters);
        }
    }
}
