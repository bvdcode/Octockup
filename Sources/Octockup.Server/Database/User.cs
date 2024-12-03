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

        [Column("password_hash")]
        public string PasswordHash { get; set; } = null!;
    }
}