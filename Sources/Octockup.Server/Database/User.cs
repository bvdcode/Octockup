using System.ComponentModel.DataAnnotations.Schema;
using EasyExtensions.EntityFrameworkCore.Abstractions;

namespace Octockup.Server.Database
{
    [Table("users")]
    public class User : BaseEntity<Guid>
    {
        [Column("username")]
        public string Username { get; set; } = null!;

        [Column("password_phc")]
        public string PasswordPhc { get; set; } = null!;

        public virtual ICollection<RefreshToken> Sessions { get; set; } = [];
    }
}