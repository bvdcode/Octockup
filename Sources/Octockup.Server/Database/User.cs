using Microsoft.EntityFrameworkCore;
using EasyExtensions.EntityFrameworkCore.Abstractions;

namespace Octockup.Server.Database
{
    /// <summary>
    /// Represents an application user with authentication credentials and associated modules.
    /// </summary>
    [Index(nameof(UsernameRename), IsUnique = true)]
    public class User : BaseEntity<Guid>
    {
        public string UsernameRename { get; set; } = string.Empty;
        public string PasswordPhc { get; set; } = string.Empty;
        public ICollection<Module> Modules { get; set; } = [];
    }
}
