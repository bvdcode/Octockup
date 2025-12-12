using Microsoft.EntityFrameworkCore;
using Octockup.Server.Converters;

namespace Octockup.Server.Database
{
    // Add-Migration Initial -Context SqliteDbContext -Output Migrations/Sqlite
    public class SqliteDbContext(DbContextOptions<SqliteDbContext> options) : AppDbContext(options)
    {
        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            base.ConfigureConventions(configurationBuilder);

            // Global DateTime converter: always store as UTC in SQLite, always read as UTC
            configurationBuilder.Properties<DateTime>()
                .HaveConversion<UtcDateTimeConverter>();

            configurationBuilder.Properties<DateTime?>()
                .HaveConversion<UtcNullableDateTimeConverter>();
        }
    }
}
