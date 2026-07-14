// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using EasyExtensions.Abstractions;
using EasyExtensions.Crypto;
using EasyExtensions.EntityFrameworkCore.Npgsql.Extensions;
using Microsoft.EntityFrameworkCore;
using Octockup.Server.Database;

namespace Octockup.Server.Extensions
{
    public static class WebApplicationBuilderExtensions
    {
        private const string PostgresEnvPrefix = "OCTOCKUP_POSTGRES_";

        public static WebApplicationBuilder SetupDatabaseAndKeys(this WebApplicationBuilder builder)
        {
            string masterKey = builder.Configuration.GetMasterKey();
            builder.Configuration.AddInMemoryCollection(
            [
                new KeyValuePair<string, string?>("Pepper", KeyDerivation.DeriveSubkeyBase64(masterKey, "pepper", 32))
            ]);
            byte[] cryptoKey = KeyDerivation.DeriveSubkey(masterKey, "crypto", 32);
            builder.Services.AddScoped<IStreamCipher>(_ => new AesGcmStreamCipher(cryptoKey));
            InjectPostgresSettings(builder.Configuration);
            builder.Services.AddPostgresDbContext<AppDbContext, PostgresDbContext>(
                options => options.UseLazyLoadingProxies = false);
            return builder;
        }

        private static void InjectPostgresSettings(ConfigurationManager configuration)
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

            if (string.IsNullOrWhiteSpace(configuration["DatabaseSettings:Password"]))
            {
                throw new InvalidOperationException(
                    $"PostgreSQL password is required. Set {PostgresEnvPrefix}PASSWORD.");
            }
        }
    }
}
