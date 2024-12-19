using Octockup.Server.Database.Enums;
using System.ComponentModel.DataAnnotations.Schema;
using EasyExtensions.EntityFrameworkCore.Abstractions;

namespace Octockup.Server.Database
{
    [Table("users")]
    public class User : BaseEntity
    {
        [Column("username")]
        public string Username { get; set; } = null!;

        [Column("email")]
        public string Email { get; set; } = null!;

        [Column("role")]
        public UserRole Role { get; set; }

        [Column("password_hash_sha512")]
        public string PasswordHash { get; set; } = null!;

        [Column("storage_limit_bytes")]
        public long? StorageLimitBytes { get; set; }

        public virtual ICollection<Session> Sessions { get; set; } = [];

        public override string ToString()
        {
            return $"#{Id} {Username} ({Email})";
        }
    }
}