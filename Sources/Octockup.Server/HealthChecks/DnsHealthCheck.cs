using System.Net;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Octockup.Server.HealthChecks
{
    public class DnsHealthCheck : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            const string host = "example.com";
            IPHostEntry entry = await Dns.GetHostEntryAsync(host, cancellationToken);
            if (entry.AddressList.Length != 0)
            {
                return HealthCheckResult.Healthy("DNS resolver is available");
            }
            return HealthCheckResult.Unhealthy("DNS resolver is unavailable");
        }
    }
}