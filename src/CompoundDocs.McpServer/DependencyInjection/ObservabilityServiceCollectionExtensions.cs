using CompoundDocs.McpServer.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CompoundDocs.McpServer.DependencyInjection;

/// <summary>
/// Extension methods for registering observability services with the service collection.
/// </summary>
public static class ObservabilityServiceCollectionExtensions
{
    /// <summary>
    /// Adds observability services to the service collection.
    /// </summary>
    public static IServiceCollection AddObservability(this IServiceCollection services)
    {
        services.TryAddSingleton<IMetricsCollector, MetricsCollector>();
        return services;
    }
}
