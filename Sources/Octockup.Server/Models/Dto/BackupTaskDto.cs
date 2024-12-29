using Octockup.Server.Models.Enums;

namespace Octockup.Server.Models.Dto
{
    public class BackupTaskDto : BaseDto
    {
        public double Progress { get; set; }
        public TimeSpan Elapsed { get; set; }
        public TimeSpan Interval { get; set; }
        public string? LastMessage { get; set; }
        public DateTime? CompletedAt { get; set; }
        public BackupTaskStatus Status { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}