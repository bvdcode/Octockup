using System.ComponentModel.DataAnnotations.Schema;
using EasyExtensions.EntityFrameworkCore.Abstractions;

namespace Octockup.Server.Database
{
    [Table("sessions")]
    public class Session : BaseEntity
    {
        [Column("user_id")]
        public int UserId { get; set; }

        [Column("refresh_token")]
        public string RefreshToken { get; set; } = null!;

        public virtual User User { get; set; } = null!;
    }
}