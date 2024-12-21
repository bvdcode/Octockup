using MediatR;

namespace Octockup.Server.Models
{
    public class CreateBackupRequest : IRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Provider { get; set; } = string.Empty;
        public int Interval { get; set; }
        public DateTime? StartAt { get; set; }
        public bool IsNotificationEnabled { get; set; }
        public Dictionary<string, string> Parameters { get; set; } = [];
    }
}
