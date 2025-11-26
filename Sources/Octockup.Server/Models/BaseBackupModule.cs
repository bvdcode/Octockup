namespace Octockup.Server.Models
{
    public abstract class BaseBackupModule : BaseEntity
    {
        public string Tag { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public Dictionary<string, string> Parameters { get; set; } = [];
    }
}
