using Octockup.Server.Database;

namespace Octockup.Server.Extensions
{
    public static class ConfigurationExtensions
    {
        public static DatabaseSettings? GetPostgresSettings(this IConfiguration configuration)
        {
            if (configuration.GetSection("Postgres").Exists())
            {
                return configuration.GetSection("Postgres").Get<DatabaseSettings>();
            }
            if (Environment.GetEnvironmentVariable("POSTGRES_HOST") != null)
            {
                string host = Environment.GetEnvironmentVariable("POSTGRES_HOST")
                    ?? throw new ArgumentNullException(nameof(configuration), "POSTGRES_HOST");
                int port = int.Parse(Environment.GetEnvironmentVariable("POSTGRES_PORT")
                    ?? throw new ArgumentNullException(nameof(configuration), "POSTGRES_PORT"));
                string database = Environment.GetEnvironmentVariable("POSTGRES_DATABASE")
                    ?? throw new ArgumentNullException(nameof(configuration), "POSTGRES_DATABASE");
                string username = Environment.GetEnvironmentVariable("POSTGRES_USERNAME")
                    ?? throw new ArgumentNullException(nameof(configuration), "POSTGRES_USERNAME");
                string password = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD")
                    ?? throw new ArgumentNullException(nameof(configuration), "POSTGRES_PASSWORD");
                return new DatabaseSettings
                {
                    Host = host,
                    Port = port,
                    Database = database,
                    Username = username,
                    Password = password,
                };
            }
            return null;
        }
    }
}
