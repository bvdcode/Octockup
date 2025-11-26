namespace Octockup.Server.Models
{
    public class SavedBackupModule : BaseEntity
    {
        public string Tag { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string BackupModuleId { get; set; } = string.Empty;
        public Dictionary<string, string> Parameters { get; set; } = [];
    }
}
