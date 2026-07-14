// SPDX-License-Identifier: MIT
// Copyright (c) 2025 Vadim Belov <https://belov.us>

using Microsoft.EntityFrameworkCore;
using Npgsql;
using Octockup.Server.Database;

namespace Octockup.Tests
{
    internal class PostgresTestDatabase : IAsyncDisposable
    {
        private const string DatabasePrefix = "octockup_test_";
        private readonly string _adminConnectionString;
        private bool _disposed;

        private PostgresTestDatabase(string databaseName, string adminConnectionString, string connectionString)
        {
            DatabaseName = databaseName;
            _adminConnectionString = adminConnectionString;
            ConnectionString = connectionString;
        }

        public string DatabaseName { get; }
        public string ConnectionString { get; }

        public static async Task<PostgresTestDatabase> CreateAsync(CancellationToken cancellationToken = default)
        {
            string databaseName = DatabasePrefix + Guid.NewGuid().ToString("N");
            NpgsqlConnectionStringBuilder adminBuilder = CreateConnectionStringBuilder("postgres", pooling: false);
            NpgsqlConnectionStringBuilder databaseBuilder = CreateConnectionStringBuilder(databaseName, pooling: true);
            PostgresTestDatabase database = new(
                databaseName,
                adminBuilder.ConnectionString,
                databaseBuilder.ConnectionString);

            bool created = false;
            try
            {
                await database.CreateDatabaseAsync(cancellationToken);
                created = true;
                DbContextOptions<PostgresDbContext> options = new DbContextOptionsBuilder<PostgresDbContext>()
                    .UseNpgsql(database.ConnectionString)
                    .Options;
                await using PostgresDbContext context = new(options);
                await context.Database.MigrateAsync(cancellationToken);
                return database;
            }
            catch
            {
                if (created)
                {
                    await database.DisposeAsync();
                }
                throw;
            }
        }

        private static NpgsqlConnectionStringBuilder CreateConnectionStringBuilder(string databaseName, bool pooling)
        {
            string portText = Environment.GetEnvironmentVariable("OCTOCKUP_TEST_POSTGRES_PORT") ?? "5432";
            if (!int.TryParse(portText, out int port))
            {
                throw new InvalidOperationException($"Invalid PostgreSQL test port: {portText}.");
            }

            return new NpgsqlConnectionStringBuilder
            {
                Host = Environment.GetEnvironmentVariable("OCTOCKUP_TEST_POSTGRES_HOST") ?? "localhost",
                Port = port,
                Username = Environment.GetEnvironmentVariable("OCTOCKUP_TEST_POSTGRES_USERNAME") ?? "postgres",
                Password = Environment.GetEnvironmentVariable("OCTOCKUP_TEST_POSTGRES_PASSWORD") ?? "postgres",
                Database = databaseName,
                Pooling = pooling,
                Timeout = 10,
                CommandTimeout = 30,
                IncludeErrorDetail = true,
            };
        }

        private async Task CreateDatabaseAsync(CancellationToken cancellationToken)
        {
            await using NpgsqlConnection connection = new(_adminConnectionString);
            await connection.OpenAsync(cancellationToken);
            await using NpgsqlCommand command = new($"CREATE DATABASE {DatabaseName}", connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            if (!DatabaseName.StartsWith(DatabasePrefix, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Refusing to drop unexpected database: {DatabaseName}.");
            }

            NpgsqlConnection.ClearAllPools();
            await using NpgsqlConnection connection = new(_adminConnectionString);
            await connection.OpenAsync();
            await using NpgsqlCommand command = new($"DROP DATABASE {DatabaseName} WITH (FORCE)", connection);
            await command.ExecuteNonQueryAsync();
            _disposed = true;
        }
    }
}
