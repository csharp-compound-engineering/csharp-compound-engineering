# Phase 042: CompoundDocument Semantic Kernel Model

> **Status**: PLANNED
> **Category**: Database & Storage
> **Estimated Effort**: S
> **Prerequisites**: Phase 041 (Qdrant Memory Store Setup)

---

## Spec References

- [mcp-server/database-schema.md - Documents Schema](../spec/mcp-server/database-schema.md#documents-schema-semantic-kernel-model)
- [mcp-server/database-schema.md - Multi-Tenant Architecture](../spec/mcp-server/database-schema.md#multi-tenant-architecture)
- [mcp-server/database-schema.md - Embedding Dimensions](../spec/mcp-server/database-schema.md#embedding-dimensions)

---

## Objectives

1. Create `CompoundDocument` class with Semantic Kernel vector store attributes
2. Define key fields for multi-tenant isolation (project_name, branch_name, path_hash)
3. Add metadata fields (title, doc_type, summary, relative_path)
4. Configure embedding vector field with 1024 dimensions for mxbai-embed-large compatibility
5. Set up filterable and searchable column attributes for efficient tenant-scoped queries
6. Implement promotion level field for document prioritization in RAG
7. Add content tracking fields (content_hash, char_count, frontmatter_json)

---

## Acceptance Criteria

- [ ] `CompoundDocument` class created in `CompoundDocs.Common.Models` namespace
- [ ] `[VectorStoreKey]` attribute applied to Id property with storage name mapping
- [ ] Tenant isolation fields (`ProjectName`, `BranchName`, `PathHash`) marked as filterable
- [ ] `DocType` and `PromotionLevel` fields marked as filterable for query filtering
- [ ] Embedding vector configured with 1024 dimensions, cosine distance, and HNSW index
- [ ] All property storage names use snake_case for PostgreSQL compatibility
- [ ] Default values set appropriately (empty strings, null for optional fields)
- [ ] XML documentation comments on all public members
- [ ] Unit tests verify model instantiation and default values

---

## Implementation Notes

### 1. CompoundDocument Model Class

Create the Semantic Kernel vector store model:

```csharp
// src/CompoundDocs.Common/Models/CompoundDocument.cs
using Microsoft.Extensions.VectorData;

namespace CompoundDocs.Common.Models;

/// <summary>
/// Represents a compound document stored in the vector database.
/// Uses Semantic Kernel vector store attributes for automatic schema generation.
/// </summary>
/// <remarks>
/// Multi-tenant isolation is achieved through compound keys:
/// - ProjectName: Identifies the project
/// - BranchName: Identifies the git branch
/// - PathHash: Identifies the specific worktree path
/// </remarks>
public class CompoundDocument
{
    /// <summary>
    /// Unique identifier for the document (GUID string).
    /// </summary>
    [VectorStoreKey(StorageName = "id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    #region Tenant Isolation Keys

    /// <summary>
    /// Project name for multi-tenant isolation.
    /// </summary>
    [VectorStoreData(StorageName = "project_name", IsFilterable = true)]
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>
    /// Git branch name for multi-tenant isolation.
    /// </summary>
    [VectorStoreData(StorageName = "branch_name", IsFilterable = true)]
    public string BranchName { get; set; } = string.Empty;

    /// <summary>
    /// SHA256 hash (first 16 chars) of the normalized absolute path.
    /// Enables support for git worktrees with same project/branch.
    /// </summary>
    [VectorStoreData(StorageName = "path_hash", IsFilterable = true)]
    public string PathHash { get; set; } = string.Empty;

    #endregion

    #region Document Metadata

    /// <summary>
    /// Relative path from the repository root to the document.
    /// </summary>
    [VectorStoreData(StorageName = "relative_path")]
    public string RelativePath { get; set; } = string.Empty;

    /// <summary>
    /// Document title extracted from frontmatter or first heading.
    /// </summary>
    [VectorStoreData(StorageName = "title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Optional summary of the document content.
    /// </summary>
    [VectorStoreData(StorageName = "summary")]
    public string? Summary { get; set; }

    /// <summary>
    /// Document type identifier (e.g., "spec", "adr", "research").
    /// Filterable to enable type-specific queries.
    /// </summary>
    [VectorStoreData(StorageName = "doc_type", IsFilterable = true)]
    public string DocType { get; set; } = string.Empty;

    /// <summary>
    /// Promotion level for RAG prioritization.
    /// Values: "standard" (default), "promoted", "pinned"
    /// </summary>
    [VectorStoreData(StorageName = "promotion_level", IsFilterable = true)]
    public string PromotionLevel { get; set; } = PromotionLevels.Standard;

    #endregion

    #region Content Tracking

    /// <summary>
    /// SHA256 hash of the document content for change detection.
    /// </summary>
    [VectorStoreData(StorageName = "content_hash")]
    public string ContentHash { get; set; } = string.Empty;

    /// <summary>
    /// Character count of the document content.
    /// </summary>
    [VectorStoreData(StorageName = "char_count")]
    public int CharCount { get; set; }

    /// <summary>
    /// JSON-serialized frontmatter metadata from the document.
    /// </summary>
    [VectorStoreData(StorageName = "frontmatter_json")]
    public string? FrontmatterJson { get; set; }

    #endregion

    #region Vector Embedding

    /// <summary>
    /// Vector embedding generated by mxbai-embed-large (1024 dimensions).
    /// Uses cosine distance with HNSW index for efficient similarity search.
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

### 2. Promotion Levels Constants

Define promotion level constants for type safety:

```csharp
// src/CompoundDocs.Common/Models/PromotionLevels.cs
namespace CompoundDocs.Common.Models;

/// <summary>
/// Defines valid promotion levels for compound documents.
/// Higher promotion levels receive priority in RAG queries.
/// </summary>
public static class PromotionLevels
{
    /// <summary>
    /// Standard documents appear in RAG results based on similarity score.
    /// </summary>
    public const string Standard = "standard";

    /// <summary>
    /// Promoted documents receive a boost in RAG ranking.
    /// </summary>
    public const string Promoted = "promoted";

    /// <summary>
    /// Pinned documents always appear at the top of RAG results.
    /// </summary>
    public const string Pinned = "pinned";

    /// <summary>
    /// All valid promotion levels.
    /// </summary>
    public static readonly IReadOnlyList<string> All = new[]
    {
        Standard,
        Promoted,
        Pinned
    };

    /// <summary>
    /// Validates that a promotion level string is valid.
    /// </summary>
    public static bool IsValid(string level) =>
        All.Contains(level, StringComparer.OrdinalIgnoreCase);
}
```

### 3. Document Type Constants

Define document type constants:

```csharp
// src/CompoundDocs.Common/Models/DocumentTypes.cs
namespace CompoundDocs.Common.Models;

/// <summary>
/// Defines valid document types for compound documents.
/// </summary>
public static class DocumentTypes
{
    /// <summary>
    /// Specification documents defining system behavior.
    /// </summary>
    public const string Spec = "spec";

    /// <summary>
    /// Architecture Decision Records.
    /// </summary>
    public const string Adr = "adr";

    /// <summary>
    /// Research documents with background information.
    /// </summary>
    public const string Research = "research";

    /// <summary>
    /// General documentation.
    /// </summary>
    public const string Doc = "doc";

    /// <summary>
    /// All valid document types.
    /// </summary>
    public static readonly IReadOnlyList<string> All = new[]
    {
        Spec,
        Adr,
        Research,
        Doc
    };

    /// <summary>
    /// Validates that a document type string is valid.
    /// </summary>
    public static bool IsValid(string type) =>
        All.Contains(type, StringComparer.OrdinalIgnoreCase);
}
```

### 4. Embedding Dimensions Constant

Define embedding configuration:

```csharp
// src/CompoundDocs.Common/Constants/EmbeddingConfiguration.cs
namespace CompoundDocs.Common.Constants;

/// <summary>
/// Configuration constants for vector embeddings.
/// </summary>
public static class EmbeddingConfiguration
{
    /// <summary>
    /// Expected dimensions for mxbai-embed-large embeddings.
    /// </summary>
    public const int Dimensions = 1024;

    /// <summary>
    /// The embedding model used for generating vectors.
    /// </summary>
    public const string ModelId = "mxbai-embed-large";
}
```

---

## Dependencies

### Depends On

- **Phase 041**: Qdrant Memory Store Setup - PostgresCollection infrastructure

### Blocks

- **Phase 043**: DocumentChunk Model - Chunk model references CompoundDocument
- **Phase 044**: Document Repository - CRUD operations on CompoundDocument
- **Phase 045+**: Document Indexing - Uses CompoundDocument for storage

---

## Testing Verification

### Unit Tests

```csharp
// tests/CompoundDocs.Tests/Models/CompoundDocumentTests.cs
using CompoundDocs.Common.Models;

namespace CompoundDocs.Tests.Models;

public class CompoundDocumentTests
{
    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        // Act
        var doc = new CompoundDocument();

        // Assert
        Assert.NotNull(doc.Id);
        Assert.True(Guid.TryParse(doc.Id, out _), "Id should be a valid GUID");
        Assert.Equal(string.Empty, doc.ProjectName);
        Assert.Equal(string.Empty, doc.BranchName);
        Assert.Equal(string.Empty, doc.PathHash);
        Assert.Equal(string.Empty, doc.RelativePath);
        Assert.Equal(string.Empty, doc.Title);
        Assert.Null(doc.Summary);
        Assert.Equal(string.Empty, doc.DocType);
        Assert.Equal(PromotionLevels.Standard, doc.PromotionLevel);
        Assert.Equal(string.Empty, doc.ContentHash);
        Assert.Equal(0, doc.CharCount);
        Assert.Null(doc.FrontmatterJson);
        Assert.Null(doc.Embedding);
    }

    [Fact]
    public void Id_IsUnique_AcrossInstances()
    {
        // Arrange & Act
        var doc1 = new CompoundDocument();
        var doc2 = new CompoundDocument();

        // Assert
        Assert.NotEqual(doc1.Id, doc2.Id);
    }

    [Fact]
    public void Embedding_CanBeSet_WithCorrectDimensions()
    {
        // Arrange
        var doc = new CompoundDocument();
        var embedding = new float[1024];
        Array.Fill(embedding, 0.5f);

        // Act
        doc.Embedding = new ReadOnlyMemory<float>(embedding);

        // Assert
        Assert.NotNull(doc.Embedding);
        Assert.Equal(1024, doc.Embedding.Value.Length);
    }

    [Theory]
    [InlineData("standard")]
    [InlineData("promoted")]
    [InlineData("pinned")]
    public void PromotionLevel_AcceptsValidValues(string level)
    {
        // Arrange
        var doc = new CompoundDocument();

        // Act
        doc.PromotionLevel = level;

        // Assert
        Assert.Equal(level, doc.PromotionLevel);
    }
}
```

### Promotion Levels Tests

```csharp
// tests/CompoundDocs.Tests/Models/PromotionLevelsTests.cs
using CompoundDocs.Common.Models;

namespace CompoundDocs.Tests.Models;

public class PromotionLevelsTests
{
    [Theory]
    [InlineData("standard", true)]
    [InlineData("promoted", true)]
    [InlineData("pinned", true)]
    [InlineData("Standard", true)]  // Case insensitive
    [InlineData("PROMOTED", true)]
    [InlineData("invalid", false)]
    [InlineData("", false)]
    public void IsValid_ReturnsExpectedResult(string level, bool expected)
    {
        // Act
        var result = PromotionLevels.IsValid(level);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void All_ContainsAllDefinedLevels()
    {
        // Assert
        Assert.Contains(PromotionLevels.Standard, PromotionLevels.All);
        Assert.Contains(PromotionLevels.Promoted, PromotionLevels.All);
        Assert.Contains(PromotionLevels.Pinned, PromotionLevels.All);
        Assert.Equal(3, PromotionLevels.All.Count);
    }
}
```

### Attribute Verification Tests

```csharp
// tests/CompoundDocs.Tests/Models/CompoundDocumentAttributeTests.cs
using System.Reflection;
using CompoundDocs.Common.Models;
using Microsoft.Extensions.VectorData;

namespace CompoundDocs.Tests.Models;

public class CompoundDocumentAttributeTests
{
    [Fact]
    public void Id_HasVectorStoreKeyAttribute()
    {
        // Arrange
        var property = typeof(CompoundDocument).GetProperty(nameof(CompoundDocument.Id));

        // Act
        var attribute = property?.GetCustomAttribute<VectorStoreKeyAttribute>();

        // Assert
        Assert.NotNull(attribute);
        Assert.Equal("id", attribute.StorageName);
    }

    [Theory]
    [InlineData(nameof(CompoundDocument.ProjectName), "project_name", true)]
    [InlineData(nameof(CompoundDocument.BranchName), "branch_name", true)]
    [InlineData(nameof(CompoundDocument.PathHash), "path_hash", true)]
    [InlineData(nameof(CompoundDocument.DocType), "doc_type", true)]
    [InlineData(nameof(CompoundDocument.PromotionLevel), "promotion_level", true)]
    [InlineData(nameof(CompoundDocument.RelativePath), "relative_path", false)]
    [InlineData(nameof(CompoundDocument.Title), "title", false)]
    public void DataProperties_HaveCorrectAttributes(
        string propertyName,
        string expectedStorageName,
        bool expectedFilterable)
    {
        // Arrange
        var property = typeof(CompoundDocument).GetProperty(propertyName);

        // Act
        var attribute = property?.GetCustomAttribute<VectorStoreDataAttribute>();

        // Assert
        Assert.NotNull(attribute);
        Assert.Equal(expectedStorageName, attribute.StorageName);
        Assert.Equal(expectedFilterable, attribute.IsFilterable);
    }

    [Fact]
    public void Embedding_HasCorrectVectorAttributes()
    {
        // Arrange
        var property = typeof(CompoundDocument).GetProperty(nameof(CompoundDocument.Embedding));

        // Act
        var attribute = property?.GetCustomAttribute<VectorStoreVectorAttribute>();

        // Assert
        Assert.NotNull(attribute);
        Assert.Equal(1024, attribute.Dimensions);
        Assert.Equal(DistanceFunction.CosineDistance, attribute.DistanceFunction);
        Assert.Equal(IndexKind.Hnsw, attribute.IndexKind);
        Assert.Equal("embedding", attribute.StorageName);
    }
}
```

---

## Files Created/Modified

| File | Action | Description |
|------|--------|-------------|
| `src/CompoundDocs.Common/Models/CompoundDocument.cs` | Create | Main vector store model |
| `src/CompoundDocs.Common/Models/PromotionLevels.cs` | Create | Promotion level constants |
| `src/CompoundDocs.Common/Models/DocumentTypes.cs` | Create | Document type constants |
| `src/CompoundDocs.Common/Constants/EmbeddingConfiguration.cs` | Create | Embedding dimension constants |
| `tests/CompoundDocs.Tests/Models/CompoundDocumentTests.cs` | Create | Model unit tests |
| `tests/CompoundDocs.Tests/Models/PromotionLevelsTests.cs` | Create | Promotion level tests |
| `tests/CompoundDocs.Tests/Models/CompoundDocumentAttributeTests.cs` | Create | Attribute verification tests |

---

## Vector Store Attribute Reference

| Attribute | Property | Settings | Purpose |
|-----------|----------|----------|---------|
| `[VectorStoreKey]` | Id | `StorageName = "id"` | Primary key |
| `[VectorStoreData]` | ProjectName | `IsFilterable = true` | Tenant isolation |
| `[VectorStoreData]` | BranchName | `IsFilterable = true` | Branch isolation |
| `[VectorStoreData]` | PathHash | `IsFilterable = true` | Worktree isolation |
| `[VectorStoreData]` | DocType | `IsFilterable = true` | Type-based queries |
| `[VectorStoreData]` | PromotionLevel | `IsFilterable = true` | RAG prioritization |
| `[VectorStoreVector]` | Embedding | `Dimensions: 1024, CosineDistance, Hnsw` | Similarity search |

---

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| Dimension mismatch with embedding model | Constant shared with embedding service; validated at startup |
| Invalid promotion levels | Constants class with validation method |
| Storage name casing issues | Explicit snake_case in all StorageName attributes |
| Missing filterable attributes | Comprehensive attribute tests verify configuration |
| Null reference on optional fields | Nullable annotations and null defaults |
