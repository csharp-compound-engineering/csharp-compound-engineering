using CompoundDocs.Graph;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CompoundDocs.McpServer.Health;

internal sealed class NeptuneHealthCheck(INeptuneClient client) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var connected = await client.TestConnectionAsync(cancellationToken);
            return connected
                ? HealthCheckResult.Healthy("Neptune connection successful")
                : HealthCheckResult.Unhealthy("Neptune connection test returned false");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Neptune connection failed", ex);
        }
    }
}
