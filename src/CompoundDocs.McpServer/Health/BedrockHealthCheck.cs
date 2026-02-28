using CompoundDocs.Bedrock;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CompoundDocs.McpServer.Health;

internal sealed class BedrockHealthCheck(IBedrockEmbeddingService embeddingService) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await embeddingService.GenerateEmbeddingAsync("health", cancellationToken);
            return HealthCheckResult.Healthy("Bedrock connection successful");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Bedrock connection failed", ex);
        }
    }
}
