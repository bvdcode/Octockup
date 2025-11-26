namespace Octockup.Server.Database
{
    public class User : BaseEntity
    {
        public string Username { get; set; } = string.Empty;
        public string PasswordPhc { get; set; } = string.Empty;
        public ICollection<SavedBackupModule> SavedSources { get; set; } = [];
        public ICollection<SavedBackupModule> SavedStorages { get; set; } = [];
    }
}
