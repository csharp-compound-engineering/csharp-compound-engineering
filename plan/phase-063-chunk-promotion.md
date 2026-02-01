# Phase 063: Chunk Promotion Inheritance

> **Status**: PLANNED
> **Category**: Document Processing
> **Estimated Effort**: S
> **Prerequisites**: Phase 062 (Document Chunk Model), Phase 052 (Update Promotion Level Tool)

---

## Spec References

- [spec/mcp-server/chunking.md - Chunk Promotion](../spec/mcp-server/chunking.md#chunk-promotion)
- [spec/mcp-server/tools.md - Update Promotion Level Tool](../spec/mcp-server/tools.md#8-update-promotion-level-tool)

---

## Objectives

1. Implement promotion level inheritance from parent document to all associated chunks
2. Create atomic update mechanism for promotion level propagation
3. Enforce prohibition of independent chunk promotion
4. Implement consistency validation between parent document and chunk promotion levels
5. Ensure transactional integrity when promotion levels change

---

## Acceptance Criteria

- [ ] Chunks automatically inherit `promotion_level` from parent document on creation
- [ ] `update_promotion_level` tool atomically updates parent and all chunks in single transaction
- [ ] Chunks cannot be promoted independently (no direct chunk promotion API)
- [ ] Consistency validation detects parent/chunk promotion level mismatches
- [ ] Reconciliation can fix inconsistent promotion levels
- [ ] Unit tests verify inheritance on chunk creation
- [ ] Integration tests verify atomic promotion propagation
- [ ] Performance benchmarks for bulk chunk promotion updates

---

## Implementation Notes

### 1. Promotion Level Inheritance on Chunk Creation

When chunks are created during document indexing, they inherit the parent's promotion level:

```csharp
// src/CompoundDocs.McpServer/Services/ChunkingService.cs
public class ChunkingService : IChunkingService
{
    public async Task<IReadOnlyList<DocumentChunk>> CreateChunksAsync(
        CompoundDocument parentDocument,
        string content,
        CancellationToken cancellationToken = default)
    {
        var chunks = new List<DocumentChunk>();
        var chunkBoundaries = _markdownParser.IdentifyChunkBoundaries(content);

        foreach (var boundary in chunkBoundaries)
        {
            var chunk = new DocumentChunk
            {
                DocumentId = parentDocument.Id,
                ProjectName = parentDocument.ProjectName,
                BranchName = parentDocument.BranchName,
                PathHash = parentDocument.PathHash,
                HeaderPath = boundary.HeaderPath,
                StartLine = boundary.StartLine,
                EndLine = boundary.EndLine,
                Content = boundary.Content,
                // Inherit promotion level from parent
                PromotionLevel = parentDocument.PromotionLevel,
                Embedding = await _embeddingService.GenerateEmbeddingAsync(
                    boundary.Content, cancellationToken)
            };

            chunks.Add(chunk);
        }

        return chunks;
    }
}
```

### 2. Atomic Promotion Level Update

The `update_promotion_level` tool must update the parent document and all chunks atomically:

```csharp
// src/CompoundDocs.McpServer/Services/PromotionService.cs
public class PromotionService : IPromotionService
{
    private readonly IVectorStoreRecordCollection<string, CompoundDocument> _documents;
    private readonly IVectorStoreRecordCollection<string, DocumentChunk> _chunks;
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<PromotionService> _logger;

    public async Task<PromotionResult> UpdatePromotionLevelAsync(
        string documentId,
        PromotionLevel newLevel,
        CancellationToken cancellationToken = default)
    {
        // Use explicit transaction for atomicity
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            // 1. Get and update parent document
            var document = await _documents.GetAsync(documentId, cancellationToken);
            if (document == null)
            {
                throw new DocumentNotFoundException(documentId);
            }

            var previousLevel = document.PromotionLevel;
            document.PromotionLevel = newLevel;

            // 2. Update parent document
            await _documents.UpsertAsync(document, cancellationToken);

            // 3. Update all associated chunks atomically
            var chunksUpdated = await UpdateChunkPromotionLevelsAsync(
                documentId,
                newLevel,
                connection,
                transaction,
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Promoted document {DocumentId} from {PreviousLevel} to {NewLevel}, updated {ChunkCount} chunks",
                documentId, previousLevel, newLevel, chunksUpdated);

            return new PromotionResult
            {
                DocumentId = documentId,
                PreviousLevel = previousLevel,
                NewLevel = newLevel,
                ChunksUpdated = chunksUpdated
            };
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Failed to update promotion level for document {DocumentId}", documentId);
            throw;
        }
    }

    private async Task<int> UpdateChunkPromotionLevelsAsync(
        string documentId,
        PromotionLevel newLevel,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        // Direct SQL for efficient bulk update within transaction
        const string sql = @"
            UPDATE document_chunks
            SET promotion_level = @newLevel
            WHERE document_id = @documentId";

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@newLevel", newLevel.ToString().ToLowerInvariant());
        command.Parameters.AddWithValue("@documentId", documentId);

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
```

### 3. Promotion Result Model

```csharp
// src/CompoundDocs.Common/Models/PromotionResult.cs
namespace CompoundDocs.Common.Models;

/// <summary>
/// Result of a promotion level update operation.
/// </summary>
public sealed class PromotionResult
{
    /// <summary>
    /// ID of the document that was promoted.
    /// </summary>
    public required string DocumentId { get; init; }

    /// <summary>
    /// Previous promotion level before the update.
    /// </summary>
    public required PromotionLevel PreviousLevel { get; init; }

    /// <summary>
    /// New promotion level after the update.
    /// </summary>
    public required PromotionLevel NewLevel { get; init; }

    /// <summary>
    /// Number of chunks that were updated along with the parent document.
    /// </summary>
    public int ChunksUpdated { get; init; }
}
```

### 4. No Independent Chunk Promotion

The chunk promotion is enforced by design:

1. **No direct chunk promotion API**: The `IPromotionService` interface only exposes document-level promotion
2. **Chunk model is read-only for promotion**: The `DocumentChunk.PromotionLevel` is set only via:
   - Initial inheritance on creation
   - Parent document promotion updates
3. **MCP tools don't expose chunk promotion**: The `update_promotion_level` tool only accepts document paths

```csharp
// src/CompoundDocs.McpServer/Interfaces/IPromotionService.cs
namespace CompoundDocs.McpServer.Interfaces;

/// <summary>
/// Service for managing document promotion levels.
/// Chunks inherit promotion from parent documents and cannot be promoted independently.
/// </summary>
public interface IPromotionService
{
    /// <summary>
    /// Updates the promotion level of a document and all its chunks atomically.
    /// </summary>
    /// <param name="documentId">The document ID to promote.</param>
    /// <param name="newLevel">The new promotion level.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result including previous level, new level, and chunks updated.</returns>
    Task<PromotionResult> UpdatePromotionLevelAsync(
        string documentId,
        PromotionLevel newLevel,
        CancellationToken cancellationToken = default);

    // NOTE: No UpdateChunkPromotionLevelAsync - chunks cannot be promoted independently
}
```

### 5. Consistency Validation

Add validation to detect promotion level mismatches during reconciliation:

```csharp
// src/CompoundDocs.McpServer/Services/ConsistencyValidator.cs
public class ConsistencyValidator : IConsistencyValidator
{
    public async Task<IReadOnlyList<PromotionInconsistency>> FindPromotionInconsistenciesAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = @"
            SELECT
                d.id AS document_id,
                d.relative_path,
                d.promotion_level AS document_level,
                c.id AS chunk_id,
                c.promotion_level AS chunk_level
            FROM documents d
            INNER JOIN document_chunks c ON d.id = c.document_id
            WHERE d.promotion_level != c.promotion_level";

        var inconsistencies = new List<PromotionInconsistency>();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            inconsistencies.Add(new PromotionInconsistency
            {
                DocumentId = reader.GetString(0),
                DocumentPath = reader.GetString(1),
                DocumentLevel = Enum.Parse<PromotionLevel>(reader.GetString(2), ignoreCase: true),
                ChunkId = reader.GetString(3),
                ChunkLevel = Enum.Parse<PromotionLevel>(reader.GetString(4), ignoreCase: true)
            });
        }

        return inconsistencies;
    }

    public async Task<int> FixPromotionInconsistenciesAsync(
        CancellationToken cancellationToken = default)
    {
        // Fix by updating chunks to match their parent document
        const string sql = @"
            UPDATE document_chunks c
            SET promotion_level = d.promotion_level
            FROM documents d
            WHERE c.document_id = d.id
            AND c.promotion_level != d.promotion_level";

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
```

### 6. Inconsistency Model

```csharp
// src/CompoundDocs.Common/Models/PromotionInconsistency.cs
namespace CompoundDocs.Common.Models;

/// <summary>
/// Represents a promotion level inconsistency between a document and one of its chunks.
/// </summary>
public sealed class PromotionInconsistency
{
    public required string DocumentId { get; init; }
    public required string DocumentPath { get; init; }
    public required PromotionLevel DocumentLevel { get; init; }
    public required string ChunkId { get; init; }
    public required PromotionLevel ChunkLevel { get; init; }
}
```

### 7. Integration with Update Promotion Level Tool

Update the MCP tool handler to use the promotion service:

```csharp
// src/CompoundDocs.McpServer/Tools/UpdatePromotionLevelTool.cs
[Tool("update_promotion_level")]
[Description("Update the promotion level of a document (standard, important, critical)")]
public class UpdatePromotionLevelTool
{
    private readonly IPromotionService _promotionService;
    private readonly IDocumentRepository _documentRepository;
    private readonly IMarkdownFileService _markdownFileService;

    public async Task<UpdatePromotionLevelResponse> ExecuteAsync(
        [Description("Relative path to document within ./csharp-compounding-docs/")]
        string documentPath,
        [Description("New level: standard, important, or critical")]
        string promotionLevel,
        CancellationToken cancellationToken = default)
    {
        // Validate promotion level
        if (!Enum.TryParse<PromotionLevel>(promotionLevel, ignoreCase: true, out var newLevel))
        {
            return new UpdatePromotionLevelResponse
            {
                Error = true,
                Code = "INVALID_PROMOTION_LEVEL",
                Message = $"Invalid promotion level: {promotionLevel}. Must be standard, important, or critical."
            };
        }

        // Get document by path
        var document = await _documentRepository.GetByPathAsync(documentPath, cancellationToken);
        if (document == null)
        {
            return new UpdatePromotionLevelResponse
            {
                Error = true,
                Code = "DOCUMENT_NOT_FOUND",
                Message = $"Document not found: {documentPath}"
            };
        }

        // Update frontmatter in file
        await _markdownFileService.UpdateFrontmatterAsync(
            documentPath,
            "promotion_level",
            newLevel.ToString().ToLowerInvariant(),
            cancellationToken);

        // Update database (document + chunks atomically)
        var result = await _promotionService.UpdatePromotionLevelAsync(
            document.Id,
            newLevel,
            cancellationToken);

        return new UpdatePromotionLevelResponse
        {
            Status = "updated",
            DocumentPath = documentPath,
            PreviousLevel = result.PreviousLevel.ToString().ToLowerInvariant(),
            NewLevel = result.NewLevel.ToString().ToLowerInvariant(),
            ChunksUpdated = result.ChunksUpdated
        };
    }
}
```

---

## Dependencies

### Depends On

- **Phase 062**: Document Chunk Model - Defines the `DocumentChunk` model with `PromotionLevel` field
- **Phase 052**: Update Promotion Level Tool - MCP tool that triggers promotion updates

### Blocks

- **Phase 064+**: Promotion Level Search Filtering - Queries that filter by promotion level across documents and chunks
- **Phase 070+**: RAG Query with Critical Docs - Critical document prepending relies on consistent promotion levels

---

## Testing Verification

### Unit Tests

```csharp
// tests/CompoundDocs.Tests/Services/PromotionServiceTests.cs
public class PromotionServiceTests
{
    [Fact]
    public async Task UpdatePromotionLevelAsync_UpdatesDocumentAndChunks()
    {
        // Arrange
        var document = new CompoundDocument
        {
            Id = "doc-1",
            PromotionLevel = PromotionLevel.Standard
        };

        var chunks = new List<DocumentChunk>
        {
            new() { Id = "chunk-1", DocumentId = "doc-1", PromotionLevel = PromotionLevel.Standard },
            new() { Id = "chunk-2", DocumentId = "doc-1", PromotionLevel = PromotionLevel.Standard }
        };

        _mockDocuments.Setup(d => d.GetAsync("doc-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        // Act
        var result = await _service.UpdatePromotionLevelAsync(
            "doc-1",
            PromotionLevel.Critical,
            CancellationToken.None);

        // Assert
        Assert.Equal(PromotionLevel.Standard, result.PreviousLevel);
        Assert.Equal(PromotionLevel.Critical, result.NewLevel);
        Assert.Equal(2, result.ChunksUpdated);
    }

    [Fact]
    public async Task UpdatePromotionLevelAsync_RollsBackOnFailure()
    {
        // Arrange
        var document = new CompoundDocument
        {
            Id = "doc-1",
            PromotionLevel = PromotionLevel.Standard
        };

        _mockDocuments.Setup(d => d.GetAsync("doc-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        _mockChunkUpdate.Setup(c => c.ExecuteNonQueryAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Database error"));

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() =>
            _service.UpdatePromotionLevelAsync("doc-1", PromotionLevel.Critical, CancellationToken.None));

        // Verify document was not persisted (rollback occurred)
        _mockDocuments.Verify(d => d.UpsertAsync(It.IsAny<CompoundDocument>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdatePromotionLevelAsync_ThrowsForNonexistentDocument()
    {
        // Arrange
        _mockDocuments.Setup(d => d.GetAsync("missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync((CompoundDocument?)null);

        // Act & Assert
        await Assert.ThrowsAsync<DocumentNotFoundException>(() =>
            _service.UpdatePromotionLevelAsync("missing", PromotionLevel.Critical, CancellationToken.None));
    }
}
```

### Chunk Creation Inheritance Tests

```csharp
// tests/CompoundDocs.Tests/Services/ChunkingServiceTests.cs
public class ChunkingServiceTests
{
    [Fact]
    public async Task CreateChunksAsync_InheritsPromotionLevelFromParent()
    {
        // Arrange
        var parentDocument = new CompoundDocument
        {
            Id = "doc-1",
            ProjectName = "test-project",
            BranchName = "main",
            PathHash = "abc123",
            PromotionLevel = PromotionLevel.Important
        };

        var content = "## Section 1\nContent\n## Section 2\nMore content";

        // Act
        var chunks = await _service.CreateChunksAsync(parentDocument, content, CancellationToken.None);

        // Assert
        Assert.All(chunks, chunk =>
        {
            Assert.Equal(PromotionLevel.Important, chunk.PromotionLevel);
            Assert.Equal(parentDocument.Id, chunk.DocumentId);
            Assert.Equal(parentDocument.ProjectName, chunk.ProjectName);
        });
    }

    [Theory]
    [InlineData(PromotionLevel.Standard)]
    [InlineData(PromotionLevel.Important)]
    [InlineData(PromotionLevel.Critical)]
    public async Task CreateChunksAsync_InheritsAnyPromotionLevel(PromotionLevel level)
    {
        // Arrange
        var parentDocument = new CompoundDocument
        {
            Id = "doc-1",
            PromotionLevel = level
        };

        // Act
        var chunks = await _service.CreateChunksAsync(parentDocument, "## Test\nContent", CancellationToken.None);

        // Assert
        Assert.All(chunks, chunk => Assert.Equal(level, chunk.PromotionLevel));
    }
}
```

### Consistency Validation Tests

```csharp
// tests/CompoundDocs.Tests/Services/ConsistencyValidatorTests.cs
public class ConsistencyValidatorTests
{
    [Fact]
    public async Task FindPromotionInconsistenciesAsync_DetectsMismatchedLevels()
    {
        // Arrange - set up database with inconsistent data
        await InsertDocumentAsync("doc-1", PromotionLevel.Critical);
        await InsertChunkAsync("chunk-1", "doc-1", PromotionLevel.Standard); // Mismatch!

        // Act
        var inconsistencies = await _validator.FindPromotionInconsistenciesAsync(CancellationToken.None);

        // Assert
        Assert.Single(inconsistencies);
        Assert.Equal("doc-1", inconsistencies[0].DocumentId);
        Assert.Equal(PromotionLevel.Critical, inconsistencies[0].DocumentLevel);
        Assert.Equal(PromotionLevel.Standard, inconsistencies[0].ChunkLevel);
    }

    [Fact]
    public async Task FixPromotionInconsistenciesAsync_UpdatesChunksToMatchParent()
    {
        // Arrange
        await InsertDocumentAsync("doc-1", PromotionLevel.Critical);
        await InsertChunkAsync("chunk-1", "doc-1", PromotionLevel.Standard);
        await InsertChunkAsync("chunk-2", "doc-1", PromotionLevel.Important);

        // Act
        var fixedCount = await _validator.FixPromotionInconsistenciesAsync(CancellationToken.None);

        // Assert
        Assert.Equal(2, fixedCount);

        var chunk1 = await GetChunkAsync("chunk-1");
        var chunk2 = await GetChunkAsync("chunk-2");
        Assert.Equal(PromotionLevel.Critical, chunk1.PromotionLevel);
        Assert.Equal(PromotionLevel.Critical, chunk2.PromotionLevel);
    }
}
```

### Integration Tests

```csharp
// tests/CompoundDocs.IntegrationTests/Services/PromotionServiceIntegrationTests.cs
[Trait("Category", "Integration")]
public class PromotionServiceIntegrationTests : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task UpdatePromotionLevelAsync_AtomicUpdateWithChunks()
    {
        // Arrange
        var document = await CreateDocumentWithChunksAsync(
            promotionLevel: PromotionLevel.Standard,
            chunkCount: 5);

        // Act
        var result = await _promotionService.UpdatePromotionLevelAsync(
            document.Id,
            PromotionLevel.Critical,
            CancellationToken.None);

        // Assert
        Assert.Equal(5, result.ChunksUpdated);

        var updatedDoc = await _documents.GetAsync(document.Id);
        Assert.Equal(PromotionLevel.Critical, updatedDoc!.PromotionLevel);

        var chunks = await GetChunksByDocumentIdAsync(document.Id);
        Assert.All(chunks, chunk => Assert.Equal(PromotionLevel.Critical, chunk.PromotionLevel));
    }

    [Fact]
    public async Task UpdatePromotionLevelAsync_TransactionRollbackOnFailure()
    {
        // This test verifies that if chunk update fails, the document update is rolled back
        // Requires simulating a database failure mid-transaction
    }
}
```

---

## Files Created/Modified

| File | Action | Description |
|------|--------|-------------|
| `src/CompoundDocs.McpServer/Services/PromotionService.cs` | Create | Service for atomic promotion level updates |
| `src/CompoundDocs.McpServer/Interfaces/IPromotionService.cs` | Create | Interface for promotion service |
| `src/CompoundDocs.Common/Models/PromotionResult.cs` | Create | Result model for promotion operations |
| `src/CompoundDocs.Common/Models/PromotionInconsistency.cs` | Create | Model for inconsistency detection |
| `src/CompoundDocs.McpServer/Services/ConsistencyValidator.cs` | Create | Validation for promotion level consistency |
| `src/CompoundDocs.McpServer/Interfaces/IConsistencyValidator.cs` | Create | Interface for consistency validation |
| `src/CompoundDocs.McpServer/Services/ChunkingService.cs` | Modify | Add promotion level inheritance on chunk creation |
| `src/CompoundDocs.McpServer/Tools/UpdatePromotionLevelTool.cs` | Modify | Integrate with PromotionService |
| `tests/CompoundDocs.Tests/Services/PromotionServiceTests.cs` | Create | Unit tests for promotion service |
| `tests/CompoundDocs.Tests/Services/ChunkingServiceTests.cs` | Modify | Add inheritance tests |
| `tests/CompoundDocs.Tests/Services/ConsistencyValidatorTests.cs` | Create | Unit tests for consistency validation |
| `tests/CompoundDocs.IntegrationTests/Services/PromotionServiceIntegrationTests.cs` | Create | Integration tests |

---

## Design Decisions

| Decision | Rationale |
|----------|-----------|
| Atomic transaction for promotion | Ensures parent and all chunks are always in sync |
| Direct SQL for bulk chunk updates | More efficient than individual Semantic Kernel operations |
| No independent chunk promotion API | Enforces inheritance model by design, not just convention |
| Consistency validation as separate service | Can be run during reconciliation or on-demand |
| Fix inconsistencies by updating chunks | Parent document is the source of truth for promotion level |
| Inheritance on creation, not reference | Chunks store their own promotion level for query efficiency |

---

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| Transaction deadlocks | Use short transactions, consistent lock ordering |
| Large chunk counts slow updates | Direct SQL bulk update, not row-by-row |
| Inconsistencies from crashes | Consistency validator runs during reconciliation |
| Race conditions | Database transactions provide isolation |
| Performance impact | Index on document_id in chunks table |

---

## Performance Considerations

### Bulk Update Benchmarks

| Chunk Count | Expected Update Time |
|-------------|---------------------|
| 10 chunks | < 50ms |
| 100 chunks | < 100ms |
| 500 chunks | < 500ms |

### Optimization Strategies

1. **Batch updates**: Single SQL UPDATE with WHERE clause
2. **Index usage**: Ensure `document_id` index exists on `document_chunks`
3. **Connection pooling**: Reuse connections from NpgsqlDataSource
4. **Minimal logging**: Reduce log verbosity for bulk operations
