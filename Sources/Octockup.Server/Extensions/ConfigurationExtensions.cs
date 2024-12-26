using Octockup.Server.Database;

namespace Octockup.Server.Extensions
{
    public static class ConfigurationExtensions
    {
        public static DatabaseSettings GetDatabaseSettings(this IConfiguration configuration)
        {
            bool isPostgresSettingsExist = configuration
                .GetSection("Postgres")
                .Exists();
            string server = "";
        }
    }
}
