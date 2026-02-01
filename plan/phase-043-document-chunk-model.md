# Phase 043: DocumentChunk Semantic Kernel Model

> **Status**: PLANNED
> **Category**: Database & Storage
> **Estimated Effort**: S
> **Prerequisites**: Phase 042 (CompoundDocument Model)

---

## Spec References

- [mcp-server/database-schema.md - Document Chunks Schema](../spec/mcp-server/database-schema.md#document-chunks-schema-for-large-documents)
- [mcp-server/chunking.md - Chunk Metadata](../spec/mcp-server/chunking.md#chunk-metadata-db-only)
- [mcp-server/chunking.md - Chunk Fields](../spec/mcp-server/chunking.md#chunk-fields)
- [mcp-server/chunking.md - Chunk Promotion](../spec/mcp-server/chunking.md#chunk-promotion)
- [research/semantic-kernel-pgvector-package-update.md](../research/semantic-kernel-pgvector-package-update.md)

---

## Objectives

1. Create `DocumentChunk` Semantic Kernel model class for storing chunks of large documents
2. Implement parent document reference via `document_id` field
3. Add chunk-specific fields (chunk_index, header_path, content)
4. Configure vector embedding field with 1024 dimensions for mxbai-embed-large
5. Implement tenant isolation fields inherited from parent document
6. Include promotion_level field that synchronizes with parent document
7. Set appropriate filterable columns for efficient tenant-scoped queries

---

## Acceptance Criteria

- [ ] `DocumentChunk` class created with `[VectorStoreKey]` attribute on Id
- [ ] `DocumentId` field references parent `CompoundDocument` with `IsFilterable = true`
- [ ] Tenant isolation fields (`ProjectName`, `BranchName`, `PathHash`) marked as filterable
- [ ] `PromotionLevel` field inherited from parent, marked as filterable
- [ ] `ChunkIndex` field for ordering chunks within a document
- [ ] `HeaderPath` field stores header hierarchy (e.g., "## Section > ### Subsection")
- [ ] `Content` field stores the chunk's text content
- [ ] Vector embedding configured for 1024 dimensions with cosine distance and HNSW index
- [ ] All `StorageName` attributes use snake_case for PostgreSQL compatibility
- [ ] Unit tests verify model attribute configuration
- [ ] Model integrates with `PostgresCollection<string, DocumentChunk>` setup

---

## Implementation Notes

### 1. DocumentChunk Model Class

Create the Semantic Kernel model in the Models directory:

```csharp
// src/CompoundDocs.McpServer/Models/DocumentChunk.cs
using Microsoft.Extensions.VectorData;

namespace CompoundDocs.McpServer.Models;

/// <summary>
/// Represents a chunk of a large document stored in the document_chunks collection.
/// Documents exceeding 500 lines are split by markdown headers (H2/H3) and stored as chunks.
/// Each chunk inherits tenant isolation and promotion level from its parent document.
/// </summary>
public sealed class DocumentChunk
{
    /// <summary>
    /// Unique identifier for this chunk (GUID string).
    /// </summary>
    [VectorStoreKey(StorageName = "id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Reference to the parent document's ID.
    /// Used to retrieve all chunks for a document or cascade deletes.
    /// </summary>
    [VectorStoreData(StorageName = "document_id", IsFilterable = true)]
    public string DocumentId { get; set; } = string.Empty;

    #region Tenant Isolation (Inherited from Parent)

    /// <summary>
    /// Project name for tenant isolation. Inherited from parent document.
    /// </summary>
    [VectorStoreData(StorageName = "project_name", IsFilterable = true)]
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>
    /// Git branch name for tenant isolation. Inherited from parent document.
    /// </summary>
    [VectorStoreData(StorageName = "branch_name", IsFilterable = true)]
    public string BranchName { get; set; } = string.Empty;

    /// <summary>
    /// SHA256 hash (first 16 chars) of the repository absolute path.
    /// Enables git worktree support. Inherited from parent document.
    /// </summary>
    [VectorStoreData(StorageName = "path_hash", IsFilterable = true)]
    public string PathHash { get; set; } = string.Empty;

    #endregion

    /// <summary>
    /// Promotion level inherited from parent document.
    /// Updated atomically when parent document promotion changes.
    /// Values: "standard", "elevated", "foundational"
    /// </summary>
    [VectorStoreData(StorageName = "promotion_level", IsFilterable = true)]
    public string PromotionLevel { get; set; } = "standard";

    #region Chunk Metadata

    /// <summary>
    /// Zero-based index of this chunk within the parent document.
    /// Used for ordering chunks when reconstructing document.
    /// </summary>
    [VectorStoreData(StorageName = "chunk_index")]
    public int ChunkIndex { get; set; }

    /// <summary>
    /// Header hierarchy path showing chunk location in document structure.
    /// Format: "## Section > ### Subsection"
    /// Uses > as separator between heading levels, preserves ## markers.
    /// </summary>
    [VectorStoreData(StorageName = "header_path")]
    public string HeaderPath { get; set; } = string.Empty;

    /// <summary>
    /// Start line number in the source document (1-based).
    /// Used for linking back to source file location.
    /// </summary>
    [VectorStoreData(StorageName = "start_line")]
    public int StartLine { get; set; }

    /// <summary>
    /// End line number in the source document (1-based, inclusive).
    /// Used for linking back to source file location.
    /// </summary>
    [VectorStoreData(StorageName = "end_line")]
    public int EndLine { get; set; }

    /// <summary>
    /// The extracted text content of this chunk.
    /// Contains the markdown content between the chunk's header boundaries.
    /// </summary>
    [VectorStoreData(StorageName = "content")]
    public string Content { get; set; } = string.Empty;

    #endregion

    /// <summary>
    /// Vector embedding for semantic search.
    /// Generated using mxbai-embed-large model (1024 dimensions).
    /// Uses cosine distance with HNSW index for efficient similarity search.
    /// </summary>
    [VectorStoreVector(
        Dimensions: 1024,
        DistanceFunction.CosineDistance,
        IndexKind.Hnsw,
        StorageName = "embedding")]
    public ReadOnlyMemory<float>? Embedding { get; set; }
}
```

### 2. Model Design Decisions

#### Parent Document Reference

The `DocumentId` field creates a logical foreign key to the parent `CompoundDocument`:

```csharp
[VectorStoreData(StorageName = "document_id", IsFilterable = true)]
public string DocumentId { get; set; } = string.Empty;
```

- Marked as filterable for efficient chunk retrieval by parent
- Used for cascading operations (delete document -> delete chunks)
- No database-level foreign key (Semantic Kernel doesn't enforce referential integrity)

#### Tenant Isolation Inheritance

Chunks inherit all three tenant isolation fields from their parent document:

| Field | Purpose | Why Inherited |
|-------|---------|---------------|
| `ProjectName` | Project-level isolation | Enables direct chunk queries without parent join |
| `BranchName` | Branch-level isolation | Search within specific branch context |
| `PathHash` | Worktree support | Same project at different paths |

All three are marked `IsFilterable = true` to support the standard tenant filter:

```csharp
var filter = new VectorSearchFilter()
    .EqualTo("project_name", activeProject.ProjectName)
    .EqualTo("branch_name", activeProject.BranchName)
    .EqualTo("path_hash", activeProject.PathHash);
```

#### Promotion Level Synchronization

The `PromotionLevel` field is denormalized onto chunks for two reasons:

1. **Query Performance**: Direct filtering without joining parent document
2. **Atomic Updates**: When parent promotion changes, all chunks update atomically

From the chunking spec:
> When a document's promotion level is updated, all associated chunks in document_chunks are updated atomically.

#### Line Range Fields

The `StartLine` and `EndLine` fields provide:

- Source file linking for IDE integration
- Chunk boundary verification during updates
- Content diff detection

### 3. PostgresCollection Setup

Register the collection alongside documents:

```csharp
// In collection setup (Phase 044+)
var chunksCollection = new PostgresCollection<string, DocumentChunk>(
    dataSource,
    "document_chunks",
    ownsDataSource: false
);

await chunksCollection.EnsureCollectionExistsAsync();
```

### 4. Factory Method for Chunk Creation

Add a factory method to create chunks from parent context:

```csharp
// src/CompoundDocs.McpServer/Models/DocumentChunk.cs

/// <summary>
/// Creates a new chunk inheriting tenant context from parent document.
/// </summary>
/// <param name="parent">The parent CompoundDocument.</param>
/// <param name="chunkIndex">Zero-based chunk index.</param>
/// <param name="headerPath">Header hierarchy path.</param>
/// <param name="content">Chunk content.</param>
/// <param name="startLine">Start line in source (1-based).</param>
/// <param name="endLine">End line in source (1-based).</param>
/// <returns>A new DocumentChunk with inherited tenant context.</returns>
public static DocumentChunk CreateFromParent(
    CompoundDocument parent,
    int chunkIndex,
    string headerPath,
    string content,
    int startLine,
    int endLine)
{
    ArgumentNullException.ThrowIfNull(parent);
    ArgumentException.ThrowIfNullOrEmpty(content);

    return new DocumentChunk
    {
        Id = Guid.NewGuid().ToString(),
        DocumentId = parent.Id,
        ProjectName = parent.ProjectName,
        BranchName = parent.BranchName,
        PathHash = parent.PathHash,
        PromotionLevel = parent.PromotionLevel,
        ChunkIndex = chunkIndex,
        HeaderPath = headerPath,
        Content = content,
        StartLine = startLine,
        EndLine = endLine
        // Embedding set separately after generation
    };
}
```

### 5. Extension Method for Bulk Promotion Update

```csharp
// src/CompoundDocs.McpServer/Extensions/DocumentChunkExtensions.cs
using CompoundDocs.McpServer.Models;

namespace CompoundDocs.McpServer.Extensions;

/// <summary>
/// Extension methods for DocumentChunk operations.
/// </summary>
public static class DocumentChunkExtensions
{
    /// <summary>
    /// Updates the promotion level on a collection of chunks.
    /// Used when parent document promotion level changes.
    /// </summary>
    /// <param name="chunks">Chunks to update.</param>
    /// <param name="newPromotionLevel">New promotion level value.</param>
    /// <returns>Chunks with updated promotion level.</returns>
    public static IEnumerable<DocumentChunk> WithPromotionLevel(
        this IEnumerable<DocumentChunk> chunks,
        string newPromotionLevel)
    {
        ArgumentException.ThrowIfNullOrEmpty(newPromotionLevel);

        foreach (var chunk in chunks)
        {
            chunk.PromotionLevel = newPromotionLevel;
            yield return chunk;
        }
    }
}
```

---

## Field Reference

| Field | Type | Storage Name | Filterable | Description |
|-------|------|--------------|------------|-------------|
| Id | string | id | Key | Unique chunk identifier (GUID) |
| DocumentId | string | document_id | Yes | Parent document reference |
| ProjectName | string | project_name | Yes | Tenant isolation - project |
| BranchName | string | branch_name | Yes | Tenant isolation - branch |
| PathHash | string | path_hash | Yes | Tenant isolation - worktree |
| PromotionLevel | string | promotion_level | Yes | Inherited from parent |
| ChunkIndex | int | chunk_index | No | Order within document |
| HeaderPath | string | header_path | No | Header hierarchy string |
| StartLine | int | start_line | No | Source start line (1-based) |
| EndLine | int | end_line | No | Source end line (1-based) |
| Content | string | content | No | Chunk text content |
| Embedding | ReadOnlyMemory<float>? | embedding | Vector | 1024-dim semantic vector |

---

## Dependencies

### Depends On

- **Phase 042**: CompoundDocument Model - Parent document model must exist for factory method

### Blocks

- **Phase 044**: ExternalDocument Model - Same pattern for external docs
- **Phase 045**: ExternalDocumentChunk Model - Chunk model for external docs
- **Phase 046+**: Vector Store Collection Setup - Collection registration
- **Phase 050+**: Chunking Service - Creates DocumentChunk instances

---

## Testing Verification

### Unit Tests

```csharp
// tests/CompoundDocs.Tests/Models/DocumentChunkTests.cs
using CompoundDocs.McpServer.Models;

namespace CompoundDocs.Tests.Models;

public class DocumentChunkTests
{
    [Fact]
    public void NewChunk_HasGeneratedGuidId()
    {
        // Arrange & Act
        var chunk = new DocumentChunk();

        // Assert
        Assert.NotNull(chunk.Id);
        Assert.True(Guid.TryParse(chunk.Id, out _));
    }

    [Fact]
    public void NewChunk_HasDefaultPromotionLevel()
    {
        // Arrange & Act
        var chunk = new DocumentChunk();

        // Assert
        Assert.Equal("standard", chunk.PromotionLevel);
    }

    [Fact]
    public void NewChunk_HasEmptyTenantFields()
    {
        // Arrange & Act
        var chunk = new DocumentChunk();

        // Assert
        Assert.Equal(string.Empty, chunk.ProjectName);
        Assert.Equal(string.Empty, chunk.BranchName);
        Assert.Equal(string.Empty, chunk.PathHash);
        Assert.Equal(string.Empty, chunk.DocumentId);
    }

    [Fact]
    public void NewChunk_HasZeroLineNumbers()
    {
        // Arrange & Act
        var chunk = new DocumentChunk();

        // Assert
        Assert.Equal(0, chunk.ChunkIndex);
        Assert.Equal(0, chunk.StartLine);
        Assert.Equal(0, chunk.EndLine);
    }

    [Fact]
    public void NewChunk_EmbeddingIsNull()
    {
        // Arrange & Act
        var chunk = new DocumentChunk();

        // Assert
        Assert.Null(chunk.Embedding);
    }

    [Fact]
    public void CreateFromParent_InheritsTenantContext()
    {
        // Arrange
        var parent = new CompoundDocument
        {
            Id = "parent-id",
            ProjectName = "test-project",
            BranchName = "main",
            PathHash = "abc123",
            PromotionLevel = "elevated"
        };

        // Act
        var chunk = DocumentChunk.CreateFromParent(
            parent,
            chunkIndex: 0,
            headerPath: "## Section",
            content: "Test content",
            startLine: 10,
            endLine: 25);

        // Assert
        Assert.Equal("parent-id", chunk.DocumentId);
        Assert.Equal("test-project", chunk.ProjectName);
        Assert.Equal("main", chunk.BranchName);
        Assert.Equal("abc123", chunk.PathHash);
        Assert.Equal("elevated", chunk.PromotionLevel);
    }

    [Fact]
    public void CreateFromParent_SetsChunkMetadata()
    {
        // Arrange
        var parent = new CompoundDocument { Id = "parent-id" };

        // Act
        var chunk = DocumentChunk.CreateFromParent(
            parent,
            chunkIndex: 2,
            headerPath: "## Root > ### Subsection",
            content: "Detailed content here",
            startLine: 45,
            endLine: 120);

        // Assert
        Assert.Equal(2, chunk.ChunkIndex);
        Assert.Equal("## Root > ### Subsection", chunk.HeaderPath);
        Assert.Equal("Detailed content here", chunk.Content);
        Assert.Equal(45, chunk.StartLine);
        Assert.Equal(120, chunk.EndLine);
    }

    [Fact]
    public void CreateFromParent_GeneratesUniqueId()
    {
        // Arrange
        var parent = new CompoundDocument { Id = "parent-id" };

        // Act
        var chunk1 = DocumentChunk.CreateFromParent(parent, 0, "", "content", 1, 10);
        var chunk2 = DocumentChunk.CreateFromParent(parent, 1, "", "content", 11, 20);

        // Assert
        Assert.NotEqual(chunk1.Id, chunk2.Id);
    }

    [Fact]
    public void CreateFromParent_ThrowsOnNullParent()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            DocumentChunk.CreateFromParent(null!, 0, "", "content", 1, 10));
    }

    [Fact]
    public void CreateFromParent_ThrowsOnEmptyContent()
    {
        // Arrange
        var parent = new CompoundDocument { Id = "parent-id" };

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            DocumentChunk.CreateFromParent(parent, 0, "", "", 1, 10));
    }
}
```

### Attribute Configuration Tests

```csharp
// tests/CompoundDocs.Tests/Models/DocumentChunkAttributeTests.cs
using System.Reflection;
using CompoundDocs.McpServer.Models;
using Microsoft.Extensions.VectorData;

namespace CompoundDocs.Tests.Models;

public class DocumentChunkAttributeTests
{
    [Fact]
    public void Id_HasVectorStoreKeyAttribute()
    {
        var property = typeof(DocumentChunk).GetProperty(nameof(DocumentChunk.Id));
        var attribute = property?.GetCustomAttribute<VectorStoreKeyAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal("id", attribute.StorageName);
    }

    [Theory]
    [InlineData(nameof(DocumentChunk.DocumentId), "document_id", true)]
    [InlineData(nameof(DocumentChunk.ProjectName), "project_name", true)]
    [InlineData(nameof(DocumentChunk.BranchName), "branch_name", true)]
    [InlineData(nameof(DocumentChunk.PathHash), "path_hash", true)]
    [InlineData(nameof(DocumentChunk.PromotionLevel), "promotion_level", true)]
    [InlineData(nameof(DocumentChunk.ChunkIndex), "chunk_index", false)]
    [InlineData(nameof(DocumentChunk.HeaderPath), "header_path", false)]
    [InlineData(nameof(DocumentChunk.StartLine), "start_line", false)]
    [InlineData(nameof(DocumentChunk.EndLine), "end_line", false)]
    [InlineData(nameof(DocumentChunk.Content), "content", false)]
    public void DataFields_HaveCorrectAttributes(
        string propertyName,
        string expectedStorageName,
        bool expectedFilterable)
    {
        var property = typeof(DocumentChunk).GetProperty(propertyName);
        var attribute = property?.GetCustomAttribute<VectorStoreDataAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(expectedStorageName, attribute.StorageName);
        Assert.Equal(expectedFilterable, attribute.IsFilterable);
    }

    [Fact]
    public void Embedding_HasCorrectVectorConfiguration()
    {
        var property = typeof(DocumentChunk).GetProperty(nameof(DocumentChunk.Embedding));
        var attribute = property?.GetCustomAttribute<VectorStoreVectorAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(1024, attribute.Dimensions);
        Assert.Equal(DistanceFunction.CosineDistance, attribute.DistanceFunction);
        Assert.Equal(IndexKind.Hnsw, attribute.IndexKind);
        Assert.Equal("embedding", attribute.StorageName);
    }

    [Fact]
    public void AllFilterableFields_CanBeUsedInTenantFilter()
    {
        // Verify we have all required fields for tenant isolation
        var filterableFields = typeof(DocumentChunk)
            .GetProperties()
            .Where(p => p.GetCustomAttribute<VectorStoreDataAttribute>()?.IsFilterable == true)
            .Select(p => p.GetCustomAttribute<VectorStoreDataAttribute>()?.StorageName)
            .ToList();

        Assert.Contains("project_name", filterableFields);
        Assert.Contains("branch_name", filterableFields);
        Assert.Contains("path_hash", filterableFields);
        Assert.Contains("document_id", filterableFields);
        Assert.Contains("promotion_level", filterableFields);
    }
}
```

### Extension Method Tests

```csharp
// tests/CompoundDocs.Tests/Extensions/DocumentChunkExtensionsTests.cs
using CompoundDocs.McpServer.Extensions;
using CompoundDocs.McpServer.Models;

namespace CompoundDocs.Tests.Extensions;

public class DocumentChunkExtensionsTests
{
    [Fact]
    public void WithPromotionLevel_UpdatesAllChunks()
    {
        // Arrange
        var chunks = new List<DocumentChunk>
        {
            new() { PromotionLevel = "standard" },
            new() { PromotionLevel = "standard" },
            new() { PromotionLevel = "standard" }
        };

        // Act
        var updated = chunks.WithPromotionLevel("elevated").ToList();

        // Assert
        Assert.All(updated, c => Assert.Equal("elevated", c.PromotionLevel));
    }

    [Fact]
    public void WithPromotionLevel_PreservesOtherFields()
    {
        // Arrange
        var chunk = new DocumentChunk
        {
            Id = "test-id",
            DocumentId = "doc-id",
            Content = "test content",
            ChunkIndex = 5
        };

        // Act
        var updated = new[] { chunk }.WithPromotionLevel("foundational").First();

        // Assert
        Assert.Equal("test-id", updated.Id);
        Assert.Equal("doc-id", updated.DocumentId);
        Assert.Equal("test content", updated.Content);
        Assert.Equal(5, updated.ChunkIndex);
    }

    [Fact]
    public void WithPromotionLevel_ThrowsOnEmptyLevel()
    {
        var chunks = new List<DocumentChunk> { new() };

        Assert.Throws<ArgumentException>(() =>
            chunks.WithPromotionLevel("").ToList());
    }
}
```

---

## Files Created/Modified

| File | Action | Description |
|------|--------|-------------|
| `src/CompoundDocs.McpServer/Models/DocumentChunk.cs` | Create | Semantic Kernel model class |
| `src/CompoundDocs.McpServer/Extensions/DocumentChunkExtensions.cs` | Create | Promotion level update helper |
| `tests/CompoundDocs.Tests/Models/DocumentChunkTests.cs` | Create | Unit tests for model |
| `tests/CompoundDocs.Tests/Models/DocumentChunkAttributeTests.cs` | Create | Attribute configuration tests |
| `tests/CompoundDocs.Tests/Extensions/DocumentChunkExtensionsTests.cs` | Create | Extension method tests |

---

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| Orphaned chunks | Delete chunks atomically with parent document |
| Promotion desync | Transaction-based updates of parent + all chunks |
| Missing tenant context | Factory method enforces inheritance from parent |
| Dimension mismatch | Constant `1024` matches mxbai-embed-large output |
| Schema drift | Attribute-based schema via Semantic Kernel conventions |
