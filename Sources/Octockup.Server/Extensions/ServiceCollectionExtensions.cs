using Npgsql;
using Microsoft.Data.Sqlite;
using EasyExtensions.Helpers;
using Octockup.Server.Helpers;
using Octockup.Server.Database;
using Microsoft.EntityFrameworkCore;
using Octockup.Server.Providers.Storage;
using EasyExtensions.AspNetCore.Extensions;

namespace Octockup.Server.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddStorageProviders(this IServiceCollection services)
        {
            var types = ReflectionHelpers.GetTypesOfInterface<IStorageProvider>();
            foreach (var type in types)
            {
                services.AddScoped(typeof(IStorageProvider), type);
            }
            return services;
        }

        public static IServiceCollection AddDbContext<TContext>(this IServiceCollection services, IConfiguration configuration)
            where TContext : DbContext
        {
            DatabaseSettings? postgresSettings = configuration.GetPostgresSettings();
            if (postgresSettings != null)
            {
                NpgsqlConnectionStringBuilder npgBuilder = new()
                {
                    Host = postgresSettings.Host,
                    Port = postgresSettings.Port,
                    Database = postgresSettings.Database,
                    Username = postgresSettings.Username,
                    Password = postgresSettings.Password,
                };
                services.AddDbContext<AppDbContext, PostgresDbContext>(x => x
                    .UseNpgsql(npgBuilder.ConnectionString)
                    .UseLazyLoadingProxies(), contextLifetime: ServiceLifetime.Transient);
                return services;
            }
            else
            {
                string filename = FileSystemHelpers.GetFilePath("octockup.sqlite");
                SqliteConnectionStringBuilder sqliteBuilder = new()
                {
                    DataSource = filename,
                };
                services.AddDbContext<AppDbContext, SqliteDbContext>(x => x
                    .UseSqlite(sqliteBuilder.ConnectionString)
                    .UseLazyLoadingProxies());
                return services;
            }
        }

        public static IServiceCollection SetupJwtKey(this IServiceCollection services, IConfiguration configuration)
        {
            const string filename = "jwt.key";
            const int keySize = 64;
            if (!File.Exists(filename))
            {
                string newKey = StringHelpers.CreateRandomString(keySize);
                File.WriteAllText(filename, newKey);
            }
            string key = File.ReadAllText(filename);
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new Exception("JWT key is empty");
            }
            if (key.Length != keySize)
            {
                throw new ArgumentOutOfRangeException($"JWT key is invalid: must be {keySize} characters long, actual length is {key.Length}");
            }
            configuration["JwtSettings:Key"] = key;
            return services;
        }

        public static IServiceCollection SetupCors(this IServiceCollection services, IConfiguration configuration)
        {
            string? corsOrigins = Environment.GetEnvironmentVariable("CORS_ORIGINS");
            List<string> origins = [];
            if (!string.IsNullOrWhiteSpace(corsOrigins))
            {
                var splitted = corsOrigins.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                origins.AddRange(splitted);
            }
            var fromConfig = configuration.GetSection("CorsOrigins").Get<string[]>();
            if (fromConfig != null)
            {
                origins.AddRange(fromConfig);
            }
            if (origins.Count == 0)
            {
                return services;
            }
            return services.AddDefaultCorsWithOrigins([.. origins]);
        }
    }
}
