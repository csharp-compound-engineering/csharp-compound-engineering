# Phase 048: Document Repository Service

> **Status**: PLANNED
> **Category**: Database & Storage
> **Estimated Effort**: L
> **Prerequisites**: Phase 042 (Semantic Kernel PostgreSQL Connector), Phase 043 (Npgsql Data Source Setup)

---

## Spec References

- [mcp-server.md - IDocumentRepository](../spec/mcp-server.md#idocumentrepository)
- [mcp-server.md - Transaction Support Limitation](../spec/mcp-server.md#transaction-support-limitation)
- [mcp-server/database-schema.md - Semantic Kernel Model](../spec/mcp-server/database-schema.md#documents-schema-semantic-kernel-model)
- [mcp-server/database-schema.md - Document Chunks Schema](../spec/mcp-server/database-schema.md#document-chunks-schema-for-large-documents)
- [mcp-server/database-schema.md - Query Filtering](../spec/mcp-server/database-schema.md#query-filtering)
- [mcp-server/file-watcher.md - Sync on Activation](../spec/mcp-server/file-watcher.md#sync-on-activation-startup-reconciliation)
- [mcp-server/chunking.md - Chunk Lifecycle](../spec/mcp-server/chunking.md#chunk-lifecycle)
- [observability.md - Document Repository Logging](../spec/observability.md#document-repository)

---

## Objectives

1. Define `IDocumentRepository` interface with full CRUD operations
2. Define `IDocumentChunkRepository` interface for chunk management
3. Implement hybrid repository pattern: raw Npgsql for transactional writes, Semantic Kernel for vector search
4. Implement tenant-scoped queries using `TenantContext`
5. Implement bulk operations for reconciliation (batch upsert, orphan detection)
6. Implement atomic chunk management (create/update/delete chunks with parent document)
7. Add comprehensive logging following observability spec patterns
8. Register repositories in dependency injection container

---

## Acceptance Criteria

- [ ] `IDocumentRepository` interface defined with all CRUD methods
- [ ] `IDocumentChunkRepository` interface defined for chunk operations
- [ ] `TenantContext` record implemented for tenant isolation
- [ ] `SearchResult` record implemented with relevance score
- [ ] `NpgsqlDocumentRepository` implements transactional writes
- [ ] `SemanticKernelSearchRepository` implements vector search
- [ ] `GetByIdAsync` returns document by ID
- [ ] `GetByPathAsync` returns document by relative path within tenant context
- [ ] `SearchAsync` performs vector similarity search with tenant filtering
- [ ] `UpsertAsync` handles insert and update with atomic chunk operations
- [ ] `DeleteAsync` deletes document and all associated chunks atomically
- [ ] `DeleteByTenantAsync` deletes all documents for a tenant
- [ ] `GetAllForTenantAsync` supports reconciliation by returning all documents for tenant
- [ ] `GetOrphanedChunksAsync` detects chunks without valid parent documents
- [ ] Bulk operations support batch processing for reconciliation
- [ ] All operations include structured logging with correlation IDs
- [ ] Unit tests cover repository interfaces with mocked dependencies
- [ ] Integration tests verify actual PostgreSQL operations

---

## Implementation Notes

### 1. TenantContext Record

Encapsulates tenant isolation context:

```csharp
// src/CompoundDocs.Common/Data/TenantContext.cs
namespace CompoundDocs.Common.Data;

/// <summary>
/// Tenant isolation context for multi-tenant document storage.
/// Combines project name, branch name, and path hash to uniquely identify a tenant.
/// </summary>
/// <param name="ProjectName">Name of the project (e.g., "my-project")</param>
/// <param name="BranchName">Git branch name (e.g., "main", "feature/auth")</param>
/// <param name="PathHash">SHA256 hash of the absolute repo path (first 16 chars)</param>
public sealed record TenantContext(
    string ProjectName,
    string BranchName,
    string PathHash)
{
    /// <summary>
    /// Creates a tenant context from project activation information.
    /// </summary>
    public static TenantContext Create(string projectName, string branchName, string absolutePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectName);
        ArgumentException.ThrowIfNullOrWhiteSpace(branchName);
        ArgumentException.ThrowIfNullOrWhiteSpace(absolutePath);

        var pathHash = ComputePathHash(absolutePath);
        return new TenantContext(projectName, branchName, pathHash);
    }

    /// <summary>
    /// Computes a short hash of the absolute path for tenant isolation.
    /// </summary>
    private static string ComputePathHash(string absolutePath)
    {
        // Normalize path separators
        var normalizedPath = absolutePath.Replace('\\', '/').TrimEnd('/');

        // Compute SHA256
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedPath));

        // Return first 16 characters (64 bits) for brevity
        return Convert.ToHexString(hashBytes)[..16].ToLowerInvariant();
    }
}
```

### 2. SearchResult Record

Wraps search results with relevance scoring:

```csharp
// src/CompoundDocs.Common/Data/SearchResult.cs
namespace CompoundDocs.Common.Data;

/// <summary>
/// Search result containing a document and its relevance score.
/// </summary>
/// <param name="Document">The matched document.</param>
/// <param name="RelevanceScore">Cosine similarity score (0.0 to 1.0, higher is more relevant).</param>
public sealed record SearchResult(
    CompoundDocument Document,
    float RelevanceScore);

/// <summary>
/// Search result containing a chunk and its relevance score.
/// </summary>
/// <param name="Chunk">The matched chunk.</param>
/// <param name="RelevanceScore">Cosine similarity score (0.0 to 1.0, higher is more relevant).</param>
public sealed record ChunkSearchResult(
    DocumentChunk Chunk,
    float RelevanceScore);
```

### 3. IDocumentRepository Interface

```csharp
// src/CompoundDocs.Common/Data/IDocumentRepository.cs
namespace CompoundDocs.Common.Data;

/// <summary>
/// Repository abstraction for CompoundDocument CRUD operations and search.
/// Uses hybrid pattern: raw Npgsql for transactional writes, Semantic Kernel for vector search.
/// </summary>
public interface IDocumentRepository
{
    // --- Read Operations (Semantic Kernel) ---

    /// <summary>
    /// Gets a document by its unique ID.
    /// </summary>
    Task<CompoundDocument?> GetByIdAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// Gets a document by its relative path within a tenant context.
    /// </summary>
    Task<CompoundDocument?> GetByPathAsync(
        string relativePath,
        TenantContext tenant,
        CancellationToken ct = default);

    /// <summary>
    /// Performs vector similarity search within a tenant context.
    /// </summary>
    /// <param name="embedding">Query embedding vector (1024 dimensions).</param>
    /// <param name="limit">Maximum number of results to return.</param>
    /// <param name="minRelevance">Minimum relevance score threshold (0.0-1.0).</param>
    /// <param name="tenant">Tenant context for filtering.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of documents with relevance scores, ordered by relevance descending.</returns>
    Task<IReadOnlyList<SearchResult>> SearchAsync(
        ReadOnlyMemory<float> embedding,
        int limit,
        float minRelevance,
        TenantContext tenant,
        CancellationToken ct = default);

    /// <summary>
    /// Gets all documents for a tenant (used for reconciliation).
    /// </summary>
    Task<IReadOnlyList<CompoundDocument>> GetAllForTenantAsync(
        TenantContext tenant,
        CancellationToken ct = default);

    // --- Write Operations (Npgsql Transactional) ---

    /// <summary>
    /// Upserts a document and optionally its chunks in a single transaction.
    /// If the document has chunks, all existing chunks are deleted and new chunks are inserted.
    /// </summary>
    /// <param name="document">The document to upsert.</param>
    /// <param name="chunks">Optional chunks for chunked documents. Pass null or empty for non-chunked documents.</param>
    /// <param name="ct">Cancellation token.</param>
    Task UpsertAsync(
        CompoundDocument document,
        IReadOnlyList<DocumentChunk>? chunks = null,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes a document and all its associated chunks atomically.
    /// </summary>
    /// <param name="id">The document ID to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if document was deleted, false if not found.</returns>
    Task<bool> DeleteAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// Deletes all documents and chunks for a tenant.
    /// Used by delete_documents tool.
    /// </summary>
    /// <param name="tenant">Tenant context identifying documents to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Number of documents deleted.</returns>
    Task<int> DeleteByTenantAsync(TenantContext tenant, CancellationToken ct = default);

    // --- Bulk Operations (Reconciliation) ---

    /// <summary>
    /// Bulk upserts multiple documents with their chunks in a single transaction.
    /// Used during reconciliation for efficiency.
    /// </summary>
    /// <param name="documentsWithChunks">Documents paired with their chunks.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Number of documents upserted.</returns>
    Task<int> BulkUpsertAsync(
        IReadOnlyList<(CompoundDocument Document, IReadOnlyList<DocumentChunk>? Chunks)> documentsWithChunks,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes multiple documents by their IDs in a single transaction.
    /// Used during reconciliation to remove orphaned documents.
    /// </summary>
    /// <param name="ids">Document IDs to delete.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Number of documents deleted.</returns>
    Task<int> BulkDeleteAsync(IReadOnlyList<string> ids, CancellationToken ct = default);

    // --- Promotion Operations ---

    /// <summary>
    /// Updates the promotion level of a document and all its chunks atomically.
    /// </summary>
    /// <param name="id">Document ID to update.</param>
    /// <param name="promotionLevel">New promotion level.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if updated, false if document not found.</returns>
    Task<bool> UpdatePromotionLevelAsync(
        string id,
        string promotionLevel,
        CancellationToken ct = default);
}
```

### 4. IDocumentChunkRepository Interface

```csharp
// src/CompoundDocs.Common/Data/IDocumentChunkRepository.cs
namespace CompoundDocs.Common.Data;

/// <summary>
/// Repository abstraction for DocumentChunk operations.
/// Chunks are always managed in context of their parent document.
/// </summary>
public interface IDocumentChunkRepository
{
    // --- Read Operations ---

    /// <summary>
    /// Gets all chunks for a document.
    /// </summary>
    Task<IReadOnlyList<DocumentChunk>> GetByDocumentIdAsync(
        string documentId,
        CancellationToken ct = default);

    /// <summary>
    /// Performs vector similarity search across chunks within a tenant context.
    /// </summary>
    Task<IReadOnlyList<ChunkSearchResult>> SearchAsync(
        ReadOnlyMemory<float> embedding,
        int limit,
        float minRelevance,
        TenantContext tenant,
        CancellationToken ct = default);

    /// <summary>
    /// Gets orphaned chunks (chunks whose parent document no longer exists).
    /// Used during reconciliation cleanup.
    /// </summary>
    Task<IReadOnlyList<DocumentChunk>> GetOrphanedChunksAsync(
        TenantContext tenant,
        CancellationToken ct = default);

    // --- Write Operations (typically called within document transaction) ---

    /// <summary>
    /// Deletes all chunks for a document.
    /// </summary>
    Task DeleteByDocumentIdAsync(string documentId, CancellationToken ct = default);

    /// <summary>
    /// Deletes orphaned chunks identified by GetOrphanedChunksAsync.
    /// </summary>
    Task<int> DeleteOrphanedChunksAsync(
        TenantContext tenant,
        CancellationToken ct = default);
}
```

### 5. NpgsqlDocumentRepository Implementation

Implements transactional write operations using raw Npgsql:

```csharp
// src/CompoundDocs.McpServer/Data/NpgsqlDocumentRepository.cs
namespace CompoundDocs.McpServer.Data;

/// <summary>
/// Document repository implementation using raw Npgsql for transactional operations.
/// Provides atomic writes for documents and their associated chunks.
/// </summary>
public sealed class NpgsqlDocumentRepository : IDocumentRepository
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly IDocumentSearchRepository _searchRepository;
    private readonly ILogger<NpgsqlDocumentRepository> _logger;

    public NpgsqlDocumentRepository(
        NpgsqlDataSource dataSource,
        IDocumentSearchRepository searchRepository,
        ILogger<NpgsqlDocumentRepository> logger)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _searchRepository = searchRepository ?? throw new ArgumentNullException(nameof(searchRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // --- Read Operations (delegate to Semantic Kernel) ---

    public Task<CompoundDocument?> GetByIdAsync(string id, CancellationToken ct = default)
        => _searchRepository.GetByIdAsync(id, ct);

    public Task<CompoundDocument?> GetByPathAsync(string relativePath, TenantContext tenant, CancellationToken ct = default)
        => _searchRepository.GetByPathAsync(relativePath, tenant, ct);

    public Task<IReadOnlyList<SearchResult>> SearchAsync(
        ReadOnlyMemory<float> embedding, int limit, float minRelevance, TenantContext tenant, CancellationToken ct = default)
        => _searchRepository.SearchAsync(embedding, limit, minRelevance, tenant, ct);

    public Task<IReadOnlyList<CompoundDocument>> GetAllForTenantAsync(TenantContext tenant, CancellationToken ct = default)
        => _searchRepository.GetAllForTenantAsync(tenant, ct);

    // --- Write Operations (transactional with Npgsql) ---

    public async Task UpsertAsync(
        CompoundDocument document,
        IReadOnlyList<DocumentChunk>? chunks = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        try
        {
            _logger.LogDebug(
                "Upserting document: {Id} at {Path}",
                document.Id,
                document.RelativePath);

            // 1. Delete existing chunks for this document
            await DeleteChunksForDocumentAsync(connection, document.Id, ct);

            // 2. Upsert the document
            await UpsertDocumentCoreAsync(connection, document, ct);

            // 3. Insert new chunks if provided
            if (chunks is { Count: > 0 })
            {
                await InsertChunksAsync(connection, chunks, ct);
                _logger.LogDebug(
                    "Inserted {ChunkCount} chunks for document {Id}",
                    chunks.Count,
                    document.Id);
            }

            await transaction.CommitAsync(ct);

            _logger.LogInformation(
                "Document upserted: {Path} ({CharCount} chars, {ChunkCount} chunks)",
                document.RelativePath,
                document.CharCount,
                chunks?.Count ?? 0);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogError(ex, "Failed to upsert document {Id}", document.Id);
            throw;
        }
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        try
        {
            _logger.LogDebug("Deleting document: {Id}", id);

            // 1. Delete chunks first (foreign key constraint)
            var chunksDeleted = await DeleteChunksForDocumentAsync(connection, id, ct);

            // 2. Delete the document
            var sql = "DELETE FROM documents WHERE id = @id";
            await using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("id", id);
            var rowsAffected = await cmd.ExecuteNonQueryAsync(ct);

            await transaction.CommitAsync(ct);

            if (rowsAffected > 0)
            {
                _logger.LogInformation(
                    "Document deleted: {Id} (with {ChunkCount} chunks)",
                    id,
                    chunksDeleted);
                return true;
            }

            _logger.LogDebug("Document not found for deletion: {Id}", id);
            return false;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogError(ex, "Failed to delete document {Id}", id);
            throw;
        }
    }

    public async Task<int> DeleteByTenantAsync(TenantContext tenant, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(tenant);

        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        try
        {
            _logger.LogDebug(
                "Deleting all documents for tenant: {ProjectName}/{BranchName}",
                tenant.ProjectName,
                tenant.BranchName);

            // 1. Delete chunks for all documents in tenant
            var chunksSql = @"
                DELETE FROM document_chunks
                WHERE project_name = @projectName
                  AND branch_name = @branchName
                  AND path_hash = @pathHash";

            await using (var chunksCmd = new NpgsqlCommand(chunksSql, connection))
            {
                chunksCmd.Parameters.AddWithValue("projectName", tenant.ProjectName);
                chunksCmd.Parameters.AddWithValue("branchName", tenant.BranchName);
                chunksCmd.Parameters.AddWithValue("pathHash", tenant.PathHash);
                await chunksCmd.ExecuteNonQueryAsync(ct);
            }

            // 2. Delete documents
            var docsSql = @"
                DELETE FROM documents
                WHERE project_name = @projectName
                  AND branch_name = @branchName
                  AND path_hash = @pathHash";

            await using var docsCmd = new NpgsqlCommand(docsSql, connection);
            docsCmd.Parameters.AddWithValue("projectName", tenant.ProjectName);
            docsCmd.Parameters.AddWithValue("branchName", tenant.BranchName);
            docsCmd.Parameters.AddWithValue("pathHash", tenant.PathHash);
            var docsDeleted = await docsCmd.ExecuteNonQueryAsync(ct);

            await transaction.CommitAsync(ct);

            _logger.LogInformation(
                "Deleted {Count} documents for tenant: {ProjectName}/{BranchName}",
                docsDeleted,
                tenant.ProjectName,
                tenant.BranchName);

            return docsDeleted;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogError(
                ex,
                "Failed to delete documents for tenant {ProjectName}/{BranchName}",
                tenant.ProjectName,
                tenant.BranchName);
            throw;
        }
    }

    public async Task<int> BulkUpsertAsync(
        IReadOnlyList<(CompoundDocument Document, IReadOnlyList<DocumentChunk>? Chunks)> documentsWithChunks,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(documentsWithChunks);

        if (documentsWithChunks.Count == 0)
        {
            return 0;
        }

        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        try
        {
            _logger.LogDebug("Bulk upserting {Count} documents", documentsWithChunks.Count);

            var upsertedCount = 0;
            foreach (var (document, chunks) in documentsWithChunks)
            {
                await DeleteChunksForDocumentAsync(connection, document.Id, ct);
                await UpsertDocumentCoreAsync(connection, document, ct);

                if (chunks is { Count: > 0 })
                {
                    await InsertChunksAsync(connection, chunks, ct);
                }

                upsertedCount++;
            }

            await transaction.CommitAsync(ct);

            _logger.LogInformation("Bulk upserted {Count} documents", upsertedCount);
            return upsertedCount;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogError(ex, "Failed to bulk upsert documents");
            throw;
        }
    }

    public async Task<int> BulkDeleteAsync(IReadOnlyList<string> ids, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        if (ids.Count == 0)
        {
            return 0;
        }

        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        try
        {
            _logger.LogDebug("Bulk deleting {Count} documents", ids.Count);

            // Delete chunks first
            var chunksSql = "DELETE FROM document_chunks WHERE document_id = ANY(@ids)";
            await using (var chunksCmd = new NpgsqlCommand(chunksSql, connection))
            {
                chunksCmd.Parameters.AddWithValue("ids", ids.ToArray());
                await chunksCmd.ExecuteNonQueryAsync(ct);
            }

            // Delete documents
            var docsSql = "DELETE FROM documents WHERE id = ANY(@ids)";
            await using var docsCmd = new NpgsqlCommand(docsSql, connection);
            docsCmd.Parameters.AddWithValue("ids", ids.ToArray());
            var deletedCount = await docsCmd.ExecuteNonQueryAsync(ct);

            await transaction.CommitAsync(ct);

            _logger.LogInformation("Bulk deleted {Count} documents", deletedCount);
            return deletedCount;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogError(ex, "Failed to bulk delete documents");
            throw;
        }
    }

    public async Task<bool> UpdatePromotionLevelAsync(
        string id,
        string promotionLevel,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentException.ThrowIfNullOrWhiteSpace(promotionLevel);

        await using var connection = await _dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);

        try
        {
            _logger.LogDebug(
                "Updating promotion level for document {Id} to {Level}",
                id,
                promotionLevel);

            // Update document
            var docSql = "UPDATE documents SET promotion_level = @level WHERE id = @id";
            await using (var docCmd = new NpgsqlCommand(docSql, connection))
            {
                docCmd.Parameters.AddWithValue("level", promotionLevel);
                docCmd.Parameters.AddWithValue("id", id);
                var rowsAffected = await docCmd.ExecuteNonQueryAsync(ct);

                if (rowsAffected == 0)
                {
                    _logger.LogDebug("Document not found: {Id}", id);
                    return false;
                }
            }

            // Update all chunks atomically
            var chunksSql = "UPDATE document_chunks SET promotion_level = @level WHERE document_id = @id";
            await using (var chunksCmd = new NpgsqlCommand(chunksSql, connection))
            {
                chunksCmd.Parameters.AddWithValue("level", promotionLevel);
                chunksCmd.Parameters.AddWithValue("id", id);
                var chunksUpdated = await chunksCmd.ExecuteNonQueryAsync(ct);

                _logger.LogDebug(
                    "Updated {ChunkCount} chunks for document {Id}",
                    chunksUpdated,
                    id);
            }

            await transaction.CommitAsync(ct);

            _logger.LogInformation(
                "Promotion level updated: {Id} -> {Level}",
                id,
                promotionLevel);

            return true;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogError(ex, "Failed to update promotion level for {Id}", id);
            throw;
        }
    }

    // --- Private Helper Methods ---

    private async Task UpsertDocumentCoreAsync(
        NpgsqlConnection connection,
        CompoundDocument document,
        CancellationToken ct)
    {
        var sql = @"
            INSERT INTO documents (
                id, project_name, branch_name, path_hash, relative_path,
                title, summary, doc_type, promotion_level, content_hash,
                char_count, frontmatter_json, embedding
            ) VALUES (
                @id, @projectName, @branchName, @pathHash, @relativePath,
                @title, @summary, @docType, @promotionLevel, @contentHash,
                @charCount, @frontmatterJson, @embedding
            )
            ON CONFLICT (id) DO UPDATE SET
                project_name = EXCLUDED.project_name,
                branch_name = EXCLUDED.branch_name,
                path_hash = EXCLUDED.path_hash,
                relative_path = EXCLUDED.relative_path,
                title = EXCLUDED.title,
                summary = EXCLUDED.summary,
                doc_type = EXCLUDED.doc_type,
                promotion_level = EXCLUDED.promotion_level,
                content_hash = EXCLUDED.content_hash,
                char_count = EXCLUDED.char_count,
                frontmatter_json = EXCLUDED.frontmatter_json,
                embedding = EXCLUDED.embedding";

        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("id", document.Id);
        cmd.Parameters.AddWithValue("projectName", document.ProjectName);
        cmd.Parameters.AddWithValue("branchName", document.BranchName);
        cmd.Parameters.AddWithValue("pathHash", document.PathHash);
        cmd.Parameters.AddWithValue("relativePath", document.RelativePath);
        cmd.Parameters.AddWithValue("title", document.Title);
        cmd.Parameters.AddWithValue("summary", (object?)document.Summary ?? DBNull.Value);
        cmd.Parameters.AddWithValue("docType", document.DocType);
        cmd.Parameters.AddWithValue("promotionLevel", document.PromotionLevel);
        cmd.Parameters.AddWithValue("contentHash", document.ContentHash);
        cmd.Parameters.AddWithValue("charCount", document.CharCount);
        cmd.Parameters.AddWithValue("frontmatterJson", (object?)document.FrontmatterJson ?? DBNull.Value);
        cmd.Parameters.AddWithValue("embedding", document.Embedding?.ToArray() ?? (object)DBNull.Value);

        await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task<int> DeleteChunksForDocumentAsync(
        NpgsqlConnection connection,
        string documentId,
        CancellationToken ct)
    {
        var sql = "DELETE FROM document_chunks WHERE document_id = @documentId";
        await using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("documentId", documentId);
        return await cmd.ExecuteNonQueryAsync(ct);
    }

    private async Task InsertChunksAsync(
        NpgsqlConnection connection,
        IReadOnlyList<DocumentChunk> chunks,
        CancellationToken ct)
    {
        var sql = @"
            INSERT INTO document_chunks (
                id, document_id, project_name, branch_name, path_hash,
                promotion_level, chunk_index, header_path, content, embedding
            ) VALUES (
                @id, @documentId, @projectName, @branchName, @pathHash,
                @promotionLevel, @chunkIndex, @headerPath, @content, @embedding
            )";

        foreach (var chunk in chunks)
        {
            await using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("id", chunk.Id);
            cmd.Parameters.AddWithValue("documentId", chunk.DocumentId);
            cmd.Parameters.AddWithValue("projectName", chunk.ProjectName);
            cmd.Parameters.AddWithValue("branchName", chunk.BranchName);
            cmd.Parameters.AddWithValue("pathHash", chunk.PathHash);
            cmd.Parameters.AddWithValue("promotionLevel", chunk.PromotionLevel);
            cmd.Parameters.AddWithValue("chunkIndex", chunk.ChunkIndex);
            cmd.Parameters.AddWithValue("headerPath", chunk.HeaderPath);
            cmd.Parameters.AddWithValue("content", chunk.Content);
            cmd.Parameters.AddWithValue("embedding", chunk.Embedding?.ToArray() ?? (object)DBNull.Value);

            await cmd.ExecuteNonQueryAsync(ct);
        }
    }
}
```

### 6. SemanticKernelSearchRepository Implementation

Implements vector search using Semantic Kernel:

```csharp
// src/CompoundDocs.McpServer/Data/SemanticKernelSearchRepository.cs
#pragma warning disable SKEXP0001 // Vector store types are experimental

namespace CompoundDocs.McpServer.Data;

/// <summary>
/// Search repository implementation using Semantic Kernel's PostgreSQL connector.
/// Provides vector similarity search with tenant filtering.
/// </summary>
public sealed class SemanticKernelSearchRepository : IDocumentSearchRepository
{
    private readonly PostgresCollection<string, CompoundDocument> _documentsCollection;
    private readonly PostgresCollection<string, DocumentChunk> _chunksCollection;
    private readonly ILogger<SemanticKernelSearchRepository> _logger;

    public SemanticKernelSearchRepository(
        PostgresCollection<string, CompoundDocument> documentsCollection,
        PostgresCollection<string, DocumentChunk> chunksCollection,
        ILogger<SemanticKernelSearchRepository> logger)
    {
        _documentsCollection = documentsCollection;
        _chunksCollection = chunksCollection;
        _logger = logger;
    }

    public async Task<CompoundDocument?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        _logger.LogDebug("Getting document by ID: {Id}", id);

        return await _documentsCollection.GetAsync(id, cancellationToken: ct);
    }

    public async Task<CompoundDocument?> GetByPathAsync(
        string relativePath,
        TenantContext tenant,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentNullException.ThrowIfNull(tenant);

        _logger.LogDebug(
            "Getting document by path: {Path} in tenant {ProjectName}/{BranchName}",
            relativePath,
            tenant.ProjectName,
            tenant.BranchName);

        var filter = new VectorSearchFilter()
            .EqualTo("project_name", tenant.ProjectName)
            .EqualTo("branch_name", tenant.BranchName)
            .EqualTo("path_hash", tenant.PathHash)
            .EqualTo("relative_path", relativePath);

        // Use a dummy embedding for filtered retrieval (we only care about the filter)
        var dummyEmbedding = new float[1024];
        var results = await _documentsCollection
            .VectorizedSearchAsync(dummyEmbedding, new VectorSearchOptions
            {
                Filter = filter,
                Top = 1
            }, ct)
            .ToListAsync(ct);

        return results.FirstOrDefault()?.Record;
    }

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(
        ReadOnlyMemory<float> embedding,
        int limit,
        float minRelevance,
        TenantContext tenant,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(tenant);

        _logger.LogDebug(
            "Searching documents: limit={Limit}, minRelevance={MinRelevance}, tenant={ProjectName}/{BranchName}",
            limit,
            minRelevance,
            tenant.ProjectName,
            tenant.BranchName);

        var filter = new VectorSearchFilter()
            .EqualTo("project_name", tenant.ProjectName)
            .EqualTo("branch_name", tenant.BranchName)
            .EqualTo("path_hash", tenant.PathHash);

        var searchResults = await _documentsCollection
            .VectorizedSearchAsync(embedding.ToArray(), new VectorSearchOptions
            {
                Filter = filter,
                Top = limit,
                IncludeVectors = false
            }, ct)
            .ToListAsync(ct);

        var results = searchResults
            .Where(r => r.Score >= minRelevance)
            .Select(r => new SearchResult(r.Record, (float)r.Score))
            .ToList();

        _logger.LogInformation(
            "Search found {Count} results above threshold {Threshold}",
            results.Count,
            minRelevance);

        return results;
    }

    public async Task<IReadOnlyList<CompoundDocument>> GetAllForTenantAsync(
        TenantContext tenant,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(tenant);

        _logger.LogDebug(
            "Getting all documents for tenant: {ProjectName}/{BranchName}",
            tenant.ProjectName,
            tenant.BranchName);

        var filter = new VectorSearchFilter()
            .EqualTo("project_name", tenant.ProjectName)
            .EqualTo("branch_name", tenant.BranchName)
            .EqualTo("path_hash", tenant.PathHash);

        // Retrieve with large limit for reconciliation
        var dummyEmbedding = new float[1024];
        var results = await _documentsCollection
            .VectorizedSearchAsync(dummyEmbedding, new VectorSearchOptions
            {
                Filter = filter,
                Top = 10000, // Large limit for full tenant retrieval
                IncludeVectors = false
            }, ct)
            .Select(r => r.Record)
            .ToListAsync(ct);

        _logger.LogDebug(
            "Found {Count} documents for tenant {ProjectName}/{BranchName}",
            results.Count,
            tenant.ProjectName,
            tenant.BranchName);

        return results;
    }
}
```

### 7. IDocumentSearchRepository Interface

Internal interface for search operations:

```csharp
// src/CompoundDocs.McpServer/Data/IDocumentSearchRepository.cs
namespace CompoundDocs.McpServer.Data;

/// <summary>
/// Internal interface for Semantic Kernel-based search operations.
/// </summary>
internal interface IDocumentSearchRepository
{
    Task<CompoundDocument?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<CompoundDocument?> GetByPathAsync(string relativePath, TenantContext tenant, CancellationToken ct = default);
    Task<IReadOnlyList<SearchResult>> SearchAsync(
        ReadOnlyMemory<float> embedding, int limit, float minRelevance, TenantContext tenant, CancellationToken ct = default);
    Task<IReadOnlyList<CompoundDocument>> GetAllForTenantAsync(TenantContext tenant, CancellationToken ct = default);
}
```

### 8. Service Registration

```csharp
// In ServiceCollectionExtensions.cs
public static IServiceCollection AddDocumentRepository(
    this IServiceCollection services,
    string connectionString)
{
    // Build Npgsql data source with pgvector support
    var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
    dataSourceBuilder.UseVector();
    var dataSource = dataSourceBuilder.Build();

    services.AddSingleton(dataSource);

    // Semantic Kernel collections for search
    services.AddSingleton(sp =>
    {
        var ds = sp.GetRequiredService<NpgsqlDataSource>();
        return new PostgresCollection<string, CompoundDocument>(ds, "documents", ownsDataSource: false);
    });

    services.AddSingleton(sp =>
    {
        var ds = sp.GetRequiredService<NpgsqlDataSource>();
        return new PostgresCollection<string, DocumentChunk>(ds, "document_chunks", ownsDataSource: false);
    });

    // Repository registrations
    services.AddSingleton<IDocumentSearchRepository, SemanticKernelSearchRepository>();
    services.AddSingleton<IDocumentRepository, NpgsqlDocumentRepository>();
    services.AddSingleton<IDocumentChunkRepository, NpgsqlDocumentChunkRepository>();

    return services;
}
```

---

## Dependencies

### Depends On

- **Phase 042**: Semantic Kernel PostgreSQL Connector - `PostgresCollection<TKey, TRecord>` setup
- **Phase 043**: Npgsql Data Source Setup - `NpgsqlDataSource` configuration with pgvector

### Blocks

- **Phase 049+**: File Watcher Service - Needs repository for document upsert/delete
- **Phase 050+**: Indexing Service - Needs repository for storing indexed documents
- **Phase 051+**: MCP Tools Implementation - Tools use repository for CRUD operations
- **Phase 052+**: Reconciliation Service - Uses bulk operations for startup sync

---

## Testing Verification

### Unit Tests

```csharp
// tests/CompoundDocs.Tests/Data/TenantContextTests.cs
public class TenantContextTests
{
    [Fact]
    public void Create_GeneratesConsistentPathHash()
    {
        // Arrange
        var path = "/Users/dev/project";

        // Act
        var context1 = TenantContext.Create("project", "main", path);
        var context2 = TenantContext.Create("project", "main", path);

        // Assert
        Assert.Equal(context1.PathHash, context2.PathHash);
        Assert.Equal(16, context1.PathHash.Length);
    }

    [Fact]
    public void Create_NormalizesPathSeparators()
    {
        // Arrange
        var unixPath = "/Users/dev/project";
        var windowsPath = "\\Users\\dev\\project";

        // Act
        var unixContext = TenantContext.Create("project", "main", unixPath);
        var windowsContext = TenantContext.Create("project", "main", windowsPath);

        // Assert
        Assert.Equal(unixContext.PathHash, windowsContext.PathHash);
    }
}

// tests/CompoundDocs.Tests/Data/NpgsqlDocumentRepositoryTests.cs
public class NpgsqlDocumentRepositoryTests
{
    [Fact]
    public async Task UpsertAsync_WithChunks_InsertsAllRecords()
    {
        // Arrange
        var mockDataSource = CreateMockDataSource();
        var mockSearchRepo = new Mock<IDocumentSearchRepository>();
        var repo = new NpgsqlDocumentRepository(
            mockDataSource,
            mockSearchRepo.Object,
            Mock.Of<ILogger<NpgsqlDocumentRepository>>());

        var document = CreateTestDocument();
        var chunks = CreateTestChunks(document.Id, 3);

        // Act
        await repo.UpsertAsync(document, chunks);

        // Assert - verify transaction was committed with all records
        // (implementation depends on mocking strategy)
    }

    [Fact]
    public async Task DeleteAsync_RemovesDocumentAndChunks()
    {
        // Arrange
        var mockDataSource = CreateMockDataSource();
        var mockSearchRepo = new Mock<IDocumentSearchRepository>();
        var repo = new NpgsqlDocumentRepository(
            mockDataSource,
            mockSearchRepo.Object,
            Mock.Of<ILogger<NpgsqlDocumentRepository>>());

        // Act
        var result = await repo.DeleteAsync("test-id");

        // Assert
        // Verify both chunks and document were deleted
    }

    [Fact]
    public async Task UpdatePromotionLevelAsync_UpdatesDocumentAndChunks()
    {
        // Verify atomic update of document and all associated chunks
    }
}
```

### Integration Tests

```csharp
// tests/CompoundDocs.IntegrationTests/Data/DocumentRepositoryIntegrationTests.cs
[Trait("Category", "Integration")]
public class DocumentRepositoryIntegrationTests : IClassFixture<PostgresFixture>
{
    private readonly IDocumentRepository _repository;
    private readonly TenantContext _tenant;

    public DocumentRepositoryIntegrationTests(PostgresFixture fixture)
    {
        _repository = fixture.GetService<IDocumentRepository>();
        _tenant = TenantContext.Create("test-project", "main", "/test/path");
    }

    [Fact]
    public async Task UpsertAndSearch_ReturnsDocument()
    {
        // Arrange
        var document = CreateTestDocument(_tenant);
        var embedding = new float[1024];
        new Random(42).NextBytes(MemoryMarshal.AsBytes(embedding.AsSpan()));

        // Act
        await _repository.UpsertAsync(document with { Embedding = embedding });
        var results = await _repository.SearchAsync(
            embedding,
            limit: 10,
            minRelevance: 0.5f,
            _tenant);

        // Assert
        Assert.Contains(results, r => r.Document.Id == document.Id);
    }

    [Fact]
    public async Task DeleteByTenant_RemovesAllDocuments()
    {
        // Arrange
        var doc1 = CreateTestDocument(_tenant, "doc1.md");
        var doc2 = CreateTestDocument(_tenant, "doc2.md");
        await _repository.UpsertAsync(doc1);
        await _repository.UpsertAsync(doc2);

        // Act
        var deleted = await _repository.DeleteByTenantAsync(_tenant);

        // Assert
        Assert.Equal(2, deleted);
        var remaining = await _repository.GetAllForTenantAsync(_tenant);
        Assert.Empty(remaining);
    }

    [Fact]
    public async Task BulkUpsert_ProcessesAllDocuments()
    {
        // Arrange
        var documents = Enumerable.Range(1, 10)
            .Select(i => CreateTestDocument(_tenant, $"doc{i}.md"))
            .Select(d => (d, (IReadOnlyList<DocumentChunk>?)null))
            .ToList();

        // Act
        var count = await _repository.BulkUpsertAsync(documents);

        // Assert
        Assert.Equal(10, count);
    }
}
```

---

## Files Created/Modified

| File | Action | Description |
|------|--------|-------------|
| `src/CompoundDocs.Common/Data/TenantContext.cs` | Create | Tenant isolation record |
| `src/CompoundDocs.Common/Data/SearchResult.cs` | Create | Search result records |
| `src/CompoundDocs.Common/Data/IDocumentRepository.cs` | Create | Main repository interface |
| `src/CompoundDocs.Common/Data/IDocumentChunkRepository.cs` | Create | Chunk repository interface |
| `src/CompoundDocs.McpServer/Data/IDocumentSearchRepository.cs` | Create | Internal search interface |
| `src/CompoundDocs.McpServer/Data/NpgsqlDocumentRepository.cs` | Create | Transactional write implementation |
| `src/CompoundDocs.McpServer/Data/NpgsqlDocumentChunkRepository.cs` | Create | Chunk repository implementation |
| `src/CompoundDocs.McpServer/Data/SemanticKernelSearchRepository.cs` | Create | Vector search implementation |
| `src/CompoundDocs.McpServer/Extensions/ServiceCollectionExtensions.cs` | Modify | Add repository registration |
| `tests/CompoundDocs.Tests/Data/TenantContextTests.cs` | Create | TenantContext unit tests |
| `tests/CompoundDocs.Tests/Data/NpgsqlDocumentRepositoryTests.cs` | Create | Repository unit tests |
| `tests/CompoundDocs.IntegrationTests/Data/DocumentRepositoryIntegrationTests.cs` | Create | Integration tests |

---

## Logging Reference

Following the observability spec patterns:

| Operation | Log Level | Message Template |
|-----------|-----------|------------------|
| Get by ID | Debug | `"Getting document by ID: {Id}"` |
| Get by path | Debug | `"Getting document by path: {Path} in tenant {ProjectName}/{BranchName}"` |
| Upsert start | Debug | `"Upserting document: {Id} at {Path}"` |
| Upsert success | Information | `"Document upserted: {Path} ({CharCount} chars, {ChunkCount} chunks)"` |
| Search complete | Information | `"Search found {Count} results above threshold {Threshold}"` |
| Delete success | Information | `"Document deleted: {Id} (with {ChunkCount} chunks)"` |
| Bulk upsert | Information | `"Bulk upserted {Count} documents"` |
| Promotion update | Information | `"Promotion level updated: {Id} -> {Level}"` |
| Failure | Error | Operation-specific with exception |

---

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| Transaction deadlocks | Use consistent lock ordering (chunks before documents) |
| Large bulk operations timeout | Process in batches of 100 documents |
| Vector search performance | Ensure HNSW index exists, set ef_search parameter |
| Orphaned chunks | GetOrphanedChunksAsync for reconciliation cleanup |
| Connection pool exhaustion | Use single NpgsqlDataSource with appropriate pool settings |
| Memory pressure on large results | Stream results where possible, limit batch sizes |
