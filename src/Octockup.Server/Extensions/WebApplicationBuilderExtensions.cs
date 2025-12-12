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
            return false;
        }
    }
}
