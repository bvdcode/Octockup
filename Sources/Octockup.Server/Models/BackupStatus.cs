using Octockup.Server.Models.Enums;

namespace Octockup.Server.Models
{
    public record BackupStatus(string JobName, DateTime LastRun, TimeSpan Duration, BackupStatusType Status);
}
