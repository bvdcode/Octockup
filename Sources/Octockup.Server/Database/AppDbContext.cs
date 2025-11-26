using Microsoft.EntityFrameworkCore;
using EasyExtensions.EntityFrameworkCore.Database;

namespace Octockup.Server.Database
{
    public class AppDbContext(DbContextOptions options) : AuditedDbContext(options)
    {
        public DbSet<User> Users => Set<User>();
        public DbSet<Backup> Backups => Set<Backup>();
        public DbSet<Module> Modules => Set<Module>();
    }
}
