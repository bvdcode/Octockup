using EasyExtensions.AspNetCore.Extensions;
using EasyExtensions.Helpers;
using Microsoft.EntityFrameworkCore;
using Octockup.Server.Helpers;

namespace Octockup.Server.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddDbContext<TContext>(this IServiceCollection services, IConfiguration configuration)
            where TContext : DbContext
        {
            string databaseFile = configuration["DatabaseFile"]
                ?? throw new ArgumentNullException(nameof(configuration), "DatabaseFile is not set in configuration");
            string filename = FileSystemHelpers.GetFilePath(databaseFile);
            return services.AddDbContext<TContext>(options => options.UseSqlite("Data Source=" + databaseFile));
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
