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
        public static IServiceCollection AddStorageProvider<TStorageProvider>(this IServiceCollection services) where TStorageProvider : class, IStorageProvider
        {
            return services.AddSingleton<IStorageProvider, TStorageProvider>();
        }

        public static IServiceCollection AddDbContext<TContext>(this IServiceCollection services, IConfiguration configuration)
            where TContext : DbContext
        {
            DatabaseSettings? postgresSettings = GetPostgresSettings(configuration);
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
                services.AddDbContext<AppDbContext, PostgresDbContext>(x => x.UseNpgsql(npgBuilder.ConnectionString).UseLazyLoadingProxies());
                return services;
            }
            else
            {
                string filename = FileSystemHelpers.GetFilePath("octockup.sqlite");
                SqliteConnectionStringBuilder sqliteBuilder = new()
                {
                    DataSource = filename,
                };
                services.AddDbContext<AppDbContext, SqliteDbContext>(x => x.UseSqlite(sqliteBuilder.ConnectionString).UseLazyLoadingProxies());
                return services;
            }
        }

        private static DatabaseSettings? GetPostgresSettings(IConfiguration configuration)
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

        public static IServiceCollection SetupJwtKey(this IServiceCollection services, IConfiguration configuration)
        {
            string filename = FileSystemHelpers.GetFilePath("jwt.key");
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

        public static IServiceCollection SetupCors(this IServiceCollection services)
        {
            string? corsOrigins = Environment.GetEnvironmentVariable("CORS_ORIGINS");
            if (string.IsNullOrWhiteSpace(corsOrigins))
            {
                services.AddCors(x =>
                {
                    x.AddDefaultPolicy(y =>
                    {
                        y.AllowAnyOrigin();
                        y.AllowAnyMethod();
                        y.AllowAnyHeader();
                        y.WithExposedHeaders("*");
                    });
                });
                return services;
            }
            return services.AddDefaultCorsWithOrigins(corsOrigins);
        }
    }
}
