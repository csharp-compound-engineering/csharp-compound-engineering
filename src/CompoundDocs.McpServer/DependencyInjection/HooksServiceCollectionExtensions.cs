using CompoundDocs.McpServer.Hooks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CompoundDocs.McpServer.DependencyInjection;

/// <summary>
/// Extension methods for registering hook services.
/// </summary>
public static class HooksServiceCollectionExtensions
{
    /// <summary>
    /// Adds hook services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// This registers the following services:
    /// - SessionHookExecutor: Manages and executes session lifecycle hooks
    /// - DocumentHookExecutor: Manages and executes document lifecycle hooks
    /// - SessionStartHook: Checks MCP prerequisites at session start
    ///
    /// All hooks are registered as singletons to maintain state across the application lifetime.
    /// </remarks>
    public static IServiceCollection AddHooks(this IServiceCollection services)
    {
        // Register hook executors
        services.TryAddSingleton<SessionHookExecutor>();
        services.TryAddSingleton<DocumentHookExecutor>();

        // Register session hooks
        services.AddSessionHooks();

        // Register document hooks
        services.AddDocumentHooks();

        return services;
    }

    /// <summary>
    /// Adds session lifecycle hooks to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSessionHooks(this IServiceCollection services)
    {
        // Register session start hook
        services.TryAddSingleton<ISessionHook, SessionStartHook>();

        return services;
    }

    /// <summary>
    /// Adds document lifecycle hooks to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDocumentHooks(this IServiceCollection services)
    {
        // Document hooks will be added here as they are implemented
        // For now, just ensure the executor is registered

        return services;
    }

    /// <summary>
    /// Adds hooks with custom configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Action to configure hooks.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddHooks(
        this IServiceCollection services,
        Action<HooksOptions> configure)
    {
        var options = new HooksOptions();
        configure(options);

        // Always register executors
        services.TryAddSingleton<SessionHookExecutor>();
        services.TryAddSingleton<DocumentHookExecutor>();

        if (options.EnableSessionHooks)
        {
            services.AddSessionHooks();
        }

        if (options.EnableDocumentHooks)
        {
            services.AddDocumentHooks();
        }

        return services;
    }
}

/// <summary>
/// Options for configuring hooks.
/// </summary>
public sealed class HooksOptions
{
    /// <summary>
    /// Whether to enable session lifecycle hooks.
    /// Default: true
    /// </summary>
    public bool EnableSessionHooks { get; set; } = true;

    /// <summary>
    /// Whether to enable document lifecycle hooks.
    /// Default: true
    /// </summary>
    public bool EnableDocumentHooks { get; set; } = true;
}
