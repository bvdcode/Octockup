using Microsoft.EntityFrameworkCore;

namespace Octockup.Server.Database
{
    /// Add-Migration Initial -Context PostgresDbContext -Output Migrations/Postgres
    public class PostgresDbContext(DbContextOptions<PostgresDbContext> options) : AppDbContext(options) { }
}
