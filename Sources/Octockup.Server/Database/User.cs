using Microsoft.EntityFrameworkCore;
using EasyExtensions.EntityFrameworkCore.Abstractions;

namespace Octockup.Server.Database
{
    [Index(nameof(UserName), IsUnique = true)]
    public class User : BaseEntity<Guid>
    {
        public string UserName { get; set; } = string.Empty;
        public string PasswordPhc { get; set; } = string.Empty;
        public ICollection<Module> SavedSources { get; set; } = [];
        public ICollection<Module> SavedStorages { get; set; } = [];
    }
}
