using CompoundDocs.McpServer.Resilience;
using CompoundDocs.McpServer.SemanticKernel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CompoundDocs.McpServer.DependencyInjection;

/// <summary>
/// Extension methods for registering resilience services with the service collection.
/// </summary>
public static class ResilienceServiceCollectionExtensions
{
    /// <summary>
    /// Adds resilience services including rate limiting, circuit breaker, and graceful degradation.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddResilienceServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure resilience options
        services.Configure<ResilienceOptions>(
            configuration.GetSection(ResilienceOptions.SectionName));
        services.Configure<RateLimitOptions>(
            configuration.GetSection(RateLimitOptions.SectionName));
        services.Configure<EmbeddingCacheOptions>(
            configuration.GetSection(EmbeddingCacheOptions.SectionName));

        // Register core resilience services
        services.TryAddSingleton<IResiliencePolicies, ResiliencePolicies>();
        services.TryAddSingleton<IRateLimiter, RateLimiter>();
        services.TryAddSingleton<IEmbeddingCache, EmbeddingCache>();

        return services;
    }

    /// <summary>
    /// Decorates the IEmbeddingService with resilience capabilities.
    /// Should be called after the base IEmbeddingService is registered.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddResilientEmbeddingService(this IServiceCollection services)
    {
        // Find and replace the existing IEmbeddingService registration with the resilient wrapper
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IEmbeddingService));

        if (descriptor != null)
        {
            services.Remove(descriptor);

            // Re-register the original implementation with a different lifetime key
            if (descriptor.ImplementationType != null)
            {
                services.Add(new ServiceDescriptor(
                    descriptor.ImplementationType,
                    descriptor.ImplementationType,
                    descriptor.Lifetime));

                // Register the resilient wrapper as IEmbeddingService
                services.Add(new ServiceDescriptor(
                    typeof(IEmbeddingService),
                    sp => new ResilientEmbeddingService(
                        (IEmbeddingService)sp.GetRequiredService(descriptor.ImplementationType),
                        sp.GetRequiredService<IResiliencePolicies>(),
                        sp.GetRequiredService<IEmbeddingCache>(),
                        sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ResilientEmbeddingService>>()),
                    descriptor.Lifetime));
            }
            else if (descriptor.ImplementationFactory != null)
            {
                // For factory-based registrations
                services.Add(new ServiceDescriptor(
                    typeof(IEmbeddingService),
                    sp =>
                    {
                        var innerService = (IEmbeddingService)descriptor.ImplementationFactory(sp);
                        return new ResilientEmbeddingService(
                            innerService,
                            sp.GetRequiredService<IResiliencePolicies>(),
                            sp.GetRequiredService<IEmbeddingCache>(),
                            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ResilientEmbeddingService>>());
                    },
                    descriptor.Lifetime));
            }
        }
        else
        {
            // If no existing registration, register with a factory that will fail if inner service missing
            services.TryAddScoped<IEmbeddingService>(sp =>
                throw new InvalidOperationException(
                    "ResilientEmbeddingService requires a base IEmbeddingService to be registered first. " +
                    "Ensure AddSemanticKernelServices() is called before AddResilientEmbeddingService()."));
        }

        return services;
    }

    /// <summary>
    /// Adds resilience services with default configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddResilienceServicesWithDefaults(this IServiceCollection services)
    {
        // Configure with default options
        services.Configure<ResilienceOptions>(_ => { });
        services.Configure<RateLimitOptions>(_ => { });
        services.Configure<EmbeddingCacheOptions>(_ => { });

        // Register core resilience services
        services.TryAddSingleton<IResiliencePolicies, ResiliencePolicies>();
        services.TryAddSingleton<IRateLimiter, RateLimiter>();
        services.TryAddSingleton<IEmbeddingCache, EmbeddingCache>();

        return services;
    }
}
