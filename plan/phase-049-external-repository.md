# Phase 049: External Document Repository Service

> **Status**: PLANNED
> **Category**: Database & Storage
> **Estimated Effort**: M
> **Prerequisites**: Phase 044 (Document Repository Interface), Phase 045 (Document Repository Implementation)

---

## Spec References

- [mcp-server/tools.md - search_external_docs](../spec/mcp-server/tools.md#5-search-external-docs-tool)
- [mcp-server/tools.md - rag_query_external](../spec/mcp-server/tools.md#6-rag-query-external-docs-tool)
- [configuration.md - external_docs configuration](../spec/configuration.md#external-documentation-optional)
- [mcp-server/database-schema.md - External Documents Schema](../spec/mcp-server/database-schema.md#external-documents-schema-separate-collection)

---

## Objectives

1. Create `IExternalDocumentRepository` interface for external doc operations
2. Implement read-only operations (no create/update except sync)
3. Support path-based queries with glob pattern matching
4. Manage separate vector collections for external docs and chunks
5. Implement sync operations for external doc paths
6. Provide integration with project configuration for external_docs settings

---

## Acceptance Criteria

- [ ] `IExternalDocumentRepository` interface defined with read-only semantics
- [ ] `ExternalDocumentRepository` implementation using Semantic Kernel PostgreSQL connector
- [ ] Separate `external_documents` collection management (distinct from `documents`)
- [ ] Separate `external_document_chunks` collection management
- [ ] Path-based queries with include/exclude glob pattern support
- [ ] Sync operations that respect `external_docs.include_patterns` and `external_docs.exclude_patterns`
- [ ] Content hash-based change detection for incremental sync
- [ ] Bulk delete operations for full re-sync scenarios
- [ ] Unit tests with mock vector store
- [ ] Integration tests with real PostgreSQL/pgvector

---

## Implementation Notes

### 1. IExternalDocumentRepository Interface

Create an interface for external document operations with read-only semantics:

```csharp
// src/CompoundDocs.Common/Repositories/IExternalDocumentRepository.cs
namespace CompoundDocs.Common.Repositories;

/// <summary>
/// Repository for external project documentation.
/// External docs are read-only reference material indexed separately from compounding docs.
/// </summary>
public interface IExternalDocumentRepository
{
    /// <summary>
    /// Ensures the external documents and chunks collections exist.
    /// </summary>
    Task EnsureCollectionsExistAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an external document by its ID.
    /// </summary>
    Task<ExternalDocument?> GetByIdAsync(
        string id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an external document by its relative path within the tenant context.
    /// </summary>
    Task<ExternalDocument?> GetByPathAsync(
        string projectName,
        string branchName,
        string pathHash,
        string relativePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs semantic search against external documents.
    /// </summary>
    Task<IReadOnlyList<ExternalDocumentSearchResult>> SearchAsync(
        ReadOnlyMemory<float> queryEmbedding,
        string projectName,
        string branchName,
        string pathHash,
        int limit = 10,
        float minRelevanceScore = 0.7f,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all external documents for a tenant context.
    /// Used for sync operations to determine what needs updating.
    /// </summary>
    Task<IReadOnlyList<ExternalDocumentSyncInfo>> GetAllForTenantAsync(
        string projectName,
        string branchName,
        string pathHash,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Syncs external documents from the file system.
    /// Only called during project activation or when external_docs path changes.
    /// </summary>
    Task<ExternalDocsSyncResult> SyncFromFileSystemAsync(
        string projectName,
        string branchName,
        string pathHash,
        string externalDocsPath,
        IReadOnlyList<string> includePatterns,
        IReadOnlyList<string> excludePatterns,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts an external document (used internally by sync).
    /// </summary>
    Task UpsertAsync(
        ExternalDocument document,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts external document chunks (used internally by sync).
    /// </summary>
    Task UpsertChunksAsync(
        IReadOnlyList<ExternalDocumentChunk> chunks,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an external document and its chunks by ID.
    /// </summary>
    Task DeleteAsync(
        string id,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all external documents and chunks for a tenant context.
    /// Used when external_docs configuration changes.
    /// </summary>
    Task DeleteAllForTenantAsync(
        string projectName,
        string branchName,
        string pathHash,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets chunks for a specific external document.
    /// </summary>
    Task<IReadOnlyList<ExternalDocumentChunk>> GetChunksByDocumentIdAsync(
        string externalDocumentId,
        CancellationToken cancellationToken = default);
}
```

### 2. Supporting Types

```csharp
// src/CompoundDocs.Common/Models/ExternalDocumentSearchResult.cs
namespace CompoundDocs.Common.Models;

/// <summary>
/// Result from external document semantic search.
/// </summary>
public record ExternalDocumentSearchResult(
    ExternalDocument Document,
    float RelevanceScore);

// src/CompoundDocs.Common/Models/ExternalDocumentSyncInfo.cs
namespace CompoundDocs.Common.Models;

/// <summary>
/// Lightweight info for sync comparison (avoids loading full embedding).
/// </summary>
public record ExternalDocumentSyncInfo(
    string Id,
    string RelativePath,
    string ContentHash);

// src/CompoundDocs.Common/Models/ExternalDocsSyncResult.cs
namespace CompoundDocs.Common.Models;

/// <summary>
/// Result of external docs sync operation.
/// </summary>
public record ExternalDocsSyncResult(
    int Added,
    int Updated,
    int Deleted,
    int Unchanged,
    int Errors,
    IReadOnlyList<string> ErrorPaths);
```

### 3. ExternalDocumentRepository Implementation

```csharp
// src/CompoundDocs.McpServer/Repositories/ExternalDocumentRepository.cs
using CompoundDocs.Common.Models;
using CompoundDocs.Common.Repositories;
using CompoundDocs.Common.Services;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Data;
using Npgsql;

namespace CompoundDocs.McpServer.Repositories;

/// <summary>
/// Repository for external project documentation using Semantic Kernel PostgreSQL connector.
/// External docs are stored in separate collections from compounding docs.
/// </summary>
public sealed class ExternalDocumentRepository : IExternalDocumentRepository
{
    private readonly PostgresCollection<string, ExternalDocument> _documentsCollection;
    private readonly PostgresCollection<string, ExternalDocumentChunk> _chunksCollection;
    private readonly IEmbeddingService _embeddingService;
    private readonly IDocumentChunker _chunker;
    private readonly ILogger<ExternalDocumentRepository> _logger;

    public ExternalDocumentRepository(
        NpgsqlDataSource dataSource,
        IEmbeddingService embeddingService,
        IDocumentChunker chunker,
        ILogger<ExternalDocumentRepository> logger)
    {
        _documentsCollection = new PostgresCollection<string, ExternalDocument>(
            dataSource,
            "external_documents",
            ownsDataSource: false);

        _chunksCollection = new PostgresCollection<string, ExternalDocumentChunk>(
            dataSource,
            "external_document_chunks",
            ownsDataSource: false);

        _embeddingService = embeddingService;
        _chunker = chunker;
        _logger = logger;
    }

    public async Task EnsureCollectionsExistAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Ensuring external document collections exist");

        await _documentsCollection.EnsureCollectionExistsAsync(cancellationToken);
        await _chunksCollection.EnsureCollectionExistsAsync(cancellationToken);

        _logger.LogInformation("External document collections verified");
    }

    public async Task<ExternalDocument?> GetByIdAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        return await _documentsCollection.GetAsync(id, cancellationToken: cancellationToken);
    }

    public async Task<ExternalDocument?> GetByPathAsync(
        string projectName,
        string branchName,
        string pathHash,
        string relativePath,
        CancellationToken cancellationToken = default)
    {
        var filter = new VectorSearchFilter()
            .EqualTo("project_name", projectName)
            .EqualTo("branch_name", branchName)
            .EqualTo("path_hash", pathHash);

        // Use a dummy embedding to search with path filter
        // This is a workaround since we need exact path match
        await foreach (var doc in _documentsCollection.GetAsync(filter, cancellationToken: cancellationToken))
        {
            if (doc.RelativePath == relativePath)
            {
                return doc;
            }
        }

        return null;
    }

    public async Task<IReadOnlyList<ExternalDocumentSearchResult>> SearchAsync(
        ReadOnlyMemory<float> queryEmbedding,
        string projectName,
        string branchName,
        string pathHash,
        int limit = 10,
        float minRelevanceScore = 0.7f,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Searching external docs for tenant {Project}/{Branch}/{PathHash} with limit {Limit}",
            projectName, branchName, pathHash, limit);

        var filter = new VectorSearchFilter()
            .EqualTo("project_name", projectName)
            .EqualTo("branch_name", branchName)
            .EqualTo("path_hash", pathHash);

        var results = new List<ExternalDocumentSearchResult>();

        await foreach (var result in _documentsCollection.SearchAsync(
            queryEmbedding,
            top: limit,
            filter: filter,
            cancellationToken: cancellationToken))
        {
            if (result.Score >= minRelevanceScore)
            {
                results.Add(new ExternalDocumentSearchResult(
                    result.Record,
                    (float)result.Score));
            }
        }

        _logger.LogDebug("Found {Count} external docs matching search criteria", results.Count);

        return results;
    }

    public async Task<IReadOnlyList<ExternalDocumentSyncInfo>> GetAllForTenantAsync(
        string projectName,
        string branchName,
        string pathHash,
        CancellationToken cancellationToken = default)
    {
        var filter = new VectorSearchFilter()
            .EqualTo("project_name", projectName)
            .EqualTo("branch_name", branchName)
            .EqualTo("path_hash", pathHash);

        var results = new List<ExternalDocumentSyncInfo>();

        await foreach (var doc in _documentsCollection.GetAsync(filter, cancellationToken: cancellationToken))
        {
            results.Add(new ExternalDocumentSyncInfo(
                doc.Id,
                doc.RelativePath,
                doc.ContentHash));
        }

        return results;
    }

    public async Task<ExternalDocsSyncResult> SyncFromFileSystemAsync(
        string projectName,
        string branchName,
        string pathHash,
        string externalDocsPath,
        IReadOnlyList<string> includePatterns,
        IReadOnlyList<string> excludePatterns,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Starting external docs sync from {Path} for tenant {Project}/{Branch}",
            externalDocsPath, projectName, branchName);

        // Get existing docs for comparison
        var existingDocs = await GetAllForTenantAsync(
            projectName, branchName, pathHash, cancellationToken);
        var existingByPath = existingDocs.ToDictionary(d => d.RelativePath, d => d);

        // Find files matching patterns
        var matcher = new Matcher();
        foreach (var pattern in includePatterns)
        {
            matcher.AddInclude(pattern);
        }
        foreach (var pattern in excludePatterns)
        {
            matcher.AddExclude(pattern);
        }

        var matchResult = matcher.Execute(
            new DirectoryInfoWrapper(new DirectoryInfo(externalDocsPath)));

        var added = 0;
        var updated = 0;
        var unchanged = 0;
        var errors = 0;
        var errorPaths = new List<string>();
        var processedPaths = new HashSet<string>();

        foreach (var file in matchResult.Files)
        {
            var relativePath = file.Path;
            processedPaths.Add(relativePath);

            try
            {
                var fullPath = Path.Combine(externalDocsPath, relativePath);
                var content = await File.ReadAllTextAsync(fullPath, cancellationToken);
                var contentHash = ComputeContentHash(content);

                if (existingByPath.TryGetValue(relativePath, out var existing))
                {
                    if (existing.ContentHash == contentHash)
                    {
                        unchanged++;
                        continue;
                    }

                    // Content changed - update
                    await UpdateDocumentAsync(
                        existing.Id,
                        projectName,
                        branchName,
                        pathHash,
                        relativePath,
                        content,
                        contentHash,
                        cancellationToken);
                    updated++;
                }
                else
                {
                    // New document
                    await CreateDocumentAsync(
                        projectName,
                        branchName,
                        pathHash,
                        relativePath,
                        content,
                        contentHash,
                        cancellationToken);
                    added++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to sync external doc: {Path}", relativePath);
                errors++;
                errorPaths.Add(relativePath);
            }
        }

        // Delete docs that no longer exist on disk
        var deleted = 0;
        foreach (var existing in existingDocs)
        {
            if (!processedPaths.Contains(existing.RelativePath))
            {
                await DeleteAsync(existing.Id, cancellationToken);
                deleted++;
            }
        }

        var result = new ExternalDocsSyncResult(added, updated, deleted, unchanged, errors, errorPaths);

        _logger.LogInformation(
            "External docs sync completed: Added={Added}, Updated={Updated}, Deleted={Deleted}, Unchanged={Unchanged}, Errors={Errors}",
            result.Added, result.Updated, result.Deleted, result.Unchanged, result.Errors);

        return result;
    }

    public async Task UpsertAsync(
        ExternalDocument document,
        CancellationToken cancellationToken = default)
    {
        await _documentsCollection.UpsertAsync(document, cancellationToken: cancellationToken);
    }

    public async Task UpsertChunksAsync(
        IReadOnlyList<ExternalDocumentChunk> chunks,
        CancellationToken cancellationToken = default)
    {
        foreach (var chunk in chunks)
        {
            await _chunksCollection.UpsertAsync(chunk, cancellationToken: cancellationToken);
        }
    }

    public async Task DeleteAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        // Delete chunks first
        var chunks = await GetChunksByDocumentIdAsync(id, cancellationToken);
        foreach (var chunk in chunks)
        {
            await _chunksCollection.DeleteAsync(chunk.Id, cancellationToken: cancellationToken);
        }

        // Delete document
        await _documentsCollection.DeleteAsync(id, cancellationToken: cancellationToken);
    }

    public async Task DeleteAllForTenantAsync(
        string projectName,
        string branchName,
        string pathHash,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Deleting all external docs for tenant {Project}/{Branch}/{PathHash}",
            projectName, branchName, pathHash);

        var docs = await GetAllForTenantAsync(projectName, branchName, pathHash, cancellationToken);

        foreach (var doc in docs)
        {
            await DeleteAsync(doc.Id, cancellationToken);
        }

        _logger.LogInformation("Deleted {Count} external documents", docs.Count);
    }

    public async Task<IReadOnlyList<ExternalDocumentChunk>> GetChunksByDocumentIdAsync(
        string externalDocumentId,
        CancellationToken cancellationToken = default)
    {
        var filter = new VectorSearchFilter()
            .EqualTo("external_document_id", externalDocumentId);

        var chunks = new List<ExternalDocumentChunk>();

        await foreach (var chunk in _chunksCollection.GetAsync(filter, cancellationToken: cancellationToken))
        {
            chunks.Add(chunk);
        }

        return chunks.OrderBy(c => c.ChunkIndex).ToList();
    }

    private async Task CreateDocumentAsync(
        string projectName,
        string branchName,
        string pathHash,
        string relativePath,
        string content,
        string contentHash,
        CancellationToken cancellationToken)
    {
        var title = ExtractTitle(content, relativePath);
        var summary = ExtractSummary(content);
        var embedding = await _embeddingService.GenerateEmbeddingAsync(content, cancellationToken);

        var document = new ExternalDocument
        {
            Id = Guid.NewGuid().ToString(),
            ProjectName = projectName,
            BranchName = branchName,
            PathHash = pathHash,
            RelativePath = relativePath,
            Title = title,
            Summary = summary,
            ContentHash = contentHash,
            CharCount = content.Length,
            Embedding = embedding
        };

        await UpsertAsync(document, cancellationToken);

        // Create chunks if document is large
        var lines = content.Split('\n');
        if (lines.Length > 500)
        {
            var chunkContents = _chunker.ChunkDocument(content);
            var chunks = new List<ExternalDocumentChunk>();

            for (int i = 0; i < chunkContents.Count; i++)
            {
                var chunkEmbedding = await _embeddingService.GenerateEmbeddingAsync(
                    chunkContents[i].Content, cancellationToken);

                chunks.Add(new ExternalDocumentChunk
                {
                    Id = Guid.NewGuid().ToString(),
                    ExternalDocumentId = document.Id,
                    ProjectName = projectName,
                    BranchName = branchName,
                    PathHash = pathHash,
                    ChunkIndex = i,
                    HeaderPath = chunkContents[i].HeaderPath,
                    Content = chunkContents[i].Content,
                    Embedding = chunkEmbedding
                });
            }

            await UpsertChunksAsync(chunks, cancellationToken);
        }
    }

    private async Task UpdateDocumentAsync(
        string existingId,
        string projectName,
        string branchName,
        string pathHash,
        string relativePath,
        string content,
        string contentHash,
        CancellationToken cancellationToken)
    {
        // Delete existing chunks
        var existingChunks = await GetChunksByDocumentIdAsync(existingId, cancellationToken);
        foreach (var chunk in existingChunks)
        {
            await _chunksCollection.DeleteAsync(chunk.Id, cancellationToken: cancellationToken);
        }

        // Update document
        var title = ExtractTitle(content, relativePath);
        var summary = ExtractSummary(content);
        var embedding = await _embeddingService.GenerateEmbeddingAsync(content, cancellationToken);

        var document = new ExternalDocument
        {
            Id = existingId,
            ProjectName = projectName,
            BranchName = branchName,
            PathHash = pathHash,
            RelativePath = relativePath,
            Title = title,
            Summary = summary,
            ContentHash = contentHash,
            CharCount = content.Length,
            Embedding = embedding
        };

        await UpsertAsync(document, cancellationToken);

        // Create new chunks if document is large
        var lines = content.Split('\n');
        if (lines.Length > 500)
        {
            var chunkContents = _chunker.ChunkDocument(content);
            var chunks = new List<ExternalDocumentChunk>();

            for (int i = 0; i < chunkContents.Count; i++)
            {
                var chunkEmbedding = await _embeddingService.GenerateEmbeddingAsync(
                    chunkContents[i].Content, cancellationToken);

                chunks.Add(new ExternalDocumentChunk
                {
                    Id = Guid.NewGuid().ToString(),
                    ExternalDocumentId = document.Id,
                    ProjectName = projectName,
                    BranchName = branchName,
                    PathHash = pathHash,
                    ChunkIndex = i,
                    HeaderPath = chunkContents[i].HeaderPath,
                    Content = chunkContents[i].Content,
                    Embedding = chunkEmbedding
                });
            }

            await UpsertChunksAsync(chunks, cancellationToken);
        }
    }

    private static string ComputeContentHash(string content)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private static string ExtractTitle(string content, string relativePath)
    {
        // Try to extract title from first H1 header
        var lines = content.Split('\n');
        foreach (var line in lines.Take(10))
        {
            if (line.StartsWith("# "))
            {
                return line[2..].Trim();
            }
        }

        // Fall back to filename without extension
        return Path.GetFileNameWithoutExtension(relativePath);
    }

    private static string? ExtractSummary(string content)
    {
        // Extract first paragraph after title (if any)
        var lines = content.Split('\n');
        var inSummary = false;
        var summaryLines = new List<string>();

        foreach (var line in lines.Skip(1).Take(20))
        {
            var trimmed = line.Trim();

            if (string.IsNullOrEmpty(trimmed))
            {
                if (inSummary && summaryLines.Count > 0)
                    break;
                continue;
            }

            if (trimmed.StartsWith('#'))
            {
                if (summaryLines.Count > 0)
                    break;
                continue;
            }

            inSummary = true;
            summaryLines.Add(trimmed);
        }

        if (summaryLines.Count == 0)
            return null;

        var summary = string.Join(" ", summaryLines);
        return summary.Length > 500 ? summary[..497] + "..." : summary;
    }
}
```

### 4. IDocumentChunker Interface

```csharp
// src/CompoundDocs.Common/Services/IDocumentChunker.cs
namespace CompoundDocs.Common.Services;

/// <summary>
/// Chunks large documents into smaller pieces for embedding.
/// </summary>
public interface IDocumentChunker
{
    /// <summary>
    /// Chunks a document into smaller pieces based on markdown headers.
    /// </summary>
    IReadOnlyList<ChunkResult> ChunkDocument(string content);
}

/// <summary>
/// Result of chunking a document section.
/// </summary>
public record ChunkResult(
    string Content,
    string HeaderPath);
```

### 5. Service Registration

```csharp
// In ServiceCollectionExtensions.cs
public static IServiceCollection AddExternalDocumentRepository(
    this IServiceCollection services)
{
    services.AddScoped<IExternalDocumentRepository, ExternalDocumentRepository>();

    return services;
}
```

### 6. File Pattern Matching

The implementation uses `Microsoft.Extensions.FileSystemGlobbing` for include/exclude pattern matching:

```xml
<PackageReference Include="Microsoft.Extensions.FileSystemGlobbing" Version="9.0.0" />
```

Pattern matching behavior:
- Include patterns: `["**/*.md"]` matches all markdown files recursively
- Exclude patterns: `["**/node_modules/**"]` excludes node_modules directories
- Patterns are relative to `external_docs.path`

---

## Dependencies

### Depends On

- **Phase 044**: Document Repository Interface - Base repository patterns
- **Phase 045**: Document Repository Implementation - Core repository implementation patterns
- **Phase 029**: Embedding Service - For generating embeddings
- **Phase 003**: PostgreSQL/pgvector - Database infrastructure

### Blocks

- **Phase 050+**: External Docs MCP Tools - `search_external_docs` and `rag_query_external` tools
- **Phase 051+**: External Docs File Watcher - Watch for changes in external docs folder

---

## Testing Verification

### Unit Tests

```csharp
// tests/CompoundDocs.Tests/Repositories/ExternalDocumentRepositoryTests.cs
public class ExternalDocumentRepositoryTests
{
    [Fact]
    public async Task SearchAsync_FiltersbyTenantContext()
    {
        // Arrange
        var mockCollection = new Mock<PostgresCollection<string, ExternalDocument>>();
        var repository = CreateRepository(mockCollection.Object);

        var queryEmbedding = new float[1024];

        // Act
        var results = await repository.SearchAsync(
            queryEmbedding,
            "project-a",
            "main",
            "abc123",
            limit: 10,
            minRelevanceScore: 0.7f);

        // Assert
        mockCollection.Verify(c => c.SearchAsync(
            It.IsAny<ReadOnlyMemory<float>>(),
            10,
            It.Is<VectorSearchFilter>(f =>
                f.Filters.Any(e => e.Key == "project_name" && e.Value == "project-a")),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SyncFromFileSystemAsync_AddsNewDocuments()
    {
        // Test implementation
    }

    [Fact]
    public async Task SyncFromFileSystemAsync_UpdatesChangedDocuments()
    {
        // Test implementation
    }

    [Fact]
    public async Task SyncFromFileSystemAsync_DeletesRemovedDocuments()
    {
        // Test implementation
    }

    [Fact]
    public async Task DeleteAllForTenantAsync_RemovesAllDocsAndChunks()
    {
        // Test implementation
    }
}
```

### Integration Tests

```csharp
// tests/CompoundDocs.IntegrationTests/Repositories/ExternalDocumentRepositoryIntegrationTests.cs
[Trait("Category", "Integration")]
public class ExternalDocumentRepositoryIntegrationTests : IClassFixture<PostgresFixture>
{
    private readonly IExternalDocumentRepository _repository;

    public ExternalDocumentRepositoryIntegrationTests(PostgresFixture fixture)
    {
        _repository = fixture.GetService<IExternalDocumentRepository>();
    }

    [Fact]
    public async Task SyncFromFileSystem_WithRealFiles_IndexesDocuments()
    {
        // Arrange
        var tempDir = CreateTempDocsDirectory();
        File.WriteAllText(Path.Combine(tempDir, "readme.md"), "# Test\n\nContent here.");
        File.WriteAllText(Path.Combine(tempDir, "guide.md"), "# Guide\n\nGuide content.");

        // Act
        var result = await _repository.SyncFromFileSystemAsync(
            "test-project",
            "main",
            "testhash",
            tempDir,
            new[] { "**/*.md" },
            Array.Empty<string>());

        // Assert
        Assert.Equal(2, result.Added);
        Assert.Equal(0, result.Updated);
        Assert.Equal(0, result.Deleted);
    }

    [Fact]
    public async Task SearchAsync_ReturnsRelevantDocuments()
    {
        // Test implementation
    }
}
```

### Manual Verification

```bash
# 1. Create test external docs directory
mkdir -p /tmp/test-external-docs
echo "# API Guide\n\nHow to use our API." > /tmp/test-external-docs/api.md
echo "# Setup Guide\n\nHow to set up." > /tmp/test-external-docs/setup.md

# 2. Run sync via test harness or tool
# (Verify documents appear in external_documents table)

# 3. Verify search works
psql -h localhost -p 5433 -U compounding -d compounding_docs -c \
    "SELECT relative_path, title FROM compounding.external_documents;"
```

---

## Files Created/Modified

| File | Action | Description |
|------|--------|-------------|
| `src/CompoundDocs.Common/Repositories/IExternalDocumentRepository.cs` | Create | Repository interface |
| `src/CompoundDocs.Common/Models/ExternalDocumentSearchResult.cs` | Create | Search result model |
| `src/CompoundDocs.Common/Models/ExternalDocumentSyncInfo.cs` | Create | Sync info model |
| `src/CompoundDocs.Common/Models/ExternalDocsSyncResult.cs` | Create | Sync result model |
| `src/CompoundDocs.Common/Services/IDocumentChunker.cs` | Create | Chunker interface |
| `src/CompoundDocs.McpServer/Repositories/ExternalDocumentRepository.cs` | Create | Repository implementation |
| `src/CompoundDocs.McpServer/Extensions/ServiceCollectionExtensions.cs` | Modify | Add DI registration |
| `tests/CompoundDocs.Tests/Repositories/ExternalDocumentRepositoryTests.cs` | Create | Unit tests |
| `tests/CompoundDocs.IntegrationTests/Repositories/ExternalDocumentRepositoryIntegrationTests.cs` | Create | Integration tests |

---

## Configuration Reference

### External Docs Configuration (Project Config)

```json
{
  "external_docs": {
    "path": "./docs",
    "include_patterns": ["**/*.md", "**/*.rst"],
    "exclude_patterns": ["**/node_modules/**", "**/drafts/**"]
  }
}
```

### Collection Names

| Collection | Purpose |
|------------|---------|
| `external_documents` | External document metadata and embeddings |
| `external_document_chunks` | Chunks for large external documents (>500 lines) |

---

## Key Design Decisions

### 1. Separate Collections

External documents use separate PostgreSQL tables (`external_documents`, `external_document_chunks`) to ensure:
- Clear separation from compounding docs
- External docs don't pollute RAG queries for institutional knowledge
- Independent indexing and sync lifecycle

### 2. Read-Only Semantics

The repository only supports:
- **Read**: Search, get by path/ID
- **Sync**: Full re-sync from file system
- **Delete**: Remove during sync when files are deleted

No direct create/update operations are exposed - all mutations happen through sync.

### 3. Content Hash-Based Change Detection

Documents are only re-indexed when their content changes:
- SHA256 hash of content stored with each document
- Sync compares file content hash vs stored hash
- Unchanged documents are skipped (no embedding regeneration)

### 4. Chunking Strategy

Large documents (>500 lines) are chunked using the same strategy as compounding docs:
- Split on H2/H3 markdown headers
- Store header path for context (e.g., "## API > ### Authentication")
- Chunks stored in `external_document_chunks` collection

---

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| Large external docs folder | Glob pattern matching filters files before processing |
| Embedding generation timeout | Chunking reduces individual embedding size |
| Concurrent sync operations | Repository operations are idempotent via upsert |
| Invalid external_docs path | Path validation at project activation |
| Memory pressure during sync | Process files one at a time, don't load all in memory |
