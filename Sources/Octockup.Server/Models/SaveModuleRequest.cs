namespace Octockup.Server.Models
{
    public class SaveModuleRequest
    {
        public string Tag { get; set; } = string.Empty;
        public Dictionary<string, string> Parameters { get; set; } = [];
    }
}
