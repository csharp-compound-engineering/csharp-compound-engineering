using CompoundDocs.McpServer.Session;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CompoundDocs.McpServer.DependencyInjection;

/// <summary>
/// Extension methods for registering session and context management services.
/// </summary>
public static class SessionServiceCollectionExtensions
{
    /// <summary>
    /// Adds session and context management services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// This registers:
    /// - ISessionContext as Scoped (per-request tenant isolation)
    /// - ProjectActivationService as Singleton (manages project lifecycle)
    ///
    /// The ISessionContext is scoped to allow for per-request tenant isolation,
    /// while ProjectActivationService is singleton since it coordinates state
    /// across the application lifetime.
    /// </remarks>
    public static IServiceCollection AddSessionServices(this IServiceCollection services)
    {
        // Register ISessionContext as scoped for per-request isolation
        // Using TryAdd to avoid duplicate registration
        services.TryAddScoped<ISessionContext, SessionContext>();

        // Register ProjectActivationService as singleton
        // It coordinates project lifecycle and manages session state
        services.TryAddSingleton<ProjectActivationService>();

        return services;
    }

    /// <summary>
    /// Adds session and context management services with a shared session context.
    /// Use this when a single session context should be shared across all scopes
    /// (e.g., in a single-project MCP server instance).
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// This registers:
    /// - ISessionContext as Singleton (shared across all requests)
    /// - ProjectActivationService as Singleton (manages project lifecycle)
    ///
    /// Use this variant for MCP servers that run as stdio processes where
    /// there is only ever one active project at a time.
    /// </remarks>
    public static IServiceCollection AddSessionServicesAsSingleton(this IServiceCollection services)
    {
        // Register ISessionContext as singleton for shared state
        // This is appropriate for MCP stdio servers with a single project
        services.TryAddSingleton<ISessionContext, SessionContext>();

        // Register ProjectActivationService as singleton
        services.TryAddSingleton<ProjectActivationService>();

        return services;
    }

    /// <summary>
    /// Adds session and context management services with a custom session context factory.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="sessionContextFactory">Factory function to create session contexts.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSessionServices(
        this IServiceCollection services,
        Func<IServiceProvider, ISessionContext> sessionContextFactory)
    {
        ArgumentNullException.ThrowIfNull(sessionContextFactory);

        // Register ISessionContext with custom factory
        services.TryAdd(ServiceDescriptor.Scoped(sessionContextFactory));

        // Register ProjectActivationService as singleton
        services.TryAddSingleton<ProjectActivationService>();

        return services;
    }

    /// <summary>
    /// Adds only the ProjectActivationService without registering ISessionContext.
    /// Use this when ISessionContext is registered elsewhere with custom configuration.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddProjectActivationService(this IServiceCollection services)
    {
        services.TryAddSingleton<ProjectActivationService>();
        return services;
    }
}
