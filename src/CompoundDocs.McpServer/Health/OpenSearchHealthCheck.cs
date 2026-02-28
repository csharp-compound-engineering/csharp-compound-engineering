using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenSearch.Client;

namespace CompoundDocs.McpServer.Health;

internal sealed class OpenSearchHealthCheck(IOpenSearchClient client) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await client.PingAsync(ct: cancellationToken);
            return HealthCheckResult.Healthy("OpenSearch connection successful");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("OpenSearch connection failed", ex);
        }
    }
}
