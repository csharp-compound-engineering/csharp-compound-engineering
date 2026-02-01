using CompoundDocs.Common.Graph;
using CompoundDocs.Common.Parsing;
using CompoundDocs.McpServer.Services.DocumentProcessing;
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
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="chunkThreshold">
    /// The line count threshold for chunking documents.
    /// Documents exceeding this threshold will be chunked at header boundaries.
    /// Defaults to 500 lines.
    /// </param>
    /// <returns>The service collection for chaining.</returns>
    /// <remarks>
    /// This registers:
    /// - MarkdownParser as Singleton (stateless, thread-safe)
    /// - FrontmatterParser as Singleton (stateless, thread-safe)
    /// - SchemaValidator as Singleton (caches schemas, thread-safe)
    /// - DocumentLinkGraph as Singleton (maintains link state)
    /// - DocumentChunker as Singleton (stateless configuration)
    /// - IDocumentProcessor as Scoped (depends on embedding service)
    /// - IDocumentIndexer as Scoped (depends on repositories)
    ///
    /// Prerequisites:
    /// - IEmbeddingService must be registered (from SemanticKernel services)
    /// - IDocumentRepository must be registered (from Data services)
    /// </remarks>
    public static IServiceCollection AddDocumentProcessingServices(
        this IServiceCollection services,
        int chunkThreshold = DocumentChunker.DefaultChunkThreshold)
    {
        // Register parsing services as singletons (stateless and thread-safe)
        services.TryAddSingleton<MarkdownParser>();
        services.TryAddSingleton<FrontmatterParser>();
        services.TryAddSingleton<SchemaValidator>();

        // Register document link graph as singleton (maintains state)
        services.TryAddSingleton<DocumentLinkGraph>();

        // Register document chunker with configured threshold
        services.TryAddSingleton(sp =>
        {
            var markdownParser = sp.GetRequiredService<MarkdownParser>();
            return new DocumentChunker(markdownParser, chunkThreshold);
        });

        // Register document processor as scoped (depends on scoped embedding service)
        services.TryAddScoped<IDocumentProcessor, DocumentProcessor>();

        // Register document indexer as scoped (depends on scoped repositories)
        services.TryAddScoped<IDocumentIndexer, DocumentIndexer>();

        return services;
    }

    /// <summary>
    /// Adds only the parsing services without the full document processing pipeline.
    /// Useful for scenarios where only parsing is needed.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddParsingServices(this IServiceCollection services)
    {
        services.TryAddSingleton<MarkdownParser>();
        services.TryAddSingleton<FrontmatterParser>();
        services.TryAddSingleton<SchemaValidator>();

        return services;
    }

    /// <summary>
    /// Adds only the document link graph service.
    /// Useful for scenarios where only link tracking is needed.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDocumentLinkGraph(this IServiceCollection services)
    {
        services.TryAddSingleton<DocumentLinkGraph>();
        return services;
    }
}
