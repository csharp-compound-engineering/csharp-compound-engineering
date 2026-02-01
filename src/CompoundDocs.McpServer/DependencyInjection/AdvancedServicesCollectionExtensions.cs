using CompoundDocs.Common.Graph;
using CompoundDocs.McpServer.Events;
using CompoundDocs.McpServer.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CompoundDocs.McpServer.DependencyInjection;

/// <summary>
/// Extension methods for registering advanced feature services.
/// </summary>
public static class AdvancedServicesCollectionExtensions
{
    /// <summary>
    /// Adds all advanced feature services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// This registers the following services:
    /// - IDocTypeRegistrationService: Runtime doc-type registration with JSON Schema validation
    /// - ICrossReferenceService: Wiki-style and markdown link resolution
    /// - IDocumentEventPublisher/IDocumentEventSubscriber: Document lifecycle events
    /// - ISupersedesService: Document supersession handling
    /// - DocumentEventProcessingService: Background service for event processing
    ///
    /// Prerequisites:
    /// - IDocTypeRegistry must be registered (from DocTypes services)
    /// - SchemaValidator must be registered (from parsing services)
    /// - DocumentLinkGraph must be registered (from document processing services)
    /// - IDocumentRepository must be registered (from data services)
    /// </remarks>
    public static IServiceCollection AddAdvancedFeatures(this IServiceCollection services)
    {
        // Add document link graph if not already registered
        services.TryAddSingleton<DocumentLinkGraph>();

        // Register doc-type registration service
        services.AddDocTypeRegistrationServices();

        // Register cross-reference service
        services.AddCrossReferenceServices();

        // Register document lifecycle events
        services.AddDocumentLifecycleEvents();

        // Register supersedes service
        services.AddSupersedesServices();

        return services;
    }

    /// <summary>
    /// Adds doc-type registration services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDocTypeRegistrationServices(this IServiceCollection services)
    {
        // Register as singleton for caching doc-type definitions
        services.TryAddSingleton<IDocTypeRegistrationService, DocTypeRegistrationService>();

        return services;
    }

    /// <summary>
    /// Adds cross-reference resolution services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCrossReferenceServices(this IServiceCollection services)
    {
        // Register document link graph as singleton (maintains link state)
        services.TryAddSingleton<DocumentLinkGraph>();

        // Register cross-reference service as singleton (maintains resolution cache)
        services.TryAddSingleton<ICrossReferenceService, CrossReferenceService>();

        return services;
    }

    /// <summary>
    /// Adds document lifecycle event services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// This registers:
    /// - DocumentEventPublisher as singleton (shared event channel)
    /// - IDocumentEventPublisher interface
    /// - IDocumentEventSubscriber interface
    /// - DocumentEventProcessingService as hosted service
    /// </remarks>
    public static IServiceCollection AddDocumentLifecycleEvents(this IServiceCollection services)
    {
        // Register the publisher as a singleton (maintains event channel and handlers)
        services.TryAddSingleton<DocumentEventPublisher>();

        // Register interfaces that resolve to the same singleton instance
        services.TryAddSingleton<IDocumentEventPublisher>(sp =>
            sp.GetRequiredService<DocumentEventPublisher>());

        services.TryAddSingleton<IDocumentEventSubscriber>(sp =>
            sp.GetRequiredService<DocumentEventPublisher>());

        // Register the background processing service
        services.AddHostedService<DocumentEventProcessingService>();

        return services;
    }

    /// <summary>
    /// Adds supersedes handling services.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSupersedesServices(this IServiceCollection services)
    {
        // Register as singleton to maintain supersession tracking state
        services.TryAddSingleton<ISupersedesService, SupersedesService>();

        return services;
    }

    /// <summary>
    /// Adds all advanced features with custom configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Action to configure advanced features.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAdvancedFeatures(
        this IServiceCollection services,
        Action<AdvancedFeaturesOptions> configure)
    {
        var options = new AdvancedFeaturesOptions();
        configure(options);

        if (options.EnableDocTypeRegistration)
        {
            services.AddDocTypeRegistrationServices();
        }

        if (options.EnableCrossReferenceResolution)
        {
            services.AddCrossReferenceServices();
        }

        if (options.EnableLifecycleEvents)
        {
            services.AddDocumentLifecycleEvents();
        }

        if (options.EnableSupersedesHandling)
        {
            services.AddSupersedesServices();
        }

        return services;
    }
}

/// <summary>
/// Options for configuring advanced features.
/// </summary>
public sealed class AdvancedFeaturesOptions
{
    /// <summary>
    /// Whether to enable runtime doc-type registration.
    /// Default: true
    /// </summary>
    public bool EnableDocTypeRegistration { get; set; } = true;

    /// <summary>
    /// Whether to enable cross-reference resolution.
    /// Default: true
    /// </summary>
    public bool EnableCrossReferenceResolution { get; set; } = true;

    /// <summary>
    /// Whether to enable document lifecycle events.
    /// Default: true
    /// </summary>
    public bool EnableLifecycleEvents { get; set; } = true;

    /// <summary>
    /// Whether to enable supersedes handling.
    /// Default: true
    /// </summary>
    public bool EnableSupersedesHandling { get; set; } = true;
}
