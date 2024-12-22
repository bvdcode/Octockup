using Microsoft.EntityFrameworkCore;
using EasyExtensions.EntityFrameworkCore.Database;

namespace Octockup.Server.Database
{
    public abstract class AppDbContext(DbContextOptions options) : AuditedDbContext(options)
    {
        public DbSet<User> Users { get; set; } = null!;
        public DbSet<Session> Sessions { get; set; } = null!;
        public DbSet<BackupTask> BackupTasks { get; set; } = null!;
    }
}
