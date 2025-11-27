using EasyExtensions.Models.Dto;
using Octockup.Server.Models.Enums;

namespace Octockup.Server.Models.Dto
{
    public class ScheduleDto : BaseDto<Guid>
    {
        public Guid BackupId { get; set; }
        public DateTime? FinishedAt { get; set; }
        public ScheduleStatus Status { get; set; }
        public DateTime StartAt { get; set; }
        public TimeSpan? Interval { get; set; }
        public string? ErrorMessage { get; set; }
        public BackupDto Backup { get; set; } = null!;
    }
}
