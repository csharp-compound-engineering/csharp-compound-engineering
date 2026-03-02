using CompoundDocs.Vector;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CompoundDocs.McpServer.Health;

internal sealed class OpenSearchHealthCheck(IOpenSearchClientFactory clientFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await clientFactory.GetClient().PingAsync(ct: cancellationToken);
            return HealthCheckResult.Healthy("OpenSearch connection successful");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("OpenSearch connection failed", ex);
        }
    }
}
