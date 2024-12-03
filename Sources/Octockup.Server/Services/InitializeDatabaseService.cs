using EasyExtensions.Helpers;
using Octockup.Server.Database;
using EasyExtensions.Extensions;
using Octockup.Server.Database.Enums;

namespace Octockup.Server.Services
{
    public class InitializeDatabaseService(IServiceScopeFactory _scopeFactory) : IHostedService
    {
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await CheckDefaultUserAsync(cancellationToken);
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
            string password = StringHelpers.CreateRandomString(16);
            string hash = password.SHA512();
            context.Users.Add(new User
            {
                Username = defaultUsername,
                PasswordHash = hash,
                Role = UserRole.Admin,
                Email = defaultUsername + "@octockup.local"
            });
            await context.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Default user '{defaultUsername}' created with password: '{Password}'",
                defaultUsername, password);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
