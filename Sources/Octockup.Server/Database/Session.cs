using System.ComponentModel.DataAnnotations.Schema;

namespace Octockup.Server.Database
{
    [Table("sessions")]
    public record Session
    {
        [Column("id")]
        public int Id { get; set; }

        [Column("user_id")]
        public int UserId { get; set; }

        [Column("refresh_token")]
        public string RefreshToken { get; set; } = null!;

        public virtual User User { get; set; } = null!;
    }
}