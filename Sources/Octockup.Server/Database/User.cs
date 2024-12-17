using Octockup.Server.Database.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace Octockup.Server.Database
{
    [Table("users")]
    public record User
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("username")]
        public string Username { get; set; } = null!;

        [Column("email")]
        public string Email { get; set; } = null!;

        [Column("role")]
        public UserRole Role { get; set; }

        [Column("password_hash_sha512")]
        public string PasswordHash { get; set; } = null!;

        public virtual ICollection<Session> Sessions { get; set; } = [];
    }
}