using CompoundDocs.McpServer.Observability;
using CompoundDocs.McpServer.Tools;
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
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// This registers the following services:
    /// <list type="bullet">
    ///   <item><description>MetricsCollector - Collects operational metrics using System.Diagnostics.Metrics</description></item>
    ///   <item><description>HealthCheckService - Background service for periodic health checks</description></item>
    ///   <item><description>DiagnosticsTool - MCP tool for runtime diagnostics</description></item>
    /// </list>
    /// </remarks>
    public static IServiceCollection AddObservability(this IServiceCollection services)
    {
        // Register MetricsCollector as singleton (shared metrics state)
        services.TryAddSingleton<MetricsCollector>();

        // Register HealthCheckService as singleton and hosted service
        services.TryAddSingleton<HealthCheckService>();
        services.AddHostedService(sp => sp.GetRequiredService<HealthCheckService>());

        // Register DiagnosticsTool for MCP
        services.TryAddScoped<DiagnosticsTool>();

        // Ensure HttpClient is available for health checks
        services.AddHttpClient("HealthCheck", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        return services;
    }

    /// <summary>
    /// Adds observability services with custom configuration.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configureHealthCheck">Optional action to configure health check HTTP client.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddObservability(
        this IServiceCollection services,
        Action<IHttpClientBuilder>? configureHealthCheck)
    {
        // Register core observability services
        services.TryAddSingleton<MetricsCollector>();
        services.TryAddSingleton<HealthCheckService>();
        services.AddHostedService(sp => sp.GetRequiredService<HealthCheckService>());
        services.TryAddScoped<DiagnosticsTool>();

        // Configure health check HTTP client
        var builder = services.AddHttpClient("HealthCheck", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        configureHealthCheck?.Invoke(builder);

        return services;
    }
}
