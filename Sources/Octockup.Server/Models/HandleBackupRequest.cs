using MediatR;

namespace Octockup.Server.Models
{
    public class HandleBackupRequest(int backupTaskId) : IRequest
    {
        public int BackupTaskId { get; } = backupTaskId;
    }
}