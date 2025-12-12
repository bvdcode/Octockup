using EasyExtensions.Abstractions;
using EasyExtensions.Crypto;
using Microsoft.EntityFrameworkCore;
using Octockup.Server.Database;

namespace Octockup.Server.Extensions
{
    public static class WebApplicationBuilderExtensions
    {
        public static WebApplicationBuilder SetupDatabaseAndKeys(this WebApplicationBuilder builder)
        {
            string masterKey = builder.Configuration.GetMasterKey();
            builder.Configuration.AddInMemoryCollection(
            [
                new KeyValuePair<string, string?>("Pepper", KeyDerivation.DeriveSubkeyBase64(masterKey, "pepper", 32))
            ]);
            byte[] cryptoKey = KeyDerivation.DeriveSubkey(masterKey, "crypto", 32);
            builder.Services.AddScoped<IStreamCipher>(sp => new AesGcmStreamCipher(cryptoKey));
            bool hasPostgresSettings = InjectPostgresSettings(builder.Configuration);
            if (!hasPostgresSettings)
            {
                string sqlitePassword = KeyDerivation.DeriveSubkeyBase64(masterKey, "sqlite", 32);
                string sqlitePath = Helpers.PathHelpers.GetPath("octockup.sqlite");
                builder.Services.AddDbContext<AppDbContext, SqliteDbContext>(x => x.UseSqlite(connectionString: $"Data Source={sqlitePath};Password={sqlitePassword};"));
                return builder;
            }

            return builder;
        }

        private static bool InjectPostgresSettings(ConfigurationManager configuration)
        {
            string host = Environment.GetEnvironmentVariable("OCTOCKUP_PG_HOST") ?? "postgres-server";
            string port = Environment.GetEnvironmentVariable("OCTOCKUP_PG_PORT") ?? "5432";
            string database = Environment.GetEnvironmentVariable("OCTOCKUP_PG_DATABASE") ?? "octockup";
            string username = Environment.GetEnvironmentVariable("OCTOCKUP_PG_USERNAME") ?? "octockup_client";
            string? password = Environment.GetEnvironmentVariable("OCTOCKUP_PG_PASSWORD");
            if (string.IsNullOrEmpty(password))
            {
                return false;
            }
            configuration.AddInMemoryCollection(
            [
                new KeyValuePair<string, string?>("DatabaseSettings:Host", host),
                new KeyValuePair<string, string?>("DatabaseSettings:Port", port),
                new KeyValuePair<string, string?>("DatabaseSettings:Database", database),
                new KeyValuePair<string, string?>("DatabaseSettings:Username", username),
                new KeyValuePair<string, string?>("DatabaseSettings:Password", password)
            ]);
            return true;
        }
    }
}
