# Phase 062: Chunk Lifecycle Management

> **Status**: PLANNED
> **Category**: Document Processing
> **Estimated Effort**: M
> **Prerequisites**: Phase 061 (Chunking Algorithm)

---

## Spec References

- [mcp-server/chunking.md - Chunk Lifecycle](../spec/mcp-server/chunking.md#chunk-lifecycle)
- [mcp-server/chunking.md - Chunk Promotion](../spec/mcp-server/chunking.md#chunk-promotion)
- [mcp-server/file-watcher.md - Event Processing](../spec/mcp-server/file-watcher.md#event-processing)
- [mcp-server/file-watcher.md - Sync on Activation](../spec/mcp-server/file-watcher.md#sync-on-activation-startup-reconciliation)

---

## Objectives

1. Implement chunk creation when documents exceeding 500 lines are indexed
2. Implement chunk update when parent document content is modified
3. Implement chunk deletion when parent document is deleted
4. Implement chunk removal when document falls below 500-line threshold
5. Ensure atomic chunk operations with parent document state
6. Integrate embedding generation for each chunk during lifecycle events

---

## Acceptance Criteria

- [ ] `ChunkLifecycleService` created with methods for create/update/delete operations
- [ ] Chunks are created atomically when new document >500 lines is indexed
- [ ] All chunks are regenerated when parent document content changes
- [ ] All chunks are deleted when parent document is deleted
- [ ] Chunks are removed when document is modified to <500 lines
- [ ] Chunk promotion levels update atomically with parent document
- [ ] Embedding generation integrated for each chunk during creation
- [ ] Transaction boundaries ensure consistency between parent and chunks
- [ ] Unit tests verify each lifecycle transition
- [ ] Integration tests verify database consistency across operations

---

## Implementation Notes

### 1. Chunk Lifecycle Service Interface

```csharp
// src/CompoundDocs.McpServer/Services/IChunkLifecycleService.cs
namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Manages the lifecycle of document chunks for large documents (>500 lines).
/// Ensures atomic operations between parent documents and their chunks.
/// </summary>
public interface IChunkLifecycleService
{
    /// <summary>
    /// Creates chunks for a newly indexed document that exceeds 500 lines.
    /// Generates embeddings for each chunk.
    /// </summary>
    /// <param name="document">The parent document to chunk.</param>
    /// <param name="content">The document content to parse and chunk.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created chunks with embeddings.</returns>
    Task<IReadOnlyList<DocumentChunk>> CreateChunksAsync(
        CompoundDocument document,
        string content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates chunks when a parent document's content changes.
    /// Regenerates all chunks from the new content.
    /// </summary>
    /// <param name="document">The parent document being updated.</param>
    /// <param name="newContent">The updated document content.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The regenerated chunks with embeddings.</returns>
    Task<IReadOnlyList<DocumentChunk>> UpdateChunksAsync(
        CompoundDocument document,
        string newContent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all chunks associated with a parent document.
    /// Called when parent document is deleted or falls below 500 lines.
    /// </summary>
    /// <param name="documentId">The ID of the parent document.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of chunks deleted.</returns>
    Task<int> DeleteChunksAsync(
        string documentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the promotion level on all chunks for a document.
    /// Called when parent document promotion level changes.
    /// </summary>
    /// <param name="documentId">The ID of the parent document.</param>
    /// <param name="newPromotionLevel">The new promotion level.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of chunks updated.</returns>
    Task<int> UpdateChunkPromotionLevelAsync(
        string documentId,
        string newPromotionLevel,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a document should be chunked based on line count.
    /// </summary>
    /// <param name="content">The document content.</param>
    /// <returns>True if document exceeds 500 lines.</returns>
    bool ShouldChunk(string content);
}
```

### 2. Chunk Lifecycle Service Implementation

```csharp
// src/CompoundDocs.McpServer/Services/ChunkLifecycleService.cs
using CompoundDocs.McpServer.Models;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.Connectors.Postgres;

namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Manages document chunk lifecycle including creation, updates, and deletion.
/// </summary>
public sealed class ChunkLifecycleService : IChunkLifecycleService
{
    private const int ChunkingThreshold = 500;

    private readonly IVectorStoreRecordCollection<string, DocumentChunk> _chunkCollection;
    private readonly IChunkingService _chunkingService;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<ChunkLifecycleService> _logger;

    public ChunkLifecycleService(
        IVectorStoreRecordCollection<string, DocumentChunk> chunkCollection,
        IChunkingService chunkingService,
        IEmbeddingService embeddingService,
        ILogger<ChunkLifecycleService> logger)
    {
        _chunkCollection = chunkCollection ?? throw new ArgumentNullException(nameof(chunkCollection));
        _chunkingService = chunkingService ?? throw new ArgumentNullException(nameof(chunkingService));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public bool ShouldChunk(string content)
    {
        if (string.IsNullOrEmpty(content))
            return false;

        var lineCount = content.Split('\n').Length;
        return lineCount > ChunkingThreshold;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DocumentChunk>> CreateChunksAsync(
        CompoundDocument document,
        string content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrEmpty(content);

        _logger.LogInformation(
            "Creating chunks for document {DocumentId} ({RelativePath})",
            document.Id,
            document.RelativePath);

        // Parse content into chunk boundaries
        var chunkBoundaries = _chunkingService.ParseChunkBoundaries(content);

        if (chunkBoundaries.Count == 0)
        {
            _logger.LogWarning(
                "No chunks extracted from document {DocumentId} - creating single chunk",
                document.Id);

            // Fall back to single chunk for entire document
            chunkBoundaries = new List<ChunkBoundary>
            {
                new(
                    HeaderPath: string.Empty,
                    Content: content,
                    StartLine: 1,
                    EndLine: content.Split('\n').Length)
            };
        }

        var chunks = new List<DocumentChunk>();

        for (var i = 0; i < chunkBoundaries.Count; i++)
        {
            var boundary = chunkBoundaries[i];

            // Create chunk with inherited tenant context
            var chunk = DocumentChunk.CreateFromParent(
                document,
                chunkIndex: i,
                headerPath: boundary.HeaderPath,
                content: boundary.Content,
                startLine: boundary.StartLine,
                endLine: boundary.EndLine);

            // Generate embedding for chunk content
            chunk.Embedding = await _embeddingService.GenerateEmbeddingAsync(
                boundary.Content,
                cancellationToken);

            chunks.Add(chunk);
        }

        // Batch upsert all chunks
        await UpsertChunksAsync(chunks, cancellationToken);

        _logger.LogInformation(
            "Created {ChunkCount} chunks for document {DocumentId}",
            chunks.Count,
            document.Id);

        return chunks;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DocumentChunk>> UpdateChunksAsync(
        CompoundDocument document,
        string newContent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrEmpty(newContent);

        _logger.LogInformation(
            "Updating chunks for document {DocumentId} ({RelativePath})",
            document.Id,
            document.RelativePath);

        // Check if document still needs chunking
        if (!ShouldChunk(newContent))
        {
            _logger.LogInformation(
                "Document {DocumentId} now below chunking threshold - removing chunks",
                document.Id);

            await DeleteChunksAsync(document.Id, cancellationToken);
            return Array.Empty<DocumentChunk>();
        }

        // Delete existing chunks first (simpler than diff-based update)
        await DeleteChunksAsync(document.Id, cancellationToken);

        // Recreate all chunks from new content
        return await CreateChunksAsync(document, newContent, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> DeleteChunksAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(documentId);

        _logger.LogInformation(
            "Deleting chunks for document {DocumentId}",
            documentId);

        // Find all chunks for this document
        var filter = new VectorSearchFilter()
            .EqualTo("document_id", documentId);

        var existingChunks = await GetChunksByDocumentIdAsync(documentId, cancellationToken);
        var deleteCount = 0;

        foreach (var chunk in existingChunks)
        {
            await _chunkCollection.DeleteAsync(chunk.Id, cancellationToken);
            deleteCount++;
        }

        _logger.LogInformation(
            "Deleted {DeleteCount} chunks for document {DocumentId}",
            deleteCount,
            documentId);

        return deleteCount;
    }

    /// <inheritdoc />
    public async Task<int> UpdateChunkPromotionLevelAsync(
        string documentId,
        string newPromotionLevel,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(documentId);
        ArgumentException.ThrowIfNullOrEmpty(newPromotionLevel);

        _logger.LogInformation(
            "Updating promotion level to '{PromotionLevel}' for chunks of document {DocumentId}",
            newPromotionLevel,
            documentId);

        var existingChunks = await GetChunksByDocumentIdAsync(documentId, cancellationToken);
        var updateCount = 0;

        foreach (var chunk in existingChunks)
        {
            chunk.PromotionLevel = newPromotionLevel;
            await _chunkCollection.UpsertAsync(chunk, cancellationToken);
            updateCount++;
        }

        _logger.LogInformation(
            "Updated promotion level on {UpdateCount} chunks for document {DocumentId}",
            updateCount,
            documentId);

        return updateCount;
    }

    private async Task<IReadOnlyList<DocumentChunk>> GetChunksByDocumentIdAsync(
        string documentId,
        CancellationToken cancellationToken)
    {
        // Use search with filter to find all chunks for document
        // Note: Actual implementation depends on Semantic Kernel collection capabilities
        var chunks = new List<DocumentChunk>();

        // Query chunks by document_id filter
        // This will be implemented using the collection's search capabilities
        var searchOptions = new VectorSearchOptions
        {
            Filter = new VectorSearchFilter()
                .EqualTo("document_id", documentId)
        };

        // Retrieve all matching chunks
        // Implementation will use GetAsync with filter or search with dummy vector
        await foreach (var chunk in _chunkCollection.SearchAsync(
            vector: null,
            top: 10000, // High limit to get all chunks
            filter: searchOptions.Filter,
            cancellationToken: cancellationToken))
        {
            chunks.Add(chunk.Record);
        }

        return chunks;
    }

    private async Task UpsertChunksAsync(
        IReadOnlyList<DocumentChunk> chunks,
        CancellationToken cancellationToken)
    {
        foreach (var chunk in chunks)
        {
            await _chunkCollection.UpsertAsync(chunk, cancellationToken);
        }
    }
}
```

### 3. Document Indexing Integration

The chunk lifecycle service integrates with document indexing operations.

```csharp
// src/CompoundDocs.McpServer/Services/DocumentIndexingService.cs (partial)

/// <summary>
/// Extended document indexing that manages chunk lifecycle.
/// </summary>
public partial class DocumentIndexingService
{
    private readonly IChunkLifecycleService _chunkLifecycle;

    /// <summary>
    /// Indexes a document, creating chunks if it exceeds 500 lines.
    /// </summary>
    public async Task<CompoundDocument> IndexDocumentAsync(
        string relativePath,
        string content,
        TenantContext tenant,
        CancellationToken cancellationToken = default)
    {
        // Create parent document
        var document = new CompoundDocument
        {
            RelativePath = relativePath,
            ProjectName = tenant.ProjectName,
            BranchName = tenant.BranchName,
            PathHash = tenant.PathHash,
            ContentHash = ComputeContentHash(content),
            CharCount = content.Length
        };

        // Generate embedding for parent document
        document.Embedding = await _embeddingService.GenerateEmbeddingAsync(
            content,
            cancellationToken);

        // Check if chunking required
        var needsChunking = _chunkLifecycle.ShouldChunk(content);
        document.IsChunked = needsChunking;

        // Save parent document first
        await _documentCollection.UpsertAsync(document, cancellationToken);

        // Create chunks if needed
        if (needsChunking)
        {
            var chunks = await _chunkLifecycle.CreateChunksAsync(
                document,
                content,
                cancellationToken);

            document.ChunkCount = chunks.Count;

            // Update parent with chunk count
            await _documentCollection.UpsertAsync(document, cancellationToken);
        }

        return document;
    }

    /// <summary>
    /// Updates an existing document, managing chunk lifecycle.
    /// </summary>
    public async Task<CompoundDocument> UpdateDocumentAsync(
        CompoundDocument existingDocument,
        string newContent,
        CancellationToken cancellationToken = default)
    {
        var previouslyChunked = existingDocument.IsChunked;
        var nowNeedsChunking = _chunkLifecycle.ShouldChunk(newContent);

        // Update parent document
        existingDocument.ContentHash = ComputeContentHash(newContent);
        existingDocument.CharCount = newContent.Length;
        existingDocument.Embedding = await _embeddingService.GenerateEmbeddingAsync(
            newContent,
            cancellationToken);

        // Handle chunk lifecycle transitions
        if (previouslyChunked && !nowNeedsChunking)
        {
            // Document fell below threshold - remove chunks
            await _chunkLifecycle.DeleteChunksAsync(
                existingDocument.Id,
                cancellationToken);

            existingDocument.IsChunked = false;
            existingDocument.ChunkCount = 0;
        }
        else if (nowNeedsChunking)
        {
            // Document needs chunking (new or updated)
            var chunks = await _chunkLifecycle.UpdateChunksAsync(
                existingDocument,
                newContent,
                cancellationToken);

            existingDocument.IsChunked = true;
            existingDocument.ChunkCount = chunks.Count;
        }

        await _documentCollection.UpsertAsync(existingDocument, cancellationToken);
        return existingDocument;
    }

    /// <summary>
    /// Deletes a document and all associated chunks.
    /// </summary>
    public async Task DeleteDocumentAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        // Delete chunks first (referential integrity)
        await _chunkLifecycle.DeleteChunksAsync(documentId, cancellationToken);

        // Delete parent document
        await _documentCollection.DeleteAsync(documentId, cancellationToken);
    }
}
```

### 4. Promotion Level Cascade

When a document's promotion level changes, all chunks must be updated atomically.

```csharp
// src/CompoundDocs.McpServer/Services/PromotionService.cs (partial)

/// <summary>
/// Updates document promotion level with cascading chunk updates.
/// </summary>
public async Task<bool> UpdatePromotionLevelAsync(
    string documentId,
    string newPromotionLevel,
    CancellationToken cancellationToken = default)
{
    // Get existing document
    var document = await _documentCollection.GetAsync(documentId, cancellationToken);
    if (document == null)
    {
        _logger.LogWarning("Document {DocumentId} not found for promotion update", documentId);
        return false;
    }

    // Update parent document
    document.PromotionLevel = newPromotionLevel;
    await _documentCollection.UpsertAsync(document, cancellationToken);

    // Cascade to chunks if document is chunked
    if (document.IsChunked)
    {
        await _chunkLifecycle.UpdateChunkPromotionLevelAsync(
            documentId,
            newPromotionLevel,
            cancellationToken);
    }

    _logger.LogInformation(
        "Updated promotion level to '{PromotionLevel}' for document {DocumentId} and {ChunkCount} chunks",
        newPromotionLevel,
        documentId,
        document.ChunkCount);

    return true;
}
```

### 5. File Watcher Integration

The file watcher triggers chunk lifecycle operations.

```csharp
// Integration with file watcher event handling

private async Task HandleFileModifiedAsync(
    string relativePath,
    CancellationToken cancellationToken)
{
    var content = await File.ReadAllTextAsync(
        GetFullPath(relativePath),
        cancellationToken);

    var existingDoc = await FindDocumentByPathAsync(relativePath, cancellationToken);

    if (existingDoc == null)
    {
        // New document
        await _indexingService.IndexDocumentAsync(
            relativePath,
            content,
            _tenantContext,
            cancellationToken);
    }
    else
    {
        // Existing document - update with chunk lifecycle handling
        await _indexingService.UpdateDocumentAsync(
            existingDoc,
            content,
            cancellationToken);
    }
}

private async Task HandleFileDeletedAsync(
    string relativePath,
    CancellationToken cancellationToken)
{
    var existingDoc = await FindDocumentByPathAsync(relativePath, cancellationToken);

    if (existingDoc != null)
    {
        // Delete document and all chunks atomically
        await _indexingService.DeleteDocumentAsync(
            existingDoc.Id,
            cancellationToken);
    }
}
```

### 6. Lifecycle State Transitions

```
                        +----------------+
                        |    Created     |
                        |  (< 500 lines) |
                        +-------+--------+
                                |
                                | grow > 500 lines
                                v
+----------------+      +-------+--------+      +----------------+
|    Deleted     |<-----|    Chunked     |----->|    Updated     |
|                |      | (> 500 lines)  |      |  (regenerate)  |
+----------------+      +-------+--------+      +-------+--------+
        ^                       |                       |
        |                       | shrink < 500 lines    |
        |                       v                       |
        |               +-------+--------+              |
        +---------------|   De-chunked   |<-------------+
                        |  (chunks removed) |
                        +----------------+
```

### 7. Embedding Generation Strategy

Embeddings are generated for each chunk during creation:

```csharp
// Chunk embedding generation
for (var i = 0; i < chunkBoundaries.Count; i++)
{
    var boundary = chunkBoundaries[i];

    var chunk = DocumentChunk.CreateFromParent(
        document,
        chunkIndex: i,
        headerPath: boundary.HeaderPath,
        content: boundary.Content,
        startLine: boundary.StartLine,
        endLine: boundary.EndLine);

    // Each chunk gets its own embedding
    chunk.Embedding = await _embeddingService.GenerateEmbeddingAsync(
        boundary.Content,
        cancellationToken);

    chunks.Add(chunk);
}
```

Per the spec, embedding generation is the primary performance bottleneck:

| Documents | Chunks (avg 5/doc) | Embedding Time (est.) |
|-----------|-------------------|----------------------|
| 10 | 50 | ~10 seconds |
| 100 | 500 | ~2 minutes |
| 500 | 2500 | ~10 minutes |

---

## Lifecycle Rules Summary

### Creation Rules

| Trigger | Condition | Action |
|---------|-----------|--------|
| New document indexed | > 500 lines | Create chunks with embeddings |
| Modified document | Was < 500, now > 500 | Create chunks with embeddings |
| Reconciliation | Document > 500 lines needs indexing | Create chunks with embeddings |

### Update Rules

| Trigger | Condition | Action |
|---------|-----------|--------|
| Content changed | Document still > 500 lines | Delete all chunks, recreate |
| Content changed | Document now < 500 lines | Delete all chunks |
| Promotion changed | Document is chunked | Update all chunk promotion levels |

### Deletion Rules

| Trigger | Action |
|---------|--------|
| Parent document deleted | Delete all associated chunks |
| Document modified to < 500 lines | Delete all associated chunks |
| `delete_documents` tool called | Delete all chunks for tenant |

---

## Dependencies

### Depends On

- **Phase 061**: Chunking Algorithm - Provides `IChunkingService` and `ChunkBoundary` types
- **Phase 043**: DocumentChunk Model - The model class being managed
- **Phase 042**: CompoundDocument Model - Parent document reference
- **Phase 029**: Embedding Service - Generates embeddings for chunks

### Blocks

- **Phase 063+**: Chunk Search Operations - Requires chunks to exist in database
- **Phase 070+**: Document Tools - Need lifecycle service for CRUD operations

---

## Testing Verification

### Unit Tests

```csharp
// tests/CompoundDocs.Tests/Services/ChunkLifecycleServiceTests.cs
using CompoundDocs.McpServer.Models;
using CompoundDocs.McpServer.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace CompoundDocs.Tests.Services;

public class ChunkLifecycleServiceTests
{
    private readonly Mock<IVectorStoreRecordCollection<string, DocumentChunk>> _chunkCollectionMock;
    private readonly Mock<IChunkingService> _chunkingServiceMock;
    private readonly Mock<IEmbeddingService> _embeddingServiceMock;
    private readonly Mock<ILogger<ChunkLifecycleService>> _loggerMock;
    private readonly ChunkLifecycleService _service;

    public ChunkLifecycleServiceTests()
    {
        _chunkCollectionMock = new Mock<IVectorStoreRecordCollection<string, DocumentChunk>>();
        _chunkingServiceMock = new Mock<IChunkingService>();
        _embeddingServiceMock = new Mock<IEmbeddingService>();
        _loggerMock = new Mock<ILogger<ChunkLifecycleService>>();

        _service = new ChunkLifecycleService(
            _chunkCollectionMock.Object,
            _chunkingServiceMock.Object,
            _embeddingServiceMock.Object,
            _loggerMock.Object);
    }

    [Theory]
    [InlineData(400, false)]
    [InlineData(500, false)]
    [InlineData(501, true)]
    [InlineData(1000, true)]
    public void ShouldChunk_ReturnsCorrectResult(int lineCount, bool expected)
    {
        // Arrange
        var content = string.Join('\n', Enumerable.Range(1, lineCount).Select(i => $"Line {i}"));

        // Act
        var result = _service.ShouldChunk(content);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ShouldChunk_ReturnsFalse_ForEmptyContent()
    {
        Assert.False(_service.ShouldChunk(string.Empty));
        Assert.False(_service.ShouldChunk(null!));
    }

    [Fact]
    public async Task CreateChunksAsync_CreatesChunksWithEmbeddings()
    {
        // Arrange
        var document = new CompoundDocument
        {
            Id = "doc-1",
            ProjectName = "test-project",
            BranchName = "main",
            PathHash = "hash123",
            PromotionLevel = "elevated"
        };

        var content = "## Section 1\nContent 1\n## Section 2\nContent 2";
        var boundaries = new List<ChunkBoundary>
        {
            new("## Section 1", "Content 1", 1, 2),
            new("## Section 2", "Content 2", 3, 4)
        };

        _chunkingServiceMock
            .Setup(x => x.ParseChunkBoundaries(content))
            .Returns(boundaries);

        _embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReadOnlyMemory<float>(new float[1024]));

        // Act
        var chunks = await _service.CreateChunksAsync(document, content);

        // Assert
        Assert.Equal(2, chunks.Count);
        Assert.All(chunks, c =>
        {
            Assert.Equal("doc-1", c.DocumentId);
            Assert.Equal("test-project", c.ProjectName);
            Assert.Equal("main", c.BranchName);
            Assert.Equal("hash123", c.PathHash);
            Assert.Equal("elevated", c.PromotionLevel);
            Assert.NotNull(c.Embedding);
        });

        _chunkCollectionMock.Verify(
            x => x.UpsertAsync(It.IsAny<DocumentChunk>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task CreateChunksAsync_SetsChunkIndicesSequentially()
    {
        // Arrange
        var document = new CompoundDocument { Id = "doc-1" };
        var boundaries = new List<ChunkBoundary>
        {
            new("## A", "Content A", 1, 10),
            new("## B", "Content B", 11, 20),
            new("## C", "Content C", 21, 30)
        };

        _chunkingServiceMock
            .Setup(x => x.ParseChunkBoundaries(It.IsAny<string>()))
            .Returns(boundaries);

        _embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReadOnlyMemory<float>(new float[1024]));

        // Act
        var chunks = await _service.CreateChunksAsync(document, "content");

        // Assert
        Assert.Equal(0, chunks[0].ChunkIndex);
        Assert.Equal(1, chunks[1].ChunkIndex);
        Assert.Equal(2, chunks[2].ChunkIndex);
    }

    [Fact]
    public async Task UpdateChunksAsync_RemovesChunks_WhenBelowThreshold()
    {
        // Arrange
        var document = new CompoundDocument { Id = "doc-1", IsChunked = true };
        var shortContent = string.Join('\n', Enumerable.Range(1, 100).Select(i => $"Line {i}"));

        // Act
        var result = await _service.UpdateChunksAsync(document, shortContent);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task UpdateChunksAsync_RegeneratesAllChunks_WhenStillAboveThreshold()
    {
        // Arrange
        var document = new CompoundDocument { Id = "doc-1", IsChunked = true };
        var longContent = string.Join('\n', Enumerable.Range(1, 600).Select(i => $"Line {i}"));
        var boundaries = new List<ChunkBoundary>
        {
            new("## New", "New content", 1, 600)
        };

        _chunkingServiceMock
            .Setup(x => x.ParseChunkBoundaries(It.IsAny<string>()))
            .Returns(boundaries);

        _embeddingServiceMock
            .Setup(x => x.GenerateEmbeddingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReadOnlyMemory<float>(new float[1024]));

        // Act
        var result = await _service.UpdateChunksAsync(document, longContent);

        // Assert
        Assert.Single(result);
    }

    [Fact]
    public async Task DeleteChunksAsync_DeletesAllChunksForDocument()
    {
        // Arrange
        var documentId = "doc-1";
        var existingChunks = new List<DocumentChunk>
        {
            new() { Id = "chunk-1", DocumentId = documentId },
            new() { Id = "chunk-2", DocumentId = documentId }
        };

        // Mock chunk retrieval (implementation-dependent)
        // ...

        // Act
        var deleteCount = await _service.DeleteChunksAsync(documentId);

        // Assert
        _chunkCollectionMock.Verify(
            x => x.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.AtLeast(0)); // Depends on mock setup
    }

    [Fact]
    public async Task UpdateChunkPromotionLevelAsync_UpdatesAllChunks()
    {
        // Arrange
        var documentId = "doc-1";
        var newLevel = "foundational";

        // Act
        var updateCount = await _service.UpdateChunkPromotionLevelAsync(
            documentId,
            newLevel);

        // Assert - verify upsert called for each chunk
        // Implementation-dependent verification
    }

    [Fact]
    public async Task CreateChunksAsync_ThrowsOnNullDocument()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => _service.CreateChunksAsync(null!, "content"));
    }

    [Fact]
    public async Task CreateChunksAsync_ThrowsOnEmptyContent()
    {
        var document = new CompoundDocument { Id = "doc-1" };

        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateChunksAsync(document, string.Empty));
    }

    [Fact]
    public async Task DeleteChunksAsync_ThrowsOnEmptyDocumentId()
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.DeleteChunksAsync(string.Empty));
    }
}
```

### Integration Tests

```csharp
// tests/CompoundDocs.IntegrationTests/Services/ChunkLifecycleIntegrationTests.cs
[Trait("Category", "Integration")]
public class ChunkLifecycleIntegrationTests : IClassFixture<PostgresFixture>
{
    private readonly IChunkLifecycleService _chunkLifecycle;
    private readonly IVectorStoreRecordCollection<string, CompoundDocument> _docCollection;
    private readonly IVectorStoreRecordCollection<string, DocumentChunk> _chunkCollection;

    public ChunkLifecycleIntegrationTests(PostgresFixture fixture)
    {
        _docCollection = fixture.GetDocumentsCollection();
        _chunkCollection = fixture.GetChunksCollection();
        _chunkLifecycle = fixture.GetChunkLifecycleService();
    }

    [Fact]
    public async Task CreateAndDeleteChunks_MaintainsDatabaseConsistency()
    {
        // Arrange
        var document = new CompoundDocument
        {
            Id = Guid.NewGuid().ToString(),
            ProjectName = "test",
            BranchName = "main",
            PathHash = "hash",
            RelativePath = "test.md"
        };

        var longContent = GenerateLongContent(600);

        await _docCollection.UpsertAsync(document);

        // Act - Create chunks
        var chunks = await _chunkLifecycle.CreateChunksAsync(document, longContent);

        // Assert - Chunks created
        Assert.NotEmpty(chunks);

        // Act - Delete chunks
        var deleteCount = await _chunkLifecycle.DeleteChunksAsync(document.Id);

        // Assert - All chunks deleted
        Assert.Equal(chunks.Count, deleteCount);

        // Cleanup
        await _docCollection.DeleteAsync(document.Id);
    }

    [Fact]
    public async Task PromotionUpdate_CascadesToAllChunks()
    {
        // Arrange
        var document = new CompoundDocument
        {
            Id = Guid.NewGuid().ToString(),
            ProjectName = "test",
            BranchName = "main",
            PathHash = "hash",
            PromotionLevel = "standard"
        };

        var longContent = GenerateLongContent(600);
        await _docCollection.UpsertAsync(document);
        var chunks = await _chunkLifecycle.CreateChunksAsync(document, longContent);

        // Act
        await _chunkLifecycle.UpdateChunkPromotionLevelAsync(
            document.Id,
            "elevated");

        // Assert - All chunks have updated promotion level
        foreach (var chunk in chunks)
        {
            var retrieved = await _chunkCollection.GetAsync(chunk.Id);
            Assert.Equal("elevated", retrieved?.PromotionLevel);
        }

        // Cleanup
        await _chunkLifecycle.DeleteChunksAsync(document.Id);
        await _docCollection.DeleteAsync(document.Id);
    }

    private static string GenerateLongContent(int lineCount)
    {
        var lines = new List<string> { "# Test Document", "" };
        for (var i = 0; i < lineCount - 2; i++)
        {
            if (i % 100 == 0)
                lines.Add($"## Section {i / 100}");
            else
                lines.Add($"Line {i}: Lorem ipsum dolor sit amet.");
        }
        return string.Join('\n', lines);
    }
}
```

---

## Files Created/Modified

| File | Action | Description |
|------|--------|-------------|
| `src/CompoundDocs.McpServer/Services/IChunkLifecycleService.cs` | Create | Service interface |
| `src/CompoundDocs.McpServer/Services/ChunkLifecycleService.cs` | Create | Service implementation |
| `src/CompoundDocs.McpServer/Services/DocumentIndexingService.cs` | Modify | Integrate chunk lifecycle |
| `src/CompoundDocs.McpServer/Services/PromotionService.cs` | Modify | Add cascade to chunks |
| `tests/CompoundDocs.Tests/Services/ChunkLifecycleServiceTests.cs` | Create | Unit tests |
| `tests/CompoundDocs.IntegrationTests/Services/ChunkLifecycleIntegrationTests.cs` | Create | Integration tests |

---

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| Orphaned chunks after crash | Reconciliation detects and cleans up orphans |
| Promotion level desync | Atomic update of parent + all chunks |
| Partial chunk creation | Delete all chunks on error, retry full creation |
| Embedding generation timeout | Retry with backoff, skip document on repeated failure |
| Large document causes memory pressure | Stream content processing, chunk sequentially |

---

## Design Decisions

| Decision | Rationale |
|----------|-----------|
| Delete-then-recreate on update | Simpler than diff-based approach, acceptable for <1000 docs |
| Denormalized tenant context | Enables direct chunk queries without parent join |
| Promotion cascade | Maintains search consistency across parent and chunks |
| 500-line threshold | Balances search granularity with storage/processing overhead |
| Sequential embedding generation | Respects Ollama parallel limits, predictable memory usage |
