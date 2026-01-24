using CompoundDocs.Common.Parsing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CompoundDocs.McpServer.DependencyInjection;

/// <summary>
/// Extension methods for registering document processing services.
/// </summary>
public static class DocumentProcessingServiceCollectionExtensions
{
    /// <summary>
    /// Adds document processing services to the service collection.
    /// </summary>
    public static IServiceCollection AddDocumentProcessingServices(this IServiceCollection services)
    {
        services.TryAddSingleton<MarkdownParser>();
        services.TryAddSingleton<FrontmatterParser>();
        services.TryAddSingleton<SchemaValidator>();

        return services;
    }

    /// <summary>
    /// Adds only the parsing services without the full document processing pipeline.
    /// </summary>
    public static IServiceCollection AddParsingServices(this IServiceCollection services)
    {
        services.TryAddSingleton<MarkdownParser>();
        services.TryAddSingleton<FrontmatterParser>();
        services.TryAddSingleton<SchemaValidator>();
        return services;
    }
}
