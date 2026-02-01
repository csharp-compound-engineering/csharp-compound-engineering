# Phase 045: ExternalDocumentChunk Semantic Kernel Model

> **Status**: NOT_STARTED
> **Effort Estimate**: 2-3 hours
> **Category**: Database & Storage
> **Prerequisites**: Phase 044 (ExternalDocument Model)

---

## Spec References

This phase implements the ExternalDocumentChunk Semantic Kernel model defined in:

- **spec/mcp-server/database-schema.md** - [External Document Chunks Schema](../spec/mcp-server/database-schema.md#external-document-chunks-schema)
- **spec/mcp-server/chunking.md** - [External Docs Chunking](../spec/mcp-server/chunking.md#external-docs-chunking)
- **spec/mcp-server/database-schema.md** - [Semantic Kernel Collection Setup](../spec/mcp-server/database-schema.md#semantic-kernel-collection-setup)

---

## Objectives

1. Create `ExternalDocumentChunk` Semantic Kernel model class for chunked external documents
2. Apply appropriate Semantic Kernel Vector Store attributes for PostgreSQL/pgvector
3. Configure parent external document reference via `ExternalDocumentId`
4. Inherit tenant isolation keys (ProjectName, BranchName, PathHash) from parent
5. Store chunk metadata (ChunkIndex, HeaderPath, Content)
6. Configure 1024-dimensional vector embedding with HNSW index and cosine distance
7. Ensure model is read-only at the application layer (no promotion level field)
8. Register collection in dependency injection for `external_document_chunks` table

---

## Acceptance Criteria

- [ ] `ExternalDocumentChunk` class created in `src/CompoundDocs.McpServer/Models/`
- [ ] `[VectorStoreKey]` attribute on `Id` property with `StorageName = "id"`
- [ ] `ExternalDocumentId` property with `[VectorStoreData]` and `IsFilterable = true`
- [ ] Tenant isolation properties (ProjectName, BranchName, PathHash) all filterable
- [ ] `ChunkIndex` property for ordering chunks within a document
- [ ] `HeaderPath` property for storing markdown header hierarchy
- [ ] `Content` property for the actual chunk text content
- [ ] `Embedding` property with 1024 dimensions, cosine distance, HNSW index
- [ ] No `PromotionLevel` property (external docs have no promotion)
- [ ] Collection registered for `external_document_chunks` table name
- [ ] Unit tests verify attribute configuration
- [ ] Model follows same chunking patterns as internal `DocumentChunk`

---

## Implementation Notes

### 1. ExternalDocumentChunk Model Class

Create `src/CompoundDocs.McpServer/Models/ExternalDocumentChunk.cs`:

```csharp
using Microsoft.Extensions.VectorData;

namespace CompoundDocs.McpServer.Models;

/// <summary>
/// Represents a chunk of a large external document (>500 lines) stored in the
/// external_document_chunks vector collection.
///
/// External document chunks follow the same chunking strategy as internal document chunks
/// (split at ## and ### markdown headers) but are stored in a separate collection and
/// have no promotion level capability.
///
/// Key differences from DocumentChunk:
/// - Parent reference is ExternalDocumentId (not DocumentId)
/// - No PromotionLevel property (external docs are read-only reference material)
/// - Stored in external_document_chunks table (not document_chunks)
/// </summary>
public class ExternalDocumentChunk
{
    /// <summary>
    /// Unique identifier for this chunk (GUID).
    /// </summary>
    [VectorStoreKey(StorageName = "id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Reference to the parent external document in external_documents collection.
    /// Used to associate chunks with their source document.
    /// </summary>
    [VectorStoreData(StorageName = "external_document_id", IsFilterable = true)]
    public string ExternalDocumentId { get; set; } = string.Empty;

    #region Tenant Isolation (inherited from parent)

    /// <summary>
    /// Project name for tenant isolation.
    /// Inherited from the parent ExternalDocument at indexing time.
    /// </summary>
    [VectorStoreData(StorageName = "project_name", IsFilterable = true)]
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>
    /// Git branch name for tenant isolation.
    /// Inherited from the parent ExternalDocument at indexing time.
    /// </summary>
    [VectorStoreData(StorageName = "branch_name", IsFilterable = true)]
    public string BranchName { get; set; } = string.Empty;

    /// <summary>
    /// SHA256 hash (first 16 chars) of the absolute repository path.
    /// Inherited from the parent ExternalDocument at indexing time.
    /// Enables worktree isolation.
    /// </summary>
    [VectorStoreData(StorageName = "path_hash", IsFilterable = true)]
    public string PathHash { get; set; } = string.Empty;

    #endregion

    #region Chunk Metadata

    /// <summary>
    /// Zero-based index of this chunk within the parent document.
    /// Used for ordering chunks when reconstructing document content.
    /// </summary>
    [VectorStoreData(StorageName = "chunk_index")]
    public int ChunkIndex { get; set; }

    /// <summary>
    /// Markdown header hierarchy path for this chunk.
    /// Format: "## Section > ### Subsection"
    /// Provides context for where this chunk appears in the document structure.
    /// </summary>
    [VectorStoreData(StorageName = "header_path")]
    public string HeaderPath { get; set; } = string.Empty;

    /// <summary>
    /// The actual text content of this chunk.
    /// Contains the markdown content between header split points.
    /// </summary>
    [VectorStoreData(StorageName = "content")]
    public string Content { get; set; } = string.Empty;

    #endregion

    #region Vector Embedding

    /// <summary>
    /// Vector embedding for semantic search.
    /// Generated by mxbai-embed-large model (1024 dimensions).
    /// Uses HNSW index with cosine distance for efficient similarity search.
    /// </summary>
    [VectorStoreVector(
        Dimensions: 1024,
        DistanceFunction.CosineDistance,
        IndexKind.Hnsw,
        StorageName = "embedding")]
    public ReadOnlyMemory<float>? Embedding { get; set; }

    #endregion
}
```

### 2. Differences from DocumentChunk

| Aspect | DocumentChunk | ExternalDocumentChunk |
|--------|---------------|----------------------|
| Parent Reference | `DocumentId` | `ExternalDocumentId` |
| Promotion Level | Has `PromotionLevel` property | **No** promotion level |
| Table Name | `document_chunks` | `external_document_chunks` |
| Write Access | Promotion can be updated | **Read-only** reference |
| Chunking Rules | Same (>500 lines, H2/H3 headers) | Same |
| Embedding | 1024-dim, HNSW, cosine | Same |

### 3. Collection Registration

Add to the vector store service setup (building on Phase 044):

```csharp
// External document chunks collection (for large external docs >500 lines)
var externalChunksCollection = new PostgresCollection<string, ExternalDocumentChunk>(
    dataSource,
    "external_document_chunks",
    ownsDataSource: false
);

// Ensure collection exists (creates table if needed)
await externalChunksCollection.EnsureCollectionExistsAsync();
```

### 4. Service Registration

Extend DI registration to include external chunk collection:

```csharp
// In ServiceCollectionExtensions.cs or equivalent
public static IServiceCollection AddExternalDocumentChunkCollection(
    this IServiceCollection services,
    NpgsqlDataSource dataSource)
{
    services.AddSingleton<IVectorStoreRecordCollection<string, ExternalDocumentChunk>>(sp =>
    {
        var collection = new PostgresCollection<string, ExternalDocumentChunk>(
            dataSource,
            "external_document_chunks",
            ownsDataSource: false
        );
        return collection;
    });

    return services;
}
```

### 5. Chunking Strategy (Same as Internal Docs)

External documents use the same chunking rules as internal compound documents:

| Rule | Value | Description |
|------|-------|-------------|
| Threshold | >500 lines | Documents exceeding this are chunked |
| Split Points | `##` and `###` | H2 and H3 markdown headers |
| Header Path Format | `## Section > ### Subsection` | Hierarchy with ` > ` separator |
| Empty Sections | Skipped | No chunk created for empty content |
| Code Blocks | Preserved | Never split mid-code-block |

### 6. Read-Only Constraints

External document chunks are read-only reference material:

1. **No promotion level**: Unlike internal chunks that inherit promotion from parent
2. **Sync lifecycle**: Chunks are regenerated when external docs are re-indexed
3. **Deletion**: Chunks are deleted when parent external document is deleted
4. **No direct updates**: Chunks are only modified via full re-indexing

```csharp
// Example: Delete all chunks for an external document
public async Task DeleteChunksForExternalDocumentAsync(
    string externalDocumentId,
    CancellationToken cancellationToken = default)
{
    // Build filter for all chunks belonging to this external document
    var filter = new VectorSearchFilter()
        .EqualTo("external_document_id", externalDocumentId);

    // Find and delete all matching chunks
    var chunksToDelete = await _externalChunksCollection
        .GetAsync(filter, cancellationToken)
        .ToListAsync(cancellationToken);

    foreach (var chunk in chunksToDelete)
    {
        await _externalChunksCollection.DeleteAsync(chunk.Id, cancellationToken);
    }
}
```

---

## Dependencies

### Depends On

- **Phase 044**: ExternalDocument Model - Parent document model must exist
- **Phase 028**: Semantic Kernel Integration - VectorStore base infrastructure
- **Phase 003**: PostgreSQL/pgvector Setup - Database must be available

### Blocks

- **Phase 046+**: External document chunking service implementation
- **Phase 047+**: External document indexing with chunking logic
- **Phase 048+**: Search tools that query external document chunks

---

## Testing Verification

### Unit Tests

```csharp
// tests/CompoundDocs.Tests/Models/ExternalDocumentChunkTests.cs
public class ExternalDocumentChunkTests
{
    [Fact]
    public void ExternalDocumentChunk_HasCorrectKeyAttribute()
    {
        // Arrange
        var idProperty = typeof(ExternalDocumentChunk).GetProperty(nameof(ExternalDocumentChunk.Id));

        // Act
        var keyAttribute = idProperty?.GetCustomAttribute<VectorStoreKeyAttribute>();

        // Assert
        Assert.NotNull(keyAttribute);
        Assert.Equal("id", keyAttribute.StorageName);
    }

    [Fact]
    public void ExternalDocumentChunk_ExternalDocumentIdIsFilterable()
    {
        // Arrange
        var property = typeof(ExternalDocumentChunk)
            .GetProperty(nameof(ExternalDocumentChunk.ExternalDocumentId));

        // Act
        var dataAttribute = property?.GetCustomAttribute<VectorStoreDataAttribute>();

        // Assert
        Assert.NotNull(dataAttribute);
        Assert.Equal("external_document_id", dataAttribute.StorageName);
        Assert.True(dataAttribute.IsFilterable);
    }

    [Fact]
    public void ExternalDocumentChunk_TenantPropertiesAreFilterable()
    {
        // Arrange
        var tenantProperties = new[]
        {
            nameof(ExternalDocumentChunk.ProjectName),
            nameof(ExternalDocumentChunk.BranchName),
            nameof(ExternalDocumentChunk.PathHash)
        };

        // Act & Assert
        foreach (var propName in tenantProperties)
        {
            var property = typeof(ExternalDocumentChunk).GetProperty(propName);
            var dataAttribute = property?.GetCustomAttribute<VectorStoreDataAttribute>();

            Assert.NotNull(dataAttribute);
            Assert.True(dataAttribute.IsFilterable, $"{propName} should be filterable");
        }
    }

    [Fact]
    public void ExternalDocumentChunk_EmbeddingHasCorrectConfiguration()
    {
        // Arrange
        var embeddingProperty = typeof(ExternalDocumentChunk)
            .GetProperty(nameof(ExternalDocumentChunk.Embedding));

        // Act
        var vectorAttribute = embeddingProperty?.GetCustomAttribute<VectorStoreVectorAttribute>();

        // Assert
        Assert.NotNull(vectorAttribute);
        Assert.Equal(1024, vectorAttribute.Dimensions);
        Assert.Equal(DistanceFunction.CosineDistance, vectorAttribute.DistanceFunction);
        Assert.Equal(IndexKind.Hnsw, vectorAttribute.IndexKind);
        Assert.Equal("embedding", vectorAttribute.StorageName);
    }

    [Fact]
    public void ExternalDocumentChunk_DoesNotHavePromotionLevel()
    {
        // External docs have no promotion capability
        var promotionProperty = typeof(ExternalDocumentChunk)
            .GetProperty("PromotionLevel");

        Assert.Null(promotionProperty);
    }

    [Fact]
    public void ExternalDocumentChunk_HasChunkMetadataProperties()
    {
        // Arrange
        var chunk = new ExternalDocumentChunk
        {
            ChunkIndex = 2,
            HeaderPath = "## Overview > ### Getting Started",
            Content = "This section covers..."
        };

        // Assert
        Assert.Equal(2, chunk.ChunkIndex);
        Assert.Equal("## Overview > ### Getting Started", chunk.HeaderPath);
        Assert.Equal("This section covers...", chunk.Content);
    }

    [Fact]
    public void ExternalDocumentChunk_InheritsParentTenantContext()
    {
        // Arrange - Simulating chunk creation from parent
        var parentDoc = new ExternalDocument
        {
            Id = "ext-doc-123",
            ProjectName = "my-project",
            BranchName = "main",
            PathHash = "abc123def456"
        };

        // Act
        var chunk = new ExternalDocumentChunk
        {
            ExternalDocumentId = parentDoc.Id,
            ProjectName = parentDoc.ProjectName,
            BranchName = parentDoc.BranchName,
            PathHash = parentDoc.PathHash,
            ChunkIndex = 0,
            HeaderPath = "## Introduction",
            Content = "First chunk content..."
        };

        // Assert
        Assert.Equal(parentDoc.Id, chunk.ExternalDocumentId);
        Assert.Equal(parentDoc.ProjectName, chunk.ProjectName);
        Assert.Equal(parentDoc.BranchName, chunk.BranchName);
        Assert.Equal(parentDoc.PathHash, chunk.PathHash);
    }
}
```

### Integration Tests

```csharp
// tests/CompoundDocs.IntegrationTests/Models/ExternalDocumentChunkIntegrationTests.cs
[Trait("Category", "Integration")]
public class ExternalDocumentChunkIntegrationTests : IClassFixture<PostgresFixture>
{
    private readonly IVectorStoreRecordCollection<string, ExternalDocumentChunk> _collection;

    public ExternalDocumentChunkIntegrationTests(PostgresFixture fixture)
    {
        _collection = fixture.GetService<IVectorStoreRecordCollection<string, ExternalDocumentChunk>>();
    }

    [Fact]
    public async Task Collection_CanBeCreated()
    {
        // Act
        await _collection.EnsureCollectionExistsAsync();
        var exists = await _collection.CollectionExistsAsync();

        // Assert
        Assert.True(exists);
    }

    [Fact]
    public async Task ExternalDocumentChunk_CanBeInsertedAndRetrieved()
    {
        // Arrange
        await _collection.EnsureCollectionExistsAsync();

        var chunk = new ExternalDocumentChunk
        {
            Id = Guid.NewGuid().ToString(),
            ExternalDocumentId = "parent-ext-doc-id",
            ProjectName = "test-project",
            BranchName = "main",
            PathHash = "testhash12345678",
            ChunkIndex = 0,
            HeaderPath = "## Test Section",
            Content = "Test chunk content for integration test."
        };

        // Act
        await _collection.UpsertAsync(chunk);
        var retrieved = await _collection.GetAsync(chunk.Id);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(chunk.ExternalDocumentId, retrieved.ExternalDocumentId);
        Assert.Equal(chunk.ChunkIndex, retrieved.ChunkIndex);
        Assert.Equal(chunk.HeaderPath, retrieved.HeaderPath);
        Assert.Equal(chunk.Content, retrieved.Content);

        // Cleanup
        await _collection.DeleteAsync(chunk.Id);
    }

    [Fact]
    public async Task ExternalDocumentChunk_CanFilterByExternalDocumentId()
    {
        // Arrange
        await _collection.EnsureCollectionExistsAsync();

        var parentId = Guid.NewGuid().ToString();
        var chunks = Enumerable.Range(0, 3).Select(i => new ExternalDocumentChunk
        {
            Id = Guid.NewGuid().ToString(),
            ExternalDocumentId = parentId,
            ProjectName = "test-project",
            BranchName = "main",
            PathHash = "testhash12345678",
            ChunkIndex = i,
            HeaderPath = $"## Section {i}",
            Content = $"Content for chunk {i}"
        }).ToList();

        foreach (var chunk in chunks)
        {
            await _collection.UpsertAsync(chunk);
        }

        // Act
        var filter = new VectorSearchFilter()
            .EqualTo("external_document_id", parentId);

        // Retrieve with filter (implementation depends on collection API)
        // This is a simplified example

        // Cleanup
        foreach (var chunk in chunks)
        {
            await _collection.DeleteAsync(chunk.Id);
        }
    }
}
```

### Manual Verification

```bash
# 1. Verify table was created by Semantic Kernel
psql -h localhost -p 5433 -U compounding -d compounding_docs -c "
  SELECT column_name, data_type
  FROM information_schema.columns
  WHERE table_schema = 'compounding'
    AND table_name = 'external_document_chunks'
  ORDER BY ordinal_position;
"

# Expected columns:
# id                    | text
# external_document_id  | text
# project_name          | text
# branch_name           | text
# path_hash             | text
# chunk_index           | integer
# header_path           | text
# content               | text
# embedding             | vector(1024)

# 2. Verify HNSW index was created
psql -h localhost -p 5433 -U compounding -d compounding_docs -c "
  SELECT indexname, indexdef
  FROM pg_indexes
  WHERE tablename = 'external_document_chunks'
    AND indexdef LIKE '%hnsw%';
"

# 3. Insert a test chunk and verify
psql -h localhost -p 5433 -U compounding -d compounding_docs -c "
  INSERT INTO compounding.external_document_chunks
    (id, external_document_id, project_name, branch_name, path_hash,
     chunk_index, header_path, content)
  VALUES
    ('test-ext-chunk-1', 'test-ext-doc-1', 'test-project', 'main',
     'abc123def456', 0, '## Test', 'Test content');

  SELECT * FROM compounding.external_document_chunks
  WHERE id = 'test-ext-chunk-1';

  DELETE FROM compounding.external_document_chunks
  WHERE id = 'test-ext-chunk-1';
"
```

---

## Files Created/Modified

| File | Action | Description |
|------|--------|-------------|
| `src/CompoundDocs.McpServer/Models/ExternalDocumentChunk.cs` | Create | Semantic Kernel model class |
| `src/CompoundDocs.McpServer/Extensions/ServiceCollectionExtensions.cs` | Modify | Add collection registration |
| `tests/CompoundDocs.Tests/Models/ExternalDocumentChunkTests.cs` | Create | Unit tests for model |
| `tests/CompoundDocs.IntegrationTests/Models/ExternalDocumentChunkIntegrationTests.cs` | Create | Integration tests |

---

## Database Schema

The `external_document_chunks` table (auto-created by Semantic Kernel):

| Column | Type | Nullable | Description |
|--------|------|----------|-------------|
| `id` | `text` | NOT NULL | Primary key (GUID) |
| `external_document_id` | `text` | NOT NULL | Foreign key to external_documents |
| `project_name` | `text` | NOT NULL | Tenant isolation |
| `branch_name` | `text` | NOT NULL | Tenant isolation |
| `path_hash` | `text` | NOT NULL | Tenant isolation |
| `chunk_index` | `integer` | NOT NULL | Order within document |
| `header_path` | `text` | NOT NULL | Header hierarchy |
| `content` | `text` | NOT NULL | Chunk text |
| `embedding` | `vector(1024)` | NULL | Semantic embedding |

**Indexes (auto-created)**:
- Primary key on `id`
- HNSW index on `embedding` for vector similarity search
- Filterable columns indexed for tenant queries

---

## Notes

- This model mirrors `DocumentChunk` but for external reference documentation
- No promotion level exists because external docs are read-only references
- Chunks are always deleted and regenerated when the parent document changes
- The 500-line threshold and H2/H3 splitting strategy matches internal document chunking
- Header path format (`## Section > ### Subsection`) provides context for search results
- All tenant properties are duplicated from parent for efficient filtering without joins
