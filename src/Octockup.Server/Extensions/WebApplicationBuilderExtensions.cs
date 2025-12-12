using EasyExtensions.Abstractions;
using EasyExtensions.Crypto;
using EasyExtensions.EntityFrameworkCore.Npgsql.Extensions;
using Microsoft.EntityFrameworkCore;
using Octockup.Server.Database;

namespace Octockup.Server.Extensions
{
    public static class WebApplicationBuilderExtensions
    {
        private const string PostgresEnvPrefix = "OCTOCKUP_POSTGRES2_";

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
            if (hasPostgresSettings)
            {
                builder.Services.AddPostgresDbContext<AppDbContext, PostgresDbContext>();
            }
            else
            {
                string sqlitePassword = KeyDerivation.DeriveSubkeyBase64(masterKey, "sqlite", 32);
                string sqlitePath = Helpers.PathHelpers.GetPath("octockup.sqlite");
                builder.Services.AddDbContext<AppDbContext, SqliteDbContext>(
                    x => x.UseSqlite(connectionString: $"Data Source={sqlitePath};Password={sqlitePassword};"));
            }
            return builder;
        }

        private static bool InjectPostgresSettings(ConfigurationManager configuration)
        {
            string? host = Environment.GetEnvironmentVariable(PostgresEnvPrefix + "HOST");
            string? port = Environment.GetEnvironmentVariable(PostgresEnvPrefix + "PORT");
            string? database = Environment.GetEnvironmentVariable(PostgresEnvPrefix + "DATABASE");
            string? username = Environment.GetEnvironmentVariable(PostgresEnvPrefix + "USERNAME");
            string? password = Environment.GetEnvironmentVariable(PostgresEnvPrefix + "PASSWORD");
            Dictionary<string, string?> envVars = [];
            if (!string.IsNullOrEmpty(host))
            {
                envVars["DatabaseSettings:Host"] = host;
            }
            if (!string.IsNullOrEmpty(port))
            {
                envVars["DatabaseSettings:Port"] = port;
            }
            if (!string.IsNullOrEmpty(database))
            {
                envVars["DatabaseSettings:Database"] = database;
            }
            if (!string.IsNullOrEmpty(username))
            {
                envVars["DatabaseSettings:Username"] = username;
            }
            if (!string.IsNullOrEmpty(password))
            {
                envVars["DatabaseSettings:Password"] = password;
            }
            configuration.AddInMemoryCollection(envVars);
            return !string.IsNullOrEmpty(configuration["DatabaseSettings:Password"]);
        }
    }
}
