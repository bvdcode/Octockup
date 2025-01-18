using Octockup.Server.Database;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Octockup.Server.HealthChecks
{
    public class DatabaseHealthCheck(IServiceProvider _serviceProvider) : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            if (await dbContext.Database.CanConnectAsync(cancellationToken))
            {
                return HealthCheckResult.Healthy("Database is available");
            }

            return HealthCheckResult.Unhealthy("Database is unavailable");
        }
    }
}