namespace Octockup.Server.Models.Requests
{
    public class CreateModuleRequest
    {
        public string Tag { get; set; } = string.Empty;
        public string BackupModuleId { get; set; } = string.Empty;
        public Dictionary<string, string> Parameters { get; set; } = [];
    }
}
