using CompoundDocs.McpServer.Models;
using CompoundDocs.McpServer.SemanticKernel;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Connectors.PgVector;

#pragma warning disable SKEXP0001 // Vector store types are experimental
#pragma warning disable SKEXP0020 // PostgreSQL VectorStore types are experimental

namespace CompoundDocs.McpServer.Data.Repositories;

/// <summary>
/// Repository implementation for vector document operations using Semantic Kernel's VectorStore.
/// Provides CRUD operations and semantic search for CompoundDocument and DocumentChunk entities.
/// </summary>
/// <remarks>
/// This implementation uses a simplified search approach that performs filtering in-memory
/// after vector search retrieval. This ensures compatibility with the evolving Semantic Kernel
/// VectorStore API while maintaining tenant isolation.
/// </remarks>
public sealed class DocumentRepository : IDocumentRepository, IDisposable
{
    private readonly PostgresCollection<string, CompoundDocument> _documentsCollection;
    private readonly PostgresCollection<string, DocumentChunk> _chunksCollection;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<DocumentRepository> _logger;
    private bool _disposed;

    /// <summary>
    /// Maximum number of results to retrieve from vector search before filtering.
    /// </summary>
    private const int MaxSearchResults = 10000;

    /// <summary>
    /// Initializes a new instance of the DocumentRepository.
    /// </summary>
    /// <param name="vectorStoreFactory">The vector store factory for creating collections.</param>
    /// <param name="embeddingService">The embedding service for vector generation.</param>
    /// <param name="logger">The logger instance.</param>
    public DocumentRepository(
        VectorStoreFactory vectorStoreFactory,
        IEmbeddingService embeddingService,
        ILogger<DocumentRepository> logger)
    {
        ArgumentNullException.ThrowIfNull(vectorStoreFactory);
        ArgumentNullException.ThrowIfNull(embeddingService);
        ArgumentNullException.ThrowIfNull(logger);

        _documentsCollection = vectorStoreFactory.CreateDocumentsCollection();
        _chunksCollection = vectorStoreFactory.CreateDocumentChunksCollection();
        _embeddingService = embeddingService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<CompoundDocument> UpsertAsync(CompoundDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        _logger.LogDebug("Upserting document: {Id} at {FilePath}", document.Id, document.FilePath);

        // Generate embedding if not already present
        if (document.Vector is null || document.Vector.Value.Length == 0)
        {
            _logger.LogDebug("Generating embedding for document: {Id}", document.Id);
            var content = $"{document.Title}\n\n{document.Content}";
            document.Vector = await _embeddingService.GenerateEmbeddingAsync(content, cancellationToken);
        }

        await _documentsCollection.UpsertAsync(document, cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Document upserted: {FilePath} ({ContentLength} chars)",
            document.FilePath,
            document.Content.Length);

        return document;
    }

    /// <inheritdoc />
    public async Task<CompoundDocument?> GetByTenantKeyAsync(string tenantKey, string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        _logger.LogDebug("Getting document by tenant key: {TenantKey} and path: {FilePath}", tenantKey, filePath);

        // Use a dummy embedding for retrieval (we filter in memory by tenant and path)
        var dummyEmbedding = new ReadOnlyMemory<float>(new float[_embeddingService.Dimensions]);

        // Search with a reasonable limit and filter results in memory
        await foreach (var result in _documentsCollection.SearchAsync(dummyEmbedding, top: MaxSearchResults, cancellationToken: cancellationToken))
        {
            if (result.Record.TenantKey == tenantKey && result.Record.FilePath == filePath)
            {
                return result.Record;
            }
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<CompoundDocument?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        _logger.LogDebug("Getting document by ID: {Id}", id);

        return await _documentsCollection.GetAsync(id, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SearchResult>> SearchAsync(
        ReadOnlyMemory<float> embedding,
        string tenantKey,
        int limit = 10,
        float minRelevance = 0.0f,
        string? docType = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantKey);

        _logger.LogDebug(
            "Searching documents: tenant={TenantKey}, limit={Limit}, minRelevance={MinRelevance}, docType={DocType}",
            tenantKey, limit, minRelevance, docType);

        // Search with extra results to account for post-filtering
        var searchLimit = Math.Min(limit * 5, MaxSearchResults);

        var results = new List<SearchResult>();
        await foreach (var result in _documentsCollection.SearchAsync(embedding, top: searchLimit, cancellationToken: cancellationToken))
        {
            // Filter by tenant key (required)
            if (result.Record.TenantKey != tenantKey)
            {
                continue;
            }

            // Filter by doc type (optional)
            if (!string.IsNullOrWhiteSpace(docType) && result.Record.DocType != docType)
            {
                continue;
            }

            // Filter by minimum relevance score
            var score = result.Score ?? 0.0;
            if (score >= minRelevance)
            {
                results.Add(new SearchResult(result.Record, (float)score));
            }

            // Stop once we have enough results
            if (results.Count >= limit)
            {
                break;
            }
        }

        _logger.LogInformation(
            "Search found {Count} results above threshold {Threshold}",
            results.Count, minRelevance);

        return results;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        _logger.LogDebug("Deleting document: {Id}", id);

        // First delete associated chunks
        await DeleteChunksAsync(id, cancellationToken);

        // Then delete the document
        await _documentsCollection.DeleteAsync(id, cancellationToken: cancellationToken);

        _logger.LogInformation("Document deleted: {Id}", id);
        return true;
    }

    /// <inheritdoc />
    public async Task<int> DeleteByTenantKeyAsync(string tenantKey, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantKey);

        _logger.LogDebug("Deleting all documents for tenant: {TenantKey}", tenantKey);

        // Get all documents for the tenant
        var documents = await GetAllForTenantAsync(tenantKey, cancellationToken: cancellationToken);
        var deleteCount = 0;

        foreach (var doc in documents)
        {
            await DeleteAsync(doc.Id, cancellationToken);
            deleteCount++;
        }

        _logger.LogInformation("Deleted {Count} documents for tenant: {TenantKey}", deleteCount, tenantKey);
        return deleteCount;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CompoundDocument>> GetAllForTenantAsync(
        string tenantKey,
        string? docType = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantKey);

        _logger.LogDebug("Getting all documents for tenant: {TenantKey}, docType: {DocType}", tenantKey, docType);

        var dummyEmbedding = new ReadOnlyMemory<float>(new float[_embeddingService.Dimensions]);

        var results = new List<CompoundDocument>();
        await foreach (var result in _documentsCollection.SearchAsync(dummyEmbedding, top: MaxSearchResults, cancellationToken: cancellationToken))
        {
            // Filter by tenant key
            if (result.Record.TenantKey != tenantKey)
            {
                continue;
            }

            // Filter by doc type (optional)
            if (!string.IsNullOrWhiteSpace(docType) && result.Record.DocType != docType)
            {
                continue;
            }

            results.Add(result.Record);
        }

        _logger.LogDebug("Found {Count} documents for tenant: {TenantKey}", results.Count, tenantKey);
        return results;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DocumentChunk>> GetChunksAsync(string documentId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);

        _logger.LogDebug("Getting chunks for document: {DocumentId}", documentId);

        var dummyEmbedding = new ReadOnlyMemory<float>(new float[_embeddingService.Dimensions]);

        var results = new List<DocumentChunk>();
        await foreach (var result in _chunksCollection.SearchAsync(dummyEmbedding, top: MaxSearchResults, cancellationToken: cancellationToken))
        {
            if (result.Record.DocumentId == documentId)
            {
                results.Add(result.Record);
            }
        }

        // Sort by chunk index/line number
        return results.OrderBy(c => c.StartLine).ToList();
    }

    /// <inheritdoc />
    public async Task<int> UpsertChunksAsync(IEnumerable<DocumentChunk> chunks, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(chunks);

        var chunkList = chunks.ToList();
        _logger.LogDebug("Upserting {Count} chunks", chunkList.Count);

        var count = 0;
        foreach (var chunk in chunkList)
        {
            // Generate embedding if not already present
            if (chunk.Vector is null || chunk.Vector.Value.Length == 0)
            {
                var content = $"{chunk.HeaderPath}\n\n{chunk.Content}";
                chunk.Vector = await _embeddingService.GenerateEmbeddingAsync(content, cancellationToken);
            }

            await _chunksCollection.UpsertAsync(chunk, cancellationToken: cancellationToken);
            count++;
        }

        _logger.LogInformation("Upserted {Count} chunks", count);
        return count;
    }

    /// <inheritdoc />
    public async Task<int> DeleteChunksAsync(string documentId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);

        _logger.LogDebug("Deleting chunks for document: {DocumentId}", documentId);

        var chunks = await GetChunksAsync(documentId, cancellationToken);
        var deleteCount = 0;

        foreach (var chunk in chunks)
        {
            await _chunksCollection.DeleteAsync(chunk.Id, cancellationToken: cancellationToken);
            deleteCount++;
        }

        _logger.LogDebug("Deleted {Count} chunks for document: {DocumentId}", deleteCount, documentId);
        return deleteCount;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ChunkSearchResult>> SearchChunksAsync(
        ReadOnlyMemory<float> embedding,
        string tenantKey,
        int limit = 10,
        float minRelevance = 0.0f,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantKey);

        _logger.LogDebug(
            "Searching chunks: tenant={TenantKey}, limit={Limit}, minRelevance={MinRelevance}",
            tenantKey, limit, minRelevance);

        // Search with extra results to account for post-filtering
        var searchLimit = Math.Min(limit * 5, MaxSearchResults);

        var results = new List<ChunkSearchResult>();
        await foreach (var result in _chunksCollection.SearchAsync(embedding, top: searchLimit, cancellationToken: cancellationToken))
        {
            // Filter by tenant key
            if (result.Record.TenantKey != tenantKey)
            {
                continue;
            }

            // Filter by minimum relevance score
            var score = result.Score ?? 0.0;
            if (score >= minRelevance)
            {
                results.Add(new ChunkSearchResult(result.Record, (float)score));
            }

            // Stop once we have enough results
            if (results.Count >= limit)
            {
                break;
            }
        }

        _logger.LogInformation(
            "Chunk search found {Count} results above threshold {Threshold}",
            results.Count, minRelevance);

        return results;
    }

    /// <inheritdoc />
    public async Task<bool> UpdatePromotionLevelAsync(string documentId, string promotionLevel, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(promotionLevel);

        _logger.LogDebug("Updating promotion level for document {Id} to {Level}", documentId, promotionLevel);

        // Get the document
        var document = await GetByIdAsync(documentId, cancellationToken);
        if (document is null)
        {
            _logger.LogWarning("Document not found for promotion update: {Id}", documentId);
            return false;
        }

        // Update document promotion level
        document.PromotionLevel = promotionLevel;
        await _documentsCollection.UpsertAsync(document, cancellationToken: cancellationToken);

        // Update all chunks
        var chunks = await GetChunksAsync(documentId, cancellationToken);
        foreach (var chunk in chunks)
        {
            chunk.PromotionLevel = promotionLevel;
            await _chunksCollection.UpsertAsync(chunk, cancellationToken: cancellationToken);
        }

        _logger.LogInformation(
            "Updated promotion level for document {Id} and {ChunkCount} chunks to {Level}",
            documentId, chunks.Count, promotionLevel);

        return true;
    }

    /// <summary>
    /// Disposes the repository and its collections.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _documentsCollection.Dispose();
            _chunksCollection.Dispose();
            _disposed = true;
        }
    }
}
