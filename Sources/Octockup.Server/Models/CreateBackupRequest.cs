using MediatR;

namespace Octockup.Server.Models
{
    public class CreateBackupRequest : IRequest
    {
        public string BackupName { get; set; } = string.Empty;

    }
}
