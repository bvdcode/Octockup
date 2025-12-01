using EasyExtensions.Models.Dto;

namespace Octockup.Server.Models.Dto
{
    public class SnapshotDto : BaseDto<Guid>
    {
        public Guid BackupId { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
}
