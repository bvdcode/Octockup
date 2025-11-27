using EasyExtensions.EntityFrameworkCore.Abstractions;

namespace Octockup.Server.Database
{
    public class RefreshToken : BaseEntity<Guid>
    {
        public Guid UserId { get; set; }
        public string Token { get; set; } = string.Empty;
        public DateTime? RevokedAt { get; set; }
        public virtual User User { get; set; } = null!;
    }
}
