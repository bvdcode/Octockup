using Microsoft.EntityFrameworkCore;

namespace Octockup.Server.Database
{
    public class PostgresDbContext(DbContextOptions options) : AppDbContext(options)
    {

    }
}
