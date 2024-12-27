using EasyExtensions.Helpers;
using Octockup.Server.Database;
using EasyExtensions.Extensions;
using Microsoft.EntityFrameworkCore;
using Octockup.Server.Database.Enums;

namespace Octockup.Server.Services
{
    public class InitializeDatabaseService(IServiceScopeFactory _scopeFactory) : IHostedService
    {
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await CheckDefaultUserAsync(cancellationToken);
            await SetupConcurrencyAsync(cancellationToken);
        }

        private async Task SetupConcurrencyAsync(CancellationToken cancellationToken)
        {
            const string query = "PRAGMA journal_mode=WAL;";
            using var scope = _scopeFactory.CreateScope();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<InitializeDatabaseService>>();
            using var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            string databaseType = context.Database.ProviderName ?? "unknown";
            logger.LogInformation("Database type: {databaseType}", databaseType);
            if (databaseType != "Microsoft.EntityFrameworkCore.Sqlite")
            {
                logger.LogInformation("Database is not SQLite, skipping WAL setup.");
                return;
            }
            try
            {
                await context.Database.ExecuteSqlRawAsync(query, cancellationToken);
                logger.LogInformation("Database concurrency mode set to WAL.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to set database concurrency mode to WAL.");
            }
        }

        private async Task CheckDefaultUserAsync(CancellationToken cancellationToken)
        {
            const string defaultUsername = "admin";

            using var scope = _scopeFactory.CreateScope();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<InitializeDatabaseService>>();
            using var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (context.Users.Any())
            {
                return;
            }

            logger.LogWarning("No users found in the database, seeding the database with the default user.");
            const string defaultPassword = "admin";
            string hash = defaultPassword.SHA512();
            context.Users.Add(new User
            {
                Username = defaultUsername,
                PasswordHash = hash,
                Role = UserRole.Admin,
                Email = defaultUsername + "@octockup.local"
            });
            await context.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Default user '{defaultUsername}' created with password: '{Password}'",
                defaultUsername, defaultPassword);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
