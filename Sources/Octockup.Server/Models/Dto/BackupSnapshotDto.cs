namespace Octockup.Server.Models.Dto
{
    public class BackupSnapshotDto : BaseDto
    {
        public long TotalSize { get; set; }

        public int BackupTaskId { get; set; }

        public string Log { get; set; } = string.Empty;

        public string TotalSizeFormatted { get; set; } = string.Empty;
    }
}
