using CompoundDocs.McpServer.Agents;
using CompoundDocs.McpServer.Skills;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace CompoundDocs.McpServer.DependencyInjection;

/// <summary>
/// Extension methods for registering agent and skill services.
/// </summary>
public static class AgentServiceCollectionExtensions
{
    /// <summary>
    /// Adds agent and skill services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="agentsDirectory">
    /// Optional custom agents directory path.
    /// If null, uses the default "agents" directory.
    /// </param>
    /// <param name="skillsDirectory">
    /// Optional custom skills directory path.
    /// If null, uses the default "skills" directory.
    /// </param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// This registers:
    /// - AgentLoader as Singleton (caches agent definitions, thread-safe)
    /// - IAgentRegistry as Singleton (provides agent lookup and query)
    /// - SkillLoader as Singleton (caches skill definitions, thread-safe)
    /// - ISkillExecutor as Scoped (executes skills with dependencies)
    ///
    /// The loaders will initialize on first use or when explicitly initialized.
    /// </remarks>
    public static IServiceCollection AddAgentsAndSkills(
        this IServiceCollection services,
        string? agentsDirectory = null,
        string? skillsDirectory = null)
    {
        // Register agent services
        services.AddAgentServices(agentsDirectory);

        // Register skill services
        services.AddSkillServices(skillsDirectory);

        return services;
    }

    /// <summary>
    /// Adds only agent services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="agentsDirectory">
    /// Optional custom agents directory path.
    /// If null, uses the default "agents" directory.
    /// </param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAgentServices(
        this IServiceCollection services,
        string? agentsDirectory = null)
    {
        // Register AgentLoader as singleton (caches loaded agents)
        services.TryAddSingleton<AgentLoader>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<AgentLoader>>();
            return new AgentLoader(logger);
        });

        // Configure agent registry options
        services.Configure<AgentRegistryOptions>(options =>
        {
            options.AgentsDirectory = agentsDirectory ?? AgentLoader.DefaultAgentsDirectory;
        });

        // Register IAgentRegistry as singleton
        services.TryAddSingleton<IAgentRegistry, AgentRegistry>();

        return services;
    }

    /// <summary>
    /// Adds only skill services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="skillsDirectory">
    /// Optional custom skills directory path.
    /// If null, uses the default "skills" directory.
    /// </param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSkillServices(
        this IServiceCollection services,
        string? skillsDirectory = null)
    {
        // Register SkillLoader as singleton (caches loaded skills)
        services.TryAddSingleton<SkillLoader>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<SkillLoader>>();
            return new SkillLoader(logger);
        });

        // Register ISkillExecutor as scoped (may have dependencies on scoped services)
        services.TryAddScoped<ISkillExecutor, SkillExecutor>();

        return services;
    }

    /// <summary>
    /// Adds and initializes agent services, loading all agent definitions at startup.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="agentsDirectory">
    /// Optional custom agents directory path.
    /// If null, uses the default "agents" directory.
    /// </param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// This is useful when you want to ensure all agents are loaded and validated
    /// at application startup rather than on first use.
    /// </remarks>
    public static IServiceCollection AddAgentServicesWithInitialization(
        this IServiceCollection services,
        string? agentsDirectory = null)
    {
        services.AddAgentServices(agentsDirectory);

        // Register a hosted service to initialize agents on startup
        services.AddHostedService<AgentInitializationService>();

        return services;
    }
}
