using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Octockup.Server.HealthChecks
{
    public class NetworkHealthCheck : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            const string url = "https://example.com";
            using var client = new HttpClient();
            var response = await client.GetAsync(url, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return HealthCheckResult.Healthy($"{url} web page is available");
            }
            return HealthCheckResult.Unhealthy($"{url} is unavailable. Status code: {response.StatusCode}");
        }
    }
}