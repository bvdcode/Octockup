using System.ComponentModel.DataAnnotations.Schema;
using EasyExtensions.EntityFrameworkCore.Abstractions;

namespace Octockup.Server.Database
{
    [Table("refresh_tokens")]
    public class RefreshToken : BaseEntity<Guid>
    {
        [Column("user_id")]
        public Guid UserId { get; set; }

        [Column("token")]
        public string Token { get; set; } = null!;

        [Column("revoked_at")]
        public DateTime? RevokedAt { get; set; }

        public virtual User User { get; set; } = null!;
    }
}