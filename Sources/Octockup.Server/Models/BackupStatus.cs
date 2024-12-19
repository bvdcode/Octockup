using Octockup.Server.Models.Enums;

namespace Octockup.Server.Models
{
    public record BackupStatus(int Id, string JobName, DateTime LastRun, TimeSpan Duration, BackupStatusType Status, double Progress);
}
