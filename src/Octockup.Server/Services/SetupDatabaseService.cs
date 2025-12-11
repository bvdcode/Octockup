using EasyExtensions.Crypto;
using EasyExtensions.EntityFrameworkCore.Extensions;
using EasyExtensions.EntityFrameworkCore.Npgsql.Extensions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Octockup.Server.Database;
using Octockup.Server.Extensions;
using System.Diagnostics;

namespace Octockup.Server.Services
{
    public class SetupDatabaseService(
        ILogger _logger,
        ConfigurationManager _configuration,
        IServiceCollection _serviceCollection)
    {
        private const string SqliteFileName = "octockup.sqlite";

        public void Setup()
        {
            string sqliteConnectionString = BuildSqliteConnectionString();
            string? postgresPassword = Environment.GetEnvironmentVariable("OCTOCKUP_POSTGRES_PASSWORD");
            string postgresHost = Environment.GetEnvironmentVariable("OCTOCKUP_POSTGRES_HOST") ?? "postgres";
            string postgresPort = Environment.GetEnvironmentVariable("OCTOCKUP_POSTGRES_PORT") ?? "5432";
            string postgresDatabase = Environment.GetEnvironmentVariable("OCTOCKUP_POSTGRES_DATABASE") ?? "octockup";
            string postgresUser = Environment.GetEnvironmentVariable("OCTOCKUP_POSTGRES_USER") ?? "octockup";

            if (string.IsNullOrWhiteSpace(postgresPassword))
            {
                _serviceCollection.AddSqlite<AppDbContext>(sqliteConnectionString);
                _logger.LogInformation("No PostgreSQL password provided. Using SQLite database: {file}",
                    Helpers.PathHelpers.GetPath(SqliteFileName));
                DbContextOptionsBuilder<SqliteDbContext> optionsBuilder = new DbContextOptionsBuilder<SqliteDbContext>()
                    .UseSqlite(sqliteConnectionString);
                SqliteDbContext sqliteContext = new(optionsBuilder.Options);
                try
                {
                    bool created = sqliteContext.Database.EnsureCreated();
                    if (created)
                    {
                        _logger.LogInformation("SQLite database created.");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create or connect to SQLite database: " +
                        "if schema is outdated, consider deleting the file {file} to recreate it. Migrations are supported only for PostgreSQL.",
                        Helpers.PathHelpers.GetPath(SqliteFileName));
                    throw;
                }
                return;
            }

            _configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "PostgresHost", postgresHost  },
                { "PostgresPort", postgresPort },
                { "PostgresDatabase", postgresDatabase },
                { "PostgresUser", postgresUser },
                { "PostgresPassword", postgresPassword }
            });

            _logger.LogInformation("Using PostgreSQL database at {host}:{port}/{database} with user {user}",
                postgresHost, postgresPort, postgresDatabase, postgresUser);
            _serviceCollection.AddPostgresDbContext<AppDbContext>();

            NpgsqlConnectionStringBuilder builder = new()
            {
                Host = postgresHost,
                Username = postgresUser,
                Password = postgresPassword,
                Database = postgresDatabase,
                Port = ushort.Parse(postgresPort),
            };
            using AppDbContext pgContext = new(new DbContextOptionsBuilder<AppDbContext>()
                .UseNpgsql(builder.ConnectionString).Options);
            pgContext.ApplyMigrations(_logger);

            bool fileExists = File.Exists(Helpers.PathHelpers.GetPath(SqliteFileName));
            if (fileExists)
            {
                using SqliteDbContext sqliteContext = new(new DbContextOptionsBuilder<SqliteDbContext>()
                    .UseSqlite(sqliteConnectionString).Options);
                bool hasUsers = pgContext.Users.Any();
                if (hasUsers)
                {
                    _logger.LogInformation("PostgreSQL database already has data. Skipping migration.");
                    return;
                }
                bool isSqliteEmpty = !sqliteContext.Users.Any();
                if (isSqliteEmpty)
                {
                    _logger.LogInformation("SQLite database is empty. No data to migrate.");
                    return;
                }
                _logger.LogInformation("Migrating data from SQLite to PostgreSQL...");
                MigrateFromSqlite(sqliteContext, pgContext);
            }
        }

        private string BuildSqliteConnectionString()
        {
            string masterKey = _configuration.GetMasterKey();
            string sqlitePassword = KeyDerivation.DeriveSubkeyBase64(masterKey, "sqlite", 32);
            string sqlitePath = Helpers.PathHelpers.GetPath(SqliteFileName);
            return $"Data Source={sqlitePath};Password={sqlitePassword};";
        }

        private void MigrateFromSqlite(AppDbContext sqliteContext, AppDbContext pgContext)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();

            _logger.LogInformation("Migrating Users...");
            var users = sqliteContext.Users.AsNoTracking().ToList();
            pgContext.Users.AddRange(users);
            pgContext.SaveChanges();
            _logger.LogInformation("Migrated {count} Users.", users.Count);

            _logger.LogInformation("Migrating Modules...");
            var modules = sqliteContext.Modules.AsNoTracking().ToList();
            pgContext.Modules.AddRange(modules);
            pgContext.SaveChanges();
            _logger.LogInformation("Migrated {count} Modules.", modules.Count);

            _logger.LogInformation("Migrating Backups...");
            var backups = sqliteContext.Backups.AsNoTracking().ToList();
            pgContext.Backups.AddRange(backups);
            pgContext.SaveChanges();
            _logger.LogInformation("Migrated {count} Backups.", backups.Count);

            _logger.LogInformation("Migrating Schedules...");
            var schedules = sqliteContext.Schedules.AsNoTracking().ToList();
            pgContext.Schedules.AddRange(schedules);
            pgContext.SaveChanges();
            _logger.LogInformation("Migrated {count} Schedules.", schedules.Count);

            _logger.LogInformation("Migrating Snapshots...");
            var snapshots = sqliteContext.Snapshots.AsNoTracking().ToList();
            pgContext.Snapshots.AddRange(snapshots);
            pgContext.SaveChanges();
            _logger.LogInformation("Migrated {count} Snapshots.", snapshots.Count);

            _logger.LogInformation("Migrating SnapshotFiles...");
            var snapshotFiles = sqliteContext.SnapshotFiles.AsNoTracking().ToList();
            pgContext.SnapshotFiles.AddRange(snapshotFiles);
            pgContext.SaveChanges();
            _logger.LogInformation("Migrated {count} SnapshotFiles.", snapshotFiles.Count);

            _logger.LogInformation("Migrating UploadedHashes...");
            var uploadedHashes = sqliteContext.UploadedHashes.AsNoTracking().ToList();
            pgContext.UploadedHashes.AddRange(uploadedHashes);
            pgContext.SaveChanges();
            _logger.LogInformation("Migrated {count} UploadedHashes.", uploadedHashes.Count);

            _logger.LogInformation("Data migration completed in {elapsed:hh\\:mm\\:ss}.", stopwatch.Elapsed);
            _logger.LogInformation("You can now delete the SQLite database file {file} if it is no longer needed.",
                Helpers.PathHelpers.GetPath(SqliteFileName));
        }
    }
}
