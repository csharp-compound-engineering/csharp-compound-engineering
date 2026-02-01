# Phase 044: ExternalDocument Semantic Kernel Model

> **Status**: PLANNED
> **Category**: Database & Storage
> **Estimated Effort**: S
> **Prerequisites**: Phase 042 (CompoundDocument Model)

---

## Spec References

- [spec/mcp-server/database-schema.md - External Documents Schema](../spec/mcp-server/database-schema.md#external-documents-schema-separate-collection)
- [spec/configuration.md - External Documentation Settings](../spec/configuration.md#external-documentation-optional)

---

## Objectives

1. Create `ExternalDocument` Semantic Kernel model class for read-only external documentation
2. Establish a separate collection from `CompoundDocument` to maintain schema isolation
3. Track external source paths via tenant isolation keys (project_name, branch_name, path_hash)
4. Implement the model without promotion_level field (external docs are read-only reference material)
5. Register the external_documents collection with Semantic Kernel

---

## Acceptance Criteria

- [ ] `ExternalDocument` class created with all Semantic Kernel attributes
- [ ] Model uses separate collection name (`external_documents`) from compound docs
- [ ] No `PromotionLevel` property (external docs don't participate in promotion)
- [ ] No `DocType` property (external docs are generic reference material)
- [ ] Tenant isolation keys match CompoundDocument pattern (project_name, branch_name, path_hash)
- [ ] Vector embedding configured for 1024 dimensions (mxbai-embed-large)
- [ ] HNSW index with cosine distance configured via attributes
- [ ] Unit tests verify model construction and property defaults
- [ ] Collection setup code registers external_documents collection

---

## Implementation Notes

### 1. ExternalDocument Model

Create the model in `CompoundDocs.Common.Models`:

```csharp
// src/CompoundDocs.Common/Models/ExternalDocument.cs
using Microsoft.SemanticKernel.Data;

namespace CompoundDocs.Common.Models;

/// <summary>
/// Represents a document from external documentation sources (read-only reference material).
/// External documents are stored in a separate collection from CompoundDocument to maintain
/// schema isolation and prevent external docs from appearing in RAG queries for institutional knowledge.
/// </summary>
public sealed class ExternalDocument
{
    /// <summary>
    /// Unique identifier for the document.
    /// </summary>
    [VectorStoreKey(StorageName = "id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    // === Tenant Isolation Keys ===

    /// <summary>
    /// Project name for tenant isolation. Filterable for query scoping.
    /// </summary>
    [VectorStoreData(StorageName = "project_name", IsFilterable = true)]
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>
    /// Git branch name for tenant isolation. Filterable for query scoping.
    /// </summary>
    [VectorStoreData(StorageName = "branch_name", IsFilterable = true)]
    public string BranchName { get; set; } = string.Empty;

    /// <summary>
    /// SHA256 hash of normalized repository path (first 16 chars).
    /// Enables worktree/path isolation within same project/branch.
    /// </summary>
    [VectorStoreData(StorageName = "path_hash", IsFilterable = true)]
    public string PathHash { get; set; } = string.Empty;

    // === Document Metadata ===

    /// <summary>
    /// Relative path to the document within the external docs folder.
    /// </summary>
    [VectorStoreData(StorageName = "relative_path")]
    public string RelativePath { get; set; } = string.Empty;

    /// <summary>
    /// Document title (extracted from first H1 or filename).
    /// </summary>
    [VectorStoreData(StorageName = "title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Optional document summary (extracted from content or frontmatter).
    /// </summary>
    [VectorStoreData(StorageName = "summary")]
    public string? Summary { get; set; }

    /// <summary>
    /// SHA256 hash of document content for change detection.
    /// </summary>
    [VectorStoreData(StorageName = "content_hash")]
    public string ContentHash { get; set; } = string.Empty;

    /// <summary>
    /// Character count of the document content.
    /// </summary>
    [VectorStoreData(StorageName = "char_count")]
    public int CharCount { get; set; }

    // === Vector Embedding ===

    /// <summary>
    /// Vector embedding for semantic search (1024 dimensions from mxbai-embed-large).
    /// Uses HNSW index with cosine distance for efficient similarity search.
    /// </summary>
    [VectorStoreVector(
        Dimensions: 1024,
        DistanceFunction.CosineDistance,
        IndexKind.Hnsw,
        StorageName = "embedding")]
    public ReadOnlyMemory<float>? Embedding { get; set; }
}
```

### 2. Key Differences from CompoundDocument

The `ExternalDocument` model intentionally omits several fields present in `CompoundDocument`:

| Field | CompoundDocument | ExternalDocument | Rationale |
|-------|-----------------|------------------|-----------|
| `DocType` | Yes | **No** | External docs are generic reference material, not categorized |
| `PromotionLevel` | Yes | **No** | External docs are read-only, no promotion workflow |
| `FrontmatterJson` | Yes | **No** | External docs don't require structured frontmatter |

This structural difference reinforces the separation between:
- **CompoundDocument**: Institutional knowledge that evolves through promotion levels
- **ExternalDocument**: Read-only reference material from external sources

### 3. Collection Registration

Add the external documents collection alongside the compound documents collection:

```csharp
// In VectorStoreCollectionFactory or similar
public async Task<IVectorStoreRecordCollection<string, ExternalDocument>> CreateExternalDocumentsCollectionAsync(
    NpgsqlDataSource dataSource,
    CancellationToken cancellationToken = default)
{
    var collection = new PostgresCollection<string, ExternalDocument>(
        dataSource,
        "external_documents",
        ownsDataSource: false
    );

    await collection.EnsureCollectionExistsAsync(cancellationToken);

    return collection;
}
```

### 4. Collection Setup in DI

```csharp
// In ServiceCollectionExtensions.cs
public static IServiceCollection AddExternalDocumentCollection(
    this IServiceCollection services)
{
    services.AddSingleton<IVectorStoreRecordCollection<string, ExternalDocument>>(sp =>
    {
        var dataSource = sp.GetRequiredService<NpgsqlDataSource>();
        return new PostgresCollection<string, ExternalDocument>(
            dataSource,
            "external_documents",
            ownsDataSource: false
        );
    });

    return services;
}
```

### 5. Schema Isolation Benefits

Keeping external documents in a separate collection provides:

1. **Query Isolation**: RAG queries for institutional knowledge won't return external docs
2. **Separate Indexing Lifecycle**: External docs sync based on `external_docs` config, independent of compound docs
3. **Clear Mental Model**: Users understand external docs are reference material, not captured knowledge
4. **Different Relevance Thresholds**: Tools can apply different scoring for external vs. compound docs
5. **Simplified Cleanup**: External docs can be re-indexed without affecting compound docs

### 6. External Source Path Tracking

External documents track their source via the standard tenant isolation keys:

```csharp
// Example: Creating an ExternalDocument from a file
var externalDoc = new ExternalDocument
{
    ProjectName = activeProject.ProjectName,      // From project config
    BranchName = activeProject.BranchName,        // Current git branch
    PathHash = activeProject.PathHash,            // Hash of repo path
    RelativePath = "docs/api/authentication.md",  // Relative to external_docs.path
    Title = "Authentication API",
    ContentHash = ComputeContentHash(content),
    CharCount = content.Length,
    Embedding = await embeddingService.GenerateEmbeddingAsync(content)
};
```

---

## Dependencies

### Depends On

- **Phase 042**: CompoundDocument Model - Establishes the model pattern and collection setup approach

### Blocks

- **Phase 045**: ExternalDocumentChunk Model - Chunks reference the parent ExternalDocument
- **Phase 060+**: External Docs Indexing Tools - Need the collection to store indexed external docs
- **Phase 070+**: External Docs Search Tools - Query the external_documents collection

---

## Testing Verification

### Unit Tests

```csharp
// tests/CompoundDocs.Tests/Models/ExternalDocumentTests.cs
public class ExternalDocumentTests
{
    [Fact]
    public void Constructor_GeneratesUniqueId()
    {
        var doc1 = new ExternalDocument();
        var doc2 = new ExternalDocument();

        Assert.NotEqual(doc1.Id, doc2.Id);
        Assert.True(Guid.TryParse(doc1.Id, out _));
    }

    [Fact]
    public void Constructor_InitializesEmptyStrings()
    {
        var doc = new ExternalDocument();

        Assert.Equal(string.Empty, doc.ProjectName);
        Assert.Equal(string.Empty, doc.BranchName);
        Assert.Equal(string.Empty, doc.PathHash);
        Assert.Equal(string.Empty, doc.RelativePath);
        Assert.Equal(string.Empty, doc.Title);
        Assert.Equal(string.Empty, doc.ContentHash);
        Assert.Equal(0, doc.CharCount);
        Assert.Null(doc.Summary);
        Assert.Null(doc.Embedding);
    }

    [Fact]
    public void Model_HasNoPromotionLevel()
    {
        var properties = typeof(ExternalDocument).GetProperties();
        Assert.DoesNotContain(properties, p => p.Name == "PromotionLevel");
    }

    [Fact]
    public void Model_HasNoDocType()
    {
        var properties = typeof(ExternalDocument).GetProperties();
        Assert.DoesNotContain(properties, p => p.Name == "DocType");
    }

    [Fact]
    public void Model_HasNoFrontmatterJson()
    {
        var properties = typeof(ExternalDocument).GetProperties();
        Assert.DoesNotContain(properties, p => p.Name == "FrontmatterJson");
    }

    [Fact]
    public void VectorStoreKeyAttribute_AppliedToId()
    {
        var idProperty = typeof(ExternalDocument).GetProperty("Id");
        var attribute = idProperty?.GetCustomAttribute<VectorStoreKeyAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal("id", attribute.StorageName);
    }

    [Fact]
    public void VectorStoreVectorAttribute_ConfiguredCorrectly()
    {
        var embeddingProperty = typeof(ExternalDocument).GetProperty("Embedding");
        var attribute = embeddingProperty?.GetCustomAttribute<VectorStoreVectorAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(1024, attribute.Dimensions);
        Assert.Equal(DistanceFunction.CosineDistance, attribute.DistanceFunction);
        Assert.Equal(IndexKind.Hnsw, attribute.IndexKind);
        Assert.Equal("embedding", attribute.StorageName);
    }

    [Theory]
    [InlineData("ProjectName", "project_name", true)]
    [InlineData("BranchName", "branch_name", true)]
    [InlineData("PathHash", "path_hash", true)]
    [InlineData("RelativePath", "relative_path", false)]
    [InlineData("Title", "title", false)]
    [InlineData("Summary", "summary", false)]
    [InlineData("ContentHash", "content_hash", false)]
    [InlineData("CharCount", "char_count", false)]
    public void VectorStoreDataAttributes_ConfiguredCorrectly(
        string propertyName,
        string expectedStorageName,
        bool expectedFilterable)
    {
        var property = typeof(ExternalDocument).GetProperty(propertyName);
        var attribute = property?.GetCustomAttribute<VectorStoreDataAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(expectedStorageName, attribute.StorageName);
        Assert.Equal(expectedFilterable, attribute.IsFilterable);
    }
}
```

### Integration Tests

```csharp
// tests/CompoundDocs.IntegrationTests/Collections/ExternalDocumentCollectionTests.cs
[Trait("Category", "Integration")]
public class ExternalDocumentCollectionTests : IClassFixture<PostgresFixture>
{
    private readonly IVectorStoreRecordCollection<string, ExternalDocument> _collection;

    public ExternalDocumentCollectionTests(PostgresFixture fixture)
    {
        _collection = fixture.GetExternalDocumentsCollection();
    }

    [Fact]
    public async Task Collection_CanUpsertAndRetrieve()
    {
        // Arrange
        var doc = new ExternalDocument
        {
            Id = Guid.NewGuid().ToString(),
            ProjectName = "test-project",
            BranchName = "main",
            PathHash = "abc123",
            RelativePath = "docs/test.md",
            Title = "Test Document",
            ContentHash = "hash123",
            CharCount = 100
        };

        // Act
        await _collection.UpsertAsync(doc);
        var retrieved = await _collection.GetAsync(doc.Id);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(doc.Title, retrieved.Title);
        Assert.Equal(doc.ProjectName, retrieved.ProjectName);
    }

    [Fact]
    public async Task Collection_IsSeparateFromCompoundDocuments()
    {
        // This test verifies that external_documents and documents are separate tables
        // by checking that IDs don't conflict

        var externalDoc = new ExternalDocument
        {
            Id = "shared-test-id",
            ProjectName = "test",
            BranchName = "main",
            PathHash = "hash",
            RelativePath = "external.md",
            Title = "External",
            ContentHash = "hash1",
            CharCount = 50
        };

        await _collection.UpsertAsync(externalDoc);

        // Should be retrievable from external_documents
        var retrieved = await _collection.GetAsync("shared-test-id");
        Assert.NotNull(retrieved);
        Assert.Equal("External", retrieved.Title);
    }
}
```

---

## Files Created/Modified

| File | Action | Description |
|------|--------|-------------|
| `src/CompoundDocs.Common/Models/ExternalDocument.cs` | Create | Semantic Kernel model for external documents |
| `src/CompoundDocs.McpServer/Extensions/ServiceCollectionExtensions.cs` | Modify | Add external documents collection registration |
| `tests/CompoundDocs.Tests/Models/ExternalDocumentTests.cs` | Create | Unit tests for model |
| `tests/CompoundDocs.IntegrationTests/Collections/ExternalDocumentCollectionTests.cs` | Create | Integration tests |

---

## Design Decisions

| Decision | Rationale |
|----------|-----------|
| Separate collection | Prevents external docs from polluting RAG queries for institutional knowledge |
| No PromotionLevel | External docs are read-only reference material, not subject to promotion |
| No DocType | External docs are generic, not categorized like compound docs |
| No FrontmatterJson | External docs don't require structured frontmatter validation |
| Same tenant isolation keys | Enables filtering by project/branch/path just like compound docs |
| Same embedding dimensions | Ensures compatibility with shared embedding service |

---

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| Confusion between collections | Clear naming (`external_documents` vs `documents`) and documentation |
| Missing field parity | Intentional - document the differences explicitly |
| Collection not created | `EnsureCollectionExistsAsync` at startup |
| Query performance | HNSW index with same parameters as compound docs |
