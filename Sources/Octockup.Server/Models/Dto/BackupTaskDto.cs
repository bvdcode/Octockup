using Octockup.Server.Models.Enums;

namespace Octockup.Server.Models.Dto
{
    public class BackupTaskDto : BaseDto
    {
        public double Progress { get; set; }
        public DateTime? LastRun { get; set; }
        public TimeSpan Duration { get; set; }
        public BackupTaskStatus Status { get; set; }
        public string JobName { get; set; } = string.Empty;
    }
}