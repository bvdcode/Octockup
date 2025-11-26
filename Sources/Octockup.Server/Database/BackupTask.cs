namespace Octockup.Server.Database
{
    public class BackupTask : BaseEntity
    {
        public Guid UserId { get; set; }
        public Guid SourceId { get; set; }
        public Guid StorageId { get; set; }
        public string Tag { get; set; } = string.Empty;
        public ICollection<string> IgnoredPaths { get; set; } = [];
    }
}
