namespace Octockup.Server.Database
{
    public class SavedBackupModule : BaseEntity
    {
        public Guid UserId { get; set; }
        public string Tag { get; set; } = string.Empty;
        public string BackupModuleId { get; set; } = string.Empty;
        public Dictionary<string, string> Parameters { get; set; } = [];
    }
}
