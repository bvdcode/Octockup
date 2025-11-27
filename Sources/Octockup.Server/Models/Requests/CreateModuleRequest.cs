using Octockup.Server.Models.Enums;

namespace Octockup.Server.Models.Requests
{
    public class CreateModuleRequest
    {
        public ModuleDestination Destination { get; set; }
        public string Tag { get; set; } = string.Empty;
        public string BackupModuleId { get; set; } = string.Empty;
        public Dictionary<string, string> Parameters { get; set; } = [];
    }
}
