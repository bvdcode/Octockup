using Microsoft.EntityFrameworkCore;

namespace Octockup.Server.Database
{
    public class PostgresDbContext(DbContextOptions<PostgresDbContext> options) : AppDbContext(options)
    {

    }
}
