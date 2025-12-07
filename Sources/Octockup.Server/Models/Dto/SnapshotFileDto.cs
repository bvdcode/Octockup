using EasyExtensions.Models.Dto;

namespace Octockup.Server.Models.Dto
{
    public class SnapshotFileDto : BaseDto<Guid>
    {
        public long Size { get; set; }
        public Guid SnapshotId { get; set; }
        public DateTime? LastModified { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string Hashsum { get; set; } = string.Empty;
    }
}
