using CompoundDocs.McpServer.Models;

namespace CompoundDocs.McpServer.Data.Repositories;

/// <summary>
/// Represents a search result containing a document and its relevance score.
/// </summary>
/// <param name="Document">The matched document.</param>
/// <param name="RelevanceScore">Cosine similarity score (0.0 to 1.0, higher is more relevant).</param>
public sealed record SearchResult(CompoundDocument Document, float RelevanceScore);

/// <summary>
/// Represents a search result containing a chunk and its relevance score.
/// </summary>
/// <param name="Chunk">The matched chunk.</param>
/// <param name="RelevanceScore">Cosine similarity score (0.0 to 1.0, higher is more relevant).</param>
public sealed record ChunkSearchResult(DocumentChunk Chunk, float RelevanceScore);

/// <summary>
/// Repository interface for vector document operations.
/// Provides methods for document CRUD and semantic search using embeddings.
/// </summary>
public interface IDocumentRepository
{
    /// <summary>
    /// Upserts a document and optionally its chunks.
    /// If the document exists, it will be updated; otherwise, a new document is created.
    /// </summary>
    /// <param name="document">The document to upsert.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The upserted document.</returns>
    Task<CompoundDocument> UpsertAsync(CompoundDocument document, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a document by its tenant key (project:branch:pathHash) and file path.
    /// </summary>
    /// <param name="tenantKey">The composite tenant key.</param>
    /// <param name="filePath">The relative file path within the repository.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The document if found, null otherwise.</returns>
    Task<CompoundDocument?> GetByTenantKeyAsync(string tenantKey, string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a document by its unique ID.
    /// </summary>
    /// <param name="id">The document ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The document if found, null otherwise.</returns>
    Task<CompoundDocument?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs semantic search using the provided embedding vector.
    /// </summary>
    /// <param name="embedding">The query embedding vector (1024 dimensions).</param>
    /// <param name="tenantKey">The tenant key to filter results.</param>
    /// <param name="limit">Maximum number of results to return.</param>
    /// <param name="minRelevance">Minimum relevance score threshold (0.0-1.0).</param>
    /// <param name="docType">Optional document type filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of search results ordered by relevance descending.</returns>
    Task<IReadOnlyList<SearchResult>> SearchAsync(
        ReadOnlyMemory<float> embedding,
        string tenantKey,
        int limit = 10,
        float minRelevance = 0.0f,
        string? docType = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a document by its ID.
    /// </summary>
    /// <param name="id">The document ID to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the document was found and deleted, false otherwise.</returns>
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all documents for a tenant key.
    /// </summary>
    /// <param name="tenantKey">The tenant key whose documents to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of documents deleted.</returns>
    Task<int> DeleteByTenantKeyAsync(string tenantKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all documents for a tenant key.
    /// </summary>
    /// <param name="tenantKey">The tenant key to filter by.</param>
    /// <param name="docType">Optional document type filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of documents for the tenant.</returns>
    Task<IReadOnlyList<CompoundDocument>> GetAllForTenantAsync(
        string tenantKey,
        string? docType = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all chunks for a document.
    /// </summary>
    /// <param name="documentId">The parent document ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of chunks for the document.</returns>
    Task<IReadOnlyList<DocumentChunk>> GetChunksAsync(string documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts chunks for a document.
    /// Existing chunks with the same IDs will be updated.
    /// </summary>
    /// <param name="chunks">The chunks to upsert.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of chunks upserted.</returns>
    Task<int> UpsertChunksAsync(IEnumerable<DocumentChunk> chunks, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all chunks for a document.
    /// </summary>
    /// <param name="documentId">The parent document ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of chunks deleted.</returns>
    Task<int> DeleteChunksAsync(string documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches chunks using the provided embedding vector.
    /// </summary>
    /// <param name="embedding">The query embedding vector (1024 dimensions).</param>
    /// <param name="tenantKey">The tenant key to filter results.</param>
    /// <param name="limit">Maximum number of results to return.</param>
    /// <param name="minRelevance">Minimum relevance score threshold (0.0-1.0).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of chunk search results ordered by relevance descending.</returns>
    Task<IReadOnlyList<ChunkSearchResult>> SearchChunksAsync(
        ReadOnlyMemory<float> embedding,
        string tenantKey,
        int limit = 10,
        float minRelevance = 0.0f,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the promotion level for a document and its chunks.
    /// </summary>
    /// <param name="documentId">The document ID to update.</param>
    /// <param name="promotionLevel">The new promotion level.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the document was found and updated, false otherwise.</returns>
    Task<bool> UpdatePromotionLevelAsync(string documentId, string promotionLevel, CancellationToken cancellationToken = default);
}
