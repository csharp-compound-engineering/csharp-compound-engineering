using CompoundDocs.McpServer.Models;
using CompoundDocs.McpServer.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.PgVector;
using Npgsql;

namespace CompoundDocs.McpServer.SemanticKernel;

/// <summary>
/// Factory for creating PostgreSQL vector store collection instances.
/// Configures HNSW index with optimal parameters for semantic search.
/// </summary>
public sealed class VectorStoreFactory : IDisposable
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<VectorStoreFactory> _logger;
    private bool _disposed;

    /// <summary>
    /// HNSW index parameter: number of bi-directional links per element.
    /// Higher values improve recall but increase index size.
    /// </summary>
    public const int HnswM = 32;

    /// <summary>
    /// HNSW index parameter: size of the dynamic candidate list during construction.
    /// Higher values improve index quality but slow down construction.
    /// </summary>
    public const int HnswEfConstruction = 128;

    /// <summary>
    /// HNSW index parameter: size of the dynamic candidate list during search.
    /// Higher values improve recall but slow down search.
    /// </summary>
    public const int HnswEfSearch = 64;

    /// <summary>
    /// Collection name for compound documents.
    /// </summary>
    public const string DocumentsCollectionName = "compound_documents";

    /// <summary>
    /// Collection name for document chunks.
    /// </summary>
    public const string DocumentChunksCollectionName = "document_chunks";

    /// <summary>
    /// Collection name for external documents.
    /// </summary>
    public const string ExternalDocumentsCollectionName = "external_documents";

    /// <summary>
    /// Collection name for external document chunks.
    /// </summary>
    public const string ExternalDocumentChunksCollectionName = "external_document_chunks";

    /// <summary>
    /// Creates a new instance of the VectorStoreFactory.
    /// </summary>
    /// <param name="options">MCP server options containing PostgreSQL configuration.</param>
    /// <param name="logger">Logger instance.</param>
    public VectorStoreFactory(
        IOptions<CompoundDocsServerOptions> options,
        ILogger<VectorStoreFactory> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var connectionString = BuildConnectionString(options.Value.Postgres);

        _logger.LogInformation("Creating PostgreSQL data source for vector store");

        var builder = new NpgsqlDataSourceBuilder(connectionString);

        // Enable pgvector support - CRITICAL for vector operations
        builder.UseVector();

        _dataSource = builder.Build();
    }

    /// <summary>
    /// Gets the configured NpgsqlDataSource.
    /// </summary>
    public NpgsqlDataSource DataSource => _dataSource;

    /// <summary>
    /// Gets the connection string for creating collections.
    /// </summary>
    public string ConnectionString => _dataSource.ConnectionString;

    /// <summary>
    /// Creates a collection for compound documents.
    /// </summary>
    /// <returns>A PostgresCollection for CompoundDocument.</returns>
    public PostgresCollection<string, CompoundDocument> CreateDocumentsCollection()
    {
        _logger.LogDebug("Creating documents collection: {Name}", DocumentsCollectionName);
        return new PostgresCollection<string, CompoundDocument>(
            ConnectionString,
            DocumentsCollectionName);
    }

    /// <summary>
    /// Creates a collection for document chunks.
    /// </summary>
    /// <returns>A PostgresCollection for DocumentChunk.</returns>
    public PostgresCollection<string, DocumentChunk> CreateDocumentChunksCollection()
    {
        _logger.LogDebug("Creating document chunks collection: {Name}", DocumentChunksCollectionName);
        return new PostgresCollection<string, DocumentChunk>(
            ConnectionString,
            DocumentChunksCollectionName);
    }

    /// <summary>
    /// Creates a collection for external documents.
    /// </summary>
    /// <returns>A PostgresCollection for ExternalDocument.</returns>
    public PostgresCollection<string, ExternalDocument> CreateExternalDocumentsCollection()
    {
        _logger.LogDebug("Creating external documents collection: {Name}", ExternalDocumentsCollectionName);
        return new PostgresCollection<string, ExternalDocument>(
            ConnectionString,
            ExternalDocumentsCollectionName);
    }

    /// <summary>
    /// Creates a collection for external document chunks.
    /// </summary>
    /// <returns>A PostgresCollection for ExternalDocumentChunk.</returns>
    public PostgresCollection<string, ExternalDocumentChunk> CreateExternalDocumentChunksCollection()
    {
        _logger.LogDebug("Creating external document chunks collection: {Name}", ExternalDocumentChunksCollectionName);
        return new PostgresCollection<string, ExternalDocumentChunk>(
            ConnectionString,
            ExternalDocumentChunksCollectionName);
    }

    /// <summary>
    /// Ensures all collections exist in the database.
    /// Creates tables with proper vector indexes if they don't exist.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task EnsureCollectionsExistAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Ensuring vector store collections exist");

        // Create collections and ensure they exist
        // PostgresCollection doesn't implement IAsyncDisposable, so we use regular using
        using var documentsCollection = CreateDocumentsCollection();
        using var chunksCollection = CreateDocumentChunksCollection();
        using var externalDocsCollection = CreateExternalDocumentsCollection();
        using var externalChunksCollection = CreateExternalDocumentChunksCollection();

        // EnsureCollectionExistsAsync is the new API (May 2025+)
        await documentsCollection.EnsureCollectionExistsAsync(cancellationToken);
        await chunksCollection.EnsureCollectionExistsAsync(cancellationToken);
        await externalDocsCollection.EnsureCollectionExistsAsync(cancellationToken);
        await externalChunksCollection.EnsureCollectionExistsAsync(cancellationToken);

        _logger.LogInformation("All vector store collections created or verified");

        // Configure HNSW search parameters
        await ConfigureHnswSearchAsync(cancellationToken);
    }

    /// <summary>
    /// Configures HNSW search parameters for the current session.
    /// </summary>
    private async Task ConfigureHnswSearchAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Configuring HNSW search parameters: ef_search={EfSearch}", HnswEfSearch);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand($"SET hnsw.ef_search = {HnswEfSearch}", connection);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Validates that the embedding dimensions match the expected value.
    /// </summary>
    /// <param name="actualDimensions">The actual dimensions from an embedding.</param>
    public void ValidateDimensions(int actualDimensions)
    {
        if (actualDimensions != OllamaConnectionOptions.EmbeddingDimensions)
        {
            throw new InvalidOperationException(
                $"Embedding dimension mismatch: expected {OllamaConnectionOptions.EmbeddingDimensions}, " +
                $"got {actualDimensions}. Ensure mxbai-embed-large model is being used.");
        }
    }

    private static string BuildConnectionString(PostgresConnectionOptions options)
    {
        return $"Host={options.Host};" +
               $"Port={options.Port};" +
               $"Database={options.Database};" +
               $"Username={options.Username};" +
               $"Password={options.Password}";
    }

    /// <summary>
    /// Disposes the data source.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _dataSource.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Extension methods for registering vector store services.
/// </summary>
public static class VectorStoreServiceCollectionExtensions
{
    /// <summary>
    /// Adds PostgreSQL vector store services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPostgresVectorStore(this IServiceCollection services)
    {
        // Register the factory (manages NpgsqlDataSource lifecycle)
        services.AddSingleton<VectorStoreFactory>();

        // Register NpgsqlDataSource from factory
        services.AddSingleton(sp =>
        {
            var factory = sp.GetRequiredService<VectorStoreFactory>();
            return factory.DataSource;
        });

        // Register document collections as transient (create new instances as needed)
        services.AddTransient(sp =>
        {
            var factory = sp.GetRequiredService<VectorStoreFactory>();
            return factory.CreateDocumentsCollection();
        });

        services.AddTransient(sp =>
        {
            var factory = sp.GetRequiredService<VectorStoreFactory>();
            return factory.CreateDocumentChunksCollection();
        });

        return services;
    }
}
