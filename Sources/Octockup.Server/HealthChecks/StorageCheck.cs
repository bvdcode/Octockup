using Octockup.Server.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Octockup.Server.HealthChecks
{
    public class StorageHealthCheck(IFileService _files) : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            if (!_files.IsStorageHealthy())
            {
                return Task.FromResult(HealthCheckResult.Degraded());
            }
            return Task.FromResult(HealthCheckResult.Healthy());
        }
    }
}