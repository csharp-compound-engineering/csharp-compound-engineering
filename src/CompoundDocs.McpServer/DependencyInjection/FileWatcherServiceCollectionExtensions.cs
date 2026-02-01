using CompoundDocs.Common.Configuration;
using CompoundDocs.McpServer.Services.FileWatcher;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CompoundDocs.McpServer.DependencyInjection;

/// <summary>
/// Extension methods for registering file watcher services.
/// </summary>
public static class FileWatcherServiceCollectionExtensions
{
    /// <summary>
    /// Adds file watcher services to the service collection.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// This registers:
    /// - IFileWatcherService as Singleton
    /// - FileReconciliationService as Singleton
    /// - FileChangeProcessor as Singleton
    /// - FileWatcherBackgroundService as hosted service
    /// - IDocumentRecordProvider as Singleton (database implementation)
    /// - IFileWatcherDocumentIndexer as Singleton (database implementation)
    /// - FileWatcherSettings as Singleton (default settings)
    /// </remarks>
    public static IServiceCollection AddFileWatcherServices(this IServiceCollection services)
    {
        // Register default FileWatcherSettings if not already registered
        services.TryAddSingleton<FileWatcherSettings>();

        // Register database-backed implementations
        services.TryAddSingleton<IDocumentRecordProvider, DatabaseDocumentRecordProvider>();
        services.TryAddSingleton<IFileWatcherDocumentIndexer, DatabaseDocumentIndexer>();

        // Register core services
        services.TryAddSingleton<FileReconciliationService>();
        services.TryAddSingleton<FileChangeProcessor>();
        services.TryAddSingleton<IFileWatcherService, FileWatcherService>();

        // Register background service
        services.AddHostedService<FileWatcherBackgroundService>();

        return services;
    }

    /// <summary>
    /// Adds file watcher services with custom settings.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="settings">The file watcher settings to use.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddFileWatcherServices(
        this IServiceCollection services,
        FileWatcherSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        // Register the provided settings
        services.AddSingleton(settings);

        // Register the rest of the services
        return services.AddFileWatcherServices();
    }

    /// <summary>
    /// Adds file watcher services with configuration action.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configureSettings">Action to configure file watcher settings.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddFileWatcherServices(
        this IServiceCollection services,
        Action<FileWatcherSettings> configureSettings)
    {
        ArgumentNullException.ThrowIfNull(configureSettings);

        var settings = new FileWatcherSettings();
        configureSettings(settings);

        return services.AddFileWatcherServices(settings);
    }

    /// <summary>
    /// Adds a custom document record provider implementation.
    /// Call this before AddFileWatcherServices to override the stub.
    /// </summary>
    /// <typeparam name="TProvider">The type of the document record provider.</typeparam>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDocumentRecordProvider<TProvider>(this IServiceCollection services)
        where TProvider : class, IDocumentRecordProvider
    {
        services.AddSingleton<IDocumentRecordProvider, TProvider>();
        return services;
    }

    /// <summary>
    /// Adds a custom file watcher document indexer implementation.
    /// Call this before AddFileWatcherServices to override the stub.
    /// </summary>
    /// <typeparam name="TIndexer">The type of the document indexer.</typeparam>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddFileWatcherDocumentIndexer<TIndexer>(this IServiceCollection services)
        where TIndexer : class, IFileWatcherDocumentIndexer
    {
        services.AddSingleton<IFileWatcherDocumentIndexer, TIndexer>();
        return services;
    }
}
