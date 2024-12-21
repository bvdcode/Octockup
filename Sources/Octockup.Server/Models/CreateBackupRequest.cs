using MediatR;

namespace Octockup.Server.Models
{
    public class CreateBackupRequest : IRequest
    {
        public int Interval { get; set; }
        public DateTime StartAt { get; set; }
        public string Provider { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }
}
