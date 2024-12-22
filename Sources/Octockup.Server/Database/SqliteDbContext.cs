using Microsoft.EntityFrameworkCore;

namespace Octockup.Server.Database
{
    public class SqliteDbContext(DbContextOptions<SqliteDbContext> options) : AppDbContext(options)
    {

    }
}