namespace Octockup.Server.Models
{
    public class UserBackupSource
    {
        public DateTime CreatedAt { get; set; }
        public string Tag { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string BackupSourceId { get; set; } = string.Empty;
        public Dictionary<string, string> Parameters { get; set; } = [];
    }
}