using System.ComponentModel.DataAnnotations.Schema;
using EasyExtensions.EntityFrameworkCore.Abstractions;

namespace Octockup.Server.Database
{
    [Table("saved_files")]
    public class SavedFile : BaseEntity
    {
        [Column("backup_snapshot_id")]
        public int BackupSnapshotId { get; set; }

        [Column("file_id")]
        public Guid FileId { get; set; }

        [Column("sha512")]
        public string SHA512 { get; set; } = string.Empty;

        [Column("size")]
        public long Size { get; set; }

        [Column("source_path")]
        public string SourcePath { get; set; } = string.Empty;

        [Column("metadata_created_at")]
        public DateTime MetadataCreatedAt { get; set; }

        [Column("metadata_updated_at")]
        public DateTime MetadataUpdatedAt { get; set; }

        public virtual BackupSnapshot BackupSnapshot { get; set; } = null!;

        public string GetName()
        {
            int slashIndex = SourcePath.IndexOf('/');
            int backslashIndex = SourcePath.IndexOf('\\');
            int separatorIndex = Math.Min(slashIndex, backslashIndex);
            if (separatorIndex == -1)
            {
                return SourcePath;
            }
            char separator = SourcePath[separatorIndex];
            int startIndex = SourcePath.LastIndexOf(separator) + 1;
            string result = SourcePath[startIndex..];
            if (string.IsNullOrWhiteSpace(result))
            {
                string name = Path.GetFileName(SourcePath);
                return string.IsNullOrWhiteSpace(name) ? SourcePath : name;
            }
            return result;
        }
    }
}
