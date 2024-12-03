using Microsoft.EntityFrameworkCore;
using EasyExtensions.EntityFrameworkCore.Database;

namespace Octockup.Server.Database
{
    public class AppDbContext(DbContextOptions options) : AuditedDbContext(options)
    {
        public DbSet<User> Users { get; set; } = null!;
    }
}
