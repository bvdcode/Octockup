using Octockup.Server.Models.Enums;

namespace Octockup.Server.Models
{
    public class BackupStatus
    {
        public int Id { get; set; }
        public double Progress { get; set; }
        public DateTime LastRun { get; set; }
        public TimeSpan Duration { get; set; }
        public BackupStatusType Status { get; set; }
        public string JobName { get; set; } = string.Empty;

        internal static BackupStatus Create(int id, string jobName, DateTime lastRun,
            TimeSpan duration, BackupStatusType status, double progress)
        {
            return new BackupStatus
            {
                Id = id,
                Status = status,
                JobName = jobName,
                LastRun = lastRun,
                Duration = duration,
                Progress = progress
            };
        }
    }
}