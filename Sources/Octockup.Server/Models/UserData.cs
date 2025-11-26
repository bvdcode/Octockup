namespace Octockup.Server.Models
{
    public class UserData : BaseEntity
    {
        public string Username { get; set; } = string.Empty;
        public string PasswordPhc { get; set; } = string.Empty;
        public ICollection<UserBackupSource> BackupSources { get; set; } = [];
    }
}
