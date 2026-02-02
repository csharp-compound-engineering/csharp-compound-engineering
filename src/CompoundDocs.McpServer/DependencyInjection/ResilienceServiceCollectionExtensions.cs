using CompoundDocs.McpServer.Resilience;
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
    public static IServiceCollection AddResilienceServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ResilienceOptions>(
            configuration.GetSection(ResilienceOptions.SectionName));
        services.Configure<RateLimitOptions>(
            configuration.GetSection(RateLimitOptions.SectionName));
        services.Configure<EmbeddingCacheOptions>(
            configuration.GetSection(EmbeddingCacheOptions.SectionName));

        services.TryAddSingleton<IResiliencePolicies, ResiliencePolicies>();
        services.TryAddSingleton<IRateLimiter, RateLimiter>();
        services.TryAddSingleton<IEmbeddingCache, EmbeddingCache>();

        return services;
    }

    /// <summary>
    /// Adds resilience services with default configuration.
    /// </summary>
    public static IServiceCollection AddResilienceServicesWithDefaults(this IServiceCollection services)
    {
        services.Configure<ResilienceOptions>(_ => { });
        services.Configure<RateLimitOptions>(_ => { });
        services.Configure<EmbeddingCacheOptions>(_ => { });

        services.TryAddSingleton<IResiliencePolicies, ResiliencePolicies>();
        services.TryAddSingleton<IRateLimiter, RateLimiter>();
        services.TryAddSingleton<IEmbeddingCache, EmbeddingCache>();

        return services;
    }
}
