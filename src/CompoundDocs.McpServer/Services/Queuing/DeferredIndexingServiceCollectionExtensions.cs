using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CompoundDocs.McpServer.Services.Queuing;

/// <summary>
/// Extension methods for registering deferred indexing services.
/// </summary>
public static class DeferredIndexingServiceCollectionExtensions
{
    /// <summary>
    /// Adds deferred indexing services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDeferredIndexingServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Bind configuration
        services.Configure<DeferredIndexingOptions>(
            configuration.GetSection(DeferredIndexingOptions.SectionName));

        // Register queue as singleton
        services.AddSingleton<IDeferredIndexingQueue, InMemoryDeferredIndexingQueue>();

        // Register background processor
        services.AddHostedService<DeferredIndexingProcessor>();

        return services;
    }
}
