using CompoundDocs.McpServer.Background;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CompoundDocs.McpServer.Health;

internal sealed class GitSyncHealthCheck(IGitSyncStatus syncStatus) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (syncStatus.LastRunFailed)
        {
            return Task.FromResult(
                HealthCheckResult.Unhealthy("Last git sync cycle had failures"));
        }

        if (syncStatus.LastSuccessfulRun is null)
        {
            return Task.FromResult(
                HealthCheckResult.Degraded("Git sync has not completed a successful run yet"));
        }

        var elapsed = DateTimeOffset.UtcNow - syncStatus.LastSuccessfulRun.Value;
        var maxAllowed = TimeSpan.FromSeconds(syncStatus.IntervalSeconds * 2);

        if (elapsed > maxAllowed)
        {
            return Task.FromResult(
                HealthCheckResult.Degraded(
                    $"Last successful git sync was {elapsed.TotalMinutes:F0} minutes ago (threshold: {maxAllowed.TotalMinutes:F0} minutes)"));
        }

        return Task.FromResult(
            HealthCheckResult.Healthy("Git sync is running normally"));
    }
}
