using Microsoft.EntityFrameworkCore;
using EasyExtensions.EntityFrameworkCore.Abstractions;
using Octockup.Server.Models.Enums;

namespace Octockup.Server.Database
{
    [Index(nameof(Tag), IsUnique = true)]
    public class Module : BaseEntity<Guid>
    {
        public ModuleType Type { get; set; }
        public Guid UserId { get; set; }
        public string Tag { get; set; } = string.Empty;
        public string BackupModuleId { get; set; } = string.Empty;
        public Dictionary<string, string> Parameters { get; set; } = [];

        public virtual User User { get; set; } = null!;
    }
}
