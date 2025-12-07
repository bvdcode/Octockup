using EasyExtensions.EntityFrameworkCore.Abstractions;

namespace Octockup.Server.Database
{
    public class UploadedHash : BaseEntity<Guid>
    {
        public Guid ModuleId { get; set; }
        public string Hash { get; set; } = string.Empty;
        public virtual Module Module { get; set; } = null!;
    }
}
