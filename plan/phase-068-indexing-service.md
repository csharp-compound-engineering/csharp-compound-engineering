# Phase 068: Document Indexing Service

> **Status**: PLANNED
> **Category**: Document Processing
> **Estimated Effort**: L
> **Prerequisites**: Phase 059 (Document Repository), Phase 029 (Embedding Service), Phase 048 (Chunk Repository)

---

## Spec References

- [mcp-server/tools.md - index_document tool](../spec/mcp-server/tools.md#3-index-document-tool)
- [mcp-server/file-watcher.md - Event Processing](../spec/mcp-server/file-watcher.md#event-processing)
- [mcp-server/file-watcher.md - Sync on Activation](../spec/mcp-server/file-watcher.md#sync-on-activation-startup-reconciliation)
- [mcp-server/chunking.md - Chunking Algorithm](../spec/mcp-server/chunking.md#chunking-algorithm)
- [mcp-server/database-schema.md - Documents Schema](../spec/mcp-server/database-schema.md#documents-schema-semantic-kernel-model)

---

## Objectives

1. Create `IDocumentIndexingService` interface for full indexing pipeline orchestration
2. Implement complete indexing pipeline: parse, validate, embed, store
3. Implement chunk generation for large documents (>500 lines)
4. Extract markdown links and update document graph
5. Ensure idempotent indexing (safe to re-index same document multiple times)
6. Provide index progress tracking for bulk operations
7. Support both compounding docs and external docs indexing

---

## Acceptance Criteria

- [ ] `IDocumentIndexingService` interface defined with `IndexDocumentAsync` and `IndexDocumentsAsync` methods
- [ ] `DocumentIndexingService` implements full pipeline: read, parse, validate, embed, store
- [ ] Documents >500 lines are automatically chunked at H2/H3 headers
- [ ] Markdown links extracted and stored for graph traversal
- [ ] Re-indexing same document produces identical results (idempotent)
- [ ] Content hash comparison skips unchanged documents
- [ ] Progress reporting via `IProgress<IndexingProgress>` for bulk operations
- [ ] Chunks deleted and regenerated when parent document changes
- [ ] Transaction ensures atomic document + chunks storage
- [ ] Unit tests cover all indexing scenarios
- [ ] Integration tests verify end-to-end indexing with Ollama and PostgreSQL

---

## Implementation Notes

### 1. IDocumentIndexingService Interface

```csharp
// src/CompoundDocs.Common/Services/IDocumentIndexingService.cs
namespace CompoundDocs.Common.Services;

/// <summary>
/// Orchestrates the full document indexing pipeline:
/// parse -> validate -> embed -> store (with chunks if needed).
/// </summary>
public interface IDocumentIndexingService
{
    /// <summary>
    /// Indexes a single document from the file system.
    /// </summary>
    /// <param name="relativePath">Path relative to the compounding docs root.</param>
    /// <param name="tenantContext">Current tenant context for isolation.</param>
    /// <param name="forceReindex">If true, re-index even if content hash matches.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating success/failure and indexing details.</returns>
    Task<IndexingResult> IndexDocumentAsync(
        string relativePath,
        TenantContext tenantContext,
        bool forceReindex = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Indexes multiple documents with progress reporting.
    /// </summary>
    /// <param name="relativePaths">Paths relative to the compounding docs root.</param>
    /// <param name="tenantContext">Current tenant context for isolation.</param>
    /// <param name="progress">Progress reporter for tracking bulk operations.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Aggregate result with per-document outcomes.</returns>
    Task<BulkIndexingResult> IndexDocumentsAsync(
        IEnumerable<string> relativePaths,
        TenantContext tenantContext,
        IProgress<IndexingProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Indexes an external document (read-only, no promotion support).
    /// </summary>
    /// <param name="relativePath">Path relative to the external docs root.</param>
    /// <param name="tenantContext">Current tenant context for isolation.</param>
    /// <param name="forceReindex">If true, re-index even if content hash matches.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating success/failure and indexing details.</returns>
    Task<IndexingResult> IndexExternalDocumentAsync(
        string relativePath,
        TenantContext tenantContext,
        bool forceReindex = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a document and its chunks from the index.
    /// </summary>
    /// <param name="relativePath">Path relative to the compounding docs root.</param>
    /// <param name="tenantContext">Current tenant context for isolation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if document was found and removed.</returns>
    Task<bool> RemoveDocumentAsync(
        string relativePath,
        TenantContext tenantContext,
        CancellationToken cancellationToken = default);
}
```

### 2. Indexing Result Models

```csharp
// src/CompoundDocs.Common/Models/IndexingResult.cs
namespace CompoundDocs.Common.Models;

/// <summary>
/// Result of a single document indexing operation.
/// </summary>
public sealed record IndexingResult
{
    /// <summary>
    /// Whether the indexing operation succeeded.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// The relative path of the indexed document.
    /// </summary>
    public required string RelativePath { get; init; }

    /// <summary>
    /// The action taken during indexing.
    /// </summary>
    public required IndexingAction Action { get; init; }

    /// <summary>
    /// Document ID in the vector store (if successful).
    /// </summary>
    public string? DocumentId { get; init; }

    /// <summary>
    /// Number of chunks created (0 if document wasn't chunked).
    /// </summary>
    public int ChunkCount { get; init; }

    /// <summary>
    /// Embedding dimensions used.
    /// </summary>
    public int EmbeddingDimensions { get; init; }

    /// <summary>
    /// Linked documents extracted from markdown content.
    /// </summary>
    public IReadOnlyList<string> LinkedDocuments { get; init; } = [];

    /// <summary>
    /// Error message if indexing failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Time taken to complete indexing.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static IndexingResult Indexed(
        string relativePath,
        string documentId,
        int chunkCount,
        int embeddingDimensions,
        IReadOnlyList<string> linkedDocuments,
        TimeSpan duration) => new()
    {
        Success = true,
        RelativePath = relativePath,
        Action = IndexingAction.Indexed,
        DocumentId = documentId,
        ChunkCount = chunkCount,
        EmbeddingDimensions = embeddingDimensions,
        LinkedDocuments = linkedDocuments,
        Duration = duration
    };

    /// <summary>
    /// Creates a skipped result (content unchanged).
    /// </summary>
    public static IndexingResult Skipped(string relativePath, string documentId) => new()
    {
        Success = true,
        RelativePath = relativePath,
        Action = IndexingAction.Skipped,
        DocumentId = documentId
    };

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static IndexingResult Failed(string relativePath, string errorMessage, TimeSpan duration) => new()
    {
        Success = false,
        RelativePath = relativePath,
        Action = IndexingAction.Failed,
        ErrorMessage = errorMessage,
        Duration = duration
    };
}

/// <summary>
/// Action taken during document indexing.
/// </summary>
public enum IndexingAction
{
    /// <summary>Document was indexed (new or updated).</summary>
    Indexed,

    /// <summary>Document was skipped (content unchanged).</summary>
    Skipped,

    /// <summary>Indexing failed.</summary>
    Failed
}
```

### 3. Progress Tracking Models

```csharp
// src/CompoundDocs.Common/Models/IndexingProgress.cs
namespace CompoundDocs.Common.Models;

/// <summary>
/// Progress information for bulk indexing operations.
/// </summary>
public sealed record IndexingProgress
{
    /// <summary>
    /// Total number of documents to process.
    /// </summary>
    public required int TotalDocuments { get; init; }

    /// <summary>
    /// Number of documents processed so far.
    /// </summary>
    public required int ProcessedDocuments { get; init; }

    /// <summary>
    /// Number of documents successfully indexed.
    /// </summary>
    public required int IndexedCount { get; init; }

    /// <summary>
    /// Number of documents skipped (unchanged).
    /// </summary>
    public required int SkippedCount { get; init; }

    /// <summary>
    /// Number of documents that failed indexing.
    /// </summary>
    public required int FailedCount { get; init; }

    /// <summary>
    /// Current document being processed.
    /// </summary>
    public string? CurrentDocument { get; init; }

    /// <summary>
    /// Current phase of processing.
    /// </summary>
    public string? CurrentPhase { get; init; }

    /// <summary>
    /// Progress percentage (0-100).
    /// </summary>
    public double PercentComplete =>
        TotalDocuments > 0 ? (double)ProcessedDocuments / TotalDocuments * 100 : 0;
}

/// <summary>
/// Result of bulk indexing operation.
/// </summary>
public sealed record BulkIndexingResult
{
    /// <summary>
    /// Total number of documents processed.
    /// </summary>
    public required int TotalDocuments { get; init; }

    /// <summary>
    /// Number of documents successfully indexed.
    /// </summary>
    public required int IndexedCount { get; init; }

    /// <summary>
    /// Number of documents skipped (unchanged).
    /// </summary>
    public required int SkippedCount { get; init; }

    /// <summary>
    /// Number of documents that failed indexing.
    /// </summary>
    public required int FailedCount { get; init; }

    /// <summary>
    /// Individual results for each document.
    /// </summary>
    public required IReadOnlyList<IndexingResult> Results { get; init; }

    /// <summary>
    /// Total time for the bulk operation.
    /// </summary>
    public required TimeSpan TotalDuration { get; init; }
}
```

### 4. Document Indexing Service Implementation

```csharp
// src/CompoundDocs.McpServer/Services/DocumentIndexingService.cs
using System.Diagnostics;
using CompoundDocs.Common.Models;
using CompoundDocs.Common.Services;

namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Implements the full document indexing pipeline.
/// </summary>
public sealed class DocumentIndexingService : IDocumentIndexingService
{
    private readonly IMarkdownParserService _markdownParser;
    private readonly ISchemaValidationService _schemaValidator;
    private readonly IEmbeddingService _embeddingService;
    private readonly IDocumentRepository _documentRepository;
    private readonly IDocumentChunkRepository _chunkRepository;
    private readonly IDocumentGraphService _graphService;
    private readonly IContentHashService _contentHashService;
    private readonly IDocumentChunkingService _chunkingService;
    private readonly ILogger<DocumentIndexingService> _logger;

    /// <summary>
    /// Line count threshold for chunking large documents.
    /// </summary>
    private const int ChunkingThreshold = 500;

    public DocumentIndexingService(
        IMarkdownParserService markdownParser,
        ISchemaValidationService schemaValidator,
        IEmbeddingService embeddingService,
        IDocumentRepository documentRepository,
        IDocumentChunkRepository chunkRepository,
        IDocumentGraphService graphService,
        IContentHashService contentHashService,
        IDocumentChunkingService chunkingService,
        ILogger<DocumentIndexingService> logger)
    {
        _markdownParser = markdownParser ?? throw new ArgumentNullException(nameof(markdownParser));
        _schemaValidator = schemaValidator ?? throw new ArgumentNullException(nameof(schemaValidator));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _chunkRepository = chunkRepository ?? throw new ArgumentNullException(nameof(chunkRepository));
        _graphService = graphService ?? throw new ArgumentNullException(nameof(graphService));
        _contentHashService = contentHashService ?? throw new ArgumentNullException(nameof(contentHashService));
        _chunkingService = chunkingService ?? throw new ArgumentNullException(nameof(chunkingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IndexingResult> IndexDocumentAsync(
        string relativePath,
        TenantContext tenantContext,
        bool forceReindex = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentNullException.ThrowIfNull(tenantContext);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogDebug("Starting indexing for document: {RelativePath}", relativePath);

            // Step 1: Read and parse the document
            var parseResult = await _markdownParser.ParseDocumentAsync(
                relativePath,
                tenantContext.CompoundingDocsRoot,
                cancellationToken);

            if (!parseResult.Success)
            {
                return IndexingResult.Failed(
                    relativePath,
                    $"Parse error: {parseResult.ErrorMessage}",
                    stopwatch.Elapsed);
            }

            // Step 2: Compute content hash for idempotency check
            var contentHash = _contentHashService.ComputeHash(parseResult.RawContent);

            // Step 3: Check if document already exists with same hash
            if (!forceReindex)
            {
                var existingDoc = await _documentRepository.GetByPathAsync(
                    relativePath,
                    tenantContext,
                    cancellationToken);

                if (existingDoc != null && existingDoc.ContentHash == contentHash)
                {
                    _logger.LogDebug(
                        "Document unchanged, skipping: {RelativePath}",
                        relativePath);
                    return IndexingResult.Skipped(relativePath, existingDoc.Id);
                }
            }

            // Step 4: Validate frontmatter against schema
            var validationResult = await _schemaValidator.ValidateAsync(
                parseResult.Frontmatter,
                parseResult.DocType,
                cancellationToken);

            if (!validationResult.IsValid)
            {
                return IndexingResult.Failed(
                    relativePath,
                    $"Schema validation failed: {string.Join("; ", validationResult.Errors)}",
                    stopwatch.Elapsed);
            }

            // Step 5: Extract linked documents
            var linkedDocuments = _markdownParser.ExtractLinks(parseResult.Document);

            // Step 6: Generate embedding for document
            var embedding = await _embeddingService.GenerateEmbeddingAsync(
                parseResult.EmbeddableContent,
                cancellationToken);

            // Step 7: Create document model
            var document = new CompoundDocument
            {
                Id = Guid.NewGuid().ToString(),
                ProjectName = tenantContext.ProjectName,
                BranchName = tenantContext.BranchName,
                PathHash = tenantContext.PathHash,
                RelativePath = relativePath,
                Title = parseResult.Title,
                Summary = parseResult.Summary,
                DocType = parseResult.DocType,
                PromotionLevel = parseResult.PromotionLevel ?? PromotionLevels.Standard,
                ContentHash = contentHash,
                CharCount = parseResult.RawContent.Length,
                FrontmatterJson = parseResult.FrontmatterJson,
                Embedding = embedding
            };

            // Step 8: Determine if chunking is needed
            var lineCount = parseResult.RawContent.Split('\n').Length;
            var chunks = new List<DocumentChunk>();

            if (lineCount > ChunkingThreshold)
            {
                _logger.LogDebug(
                    "Document exceeds {Threshold} lines ({Lines}), generating chunks",
                    ChunkingThreshold,
                    lineCount);

                chunks = await GenerateChunksAsync(
                    document,
                    parseResult,
                    tenantContext,
                    cancellationToken);
            }

            // Step 9: Store document and chunks atomically
            await StoreDocumentWithChunksAsync(
                document,
                chunks,
                relativePath,
                tenantContext,
                cancellationToken);

            // Step 10: Update document graph with links
            await _graphService.UpdateLinksAsync(
                document.Id,
                relativePath,
                linkedDocuments,
                tenantContext,
                cancellationToken);

            _logger.LogInformation(
                "Indexed document: {RelativePath} (ID: {DocumentId}, Chunks: {ChunkCount})",
                relativePath,
                document.Id,
                chunks.Count);

            return IndexingResult.Indexed(
                relativePath,
                document.Id,
                chunks.Count,
                embedding.Length,
                linkedDocuments,
                stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to index document: {RelativePath}", relativePath);
            return IndexingResult.Failed(
                relativePath,
                ex.Message,
                stopwatch.Elapsed);
        }
    }

    public async Task<BulkIndexingResult> IndexDocumentsAsync(
        IEnumerable<string> relativePaths,
        TenantContext tenantContext,
        IProgress<IndexingProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(relativePaths);
        ArgumentNullException.ThrowIfNull(tenantContext);

        var stopwatch = Stopwatch.StartNew();
        var pathList = relativePaths.ToList();
        var results = new List<IndexingResult>();

        var indexedCount = 0;
        var skippedCount = 0;
        var failedCount = 0;

        for (var i = 0; i < pathList.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var path = pathList[i];

            // Report progress before processing
            progress?.Report(new IndexingProgress
            {
                TotalDocuments = pathList.Count,
                ProcessedDocuments = i,
                IndexedCount = indexedCount,
                SkippedCount = skippedCount,
                FailedCount = failedCount,
                CurrentDocument = path,
                CurrentPhase = "Indexing"
            });

            var result = await IndexDocumentAsync(
                path,
                tenantContext,
                forceReindex: false,
                cancellationToken);

            results.Add(result);

            switch (result.Action)
            {
                case IndexingAction.Indexed:
                    indexedCount++;
                    break;
                case IndexingAction.Skipped:
                    skippedCount++;
                    break;
                case IndexingAction.Failed:
                    failedCount++;
                    break;
            }
        }

        // Final progress report
        progress?.Report(new IndexingProgress
        {
            TotalDocuments = pathList.Count,
            ProcessedDocuments = pathList.Count,
            IndexedCount = indexedCount,
            SkippedCount = skippedCount,
            FailedCount = failedCount,
            CurrentPhase = "Complete"
        });

        return new BulkIndexingResult
        {
            TotalDocuments = pathList.Count,
            IndexedCount = indexedCount,
            SkippedCount = skippedCount,
            FailedCount = failedCount,
            Results = results,
            TotalDuration = stopwatch.Elapsed
        };
    }

    public async Task<IndexingResult> IndexExternalDocumentAsync(
        string relativePath,
        TenantContext tenantContext,
        bool forceReindex = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentNullException.ThrowIfNull(tenantContext);

        if (string.IsNullOrEmpty(tenantContext.ExternalDocsRoot))
        {
            return IndexingResult.Failed(
                relativePath,
                "External docs not configured for this project",
                TimeSpan.Zero);
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogDebug("Starting external doc indexing: {RelativePath}", relativePath);

            // External docs use simplified parsing (no schema validation)
            var parseResult = await _markdownParser.ParseExternalDocumentAsync(
                relativePath,
                tenantContext.ExternalDocsRoot,
                cancellationToken);

            if (!parseResult.Success)
            {
                return IndexingResult.Failed(
                    relativePath,
                    $"Parse error: {parseResult.ErrorMessage}",
                    stopwatch.Elapsed);
            }

            var contentHash = _contentHashService.ComputeHash(parseResult.RawContent);

            // Check for existing unchanged document
            if (!forceReindex)
            {
                var existingDoc = await _documentRepository.GetExternalByPathAsync(
                    relativePath,
                    tenantContext,
                    cancellationToken);

                if (existingDoc != null && existingDoc.ContentHash == contentHash)
                {
                    return IndexingResult.Skipped(relativePath, existingDoc.Id);
                }
            }

            var embedding = await _embeddingService.GenerateEmbeddingAsync(
                parseResult.EmbeddableContent,
                cancellationToken);

            var document = new ExternalDocument
            {
                Id = Guid.NewGuid().ToString(),
                ProjectName = tenantContext.ProjectName,
                BranchName = tenantContext.BranchName,
                PathHash = tenantContext.PathHash,
                RelativePath = relativePath,
                Title = parseResult.Title,
                Summary = parseResult.Summary,
                ContentHash = contentHash,
                CharCount = parseResult.RawContent.Length,
                Embedding = embedding
            };

            // Generate chunks if document is large
            var lineCount = parseResult.RawContent.Split('\n').Length;
            var chunks = new List<ExternalDocumentChunk>();

            if (lineCount > ChunkingThreshold)
            {
                chunks = await GenerateExternalChunksAsync(
                    document,
                    parseResult,
                    tenantContext,
                    cancellationToken);
            }

            await StoreExternalDocumentWithChunksAsync(
                document,
                chunks,
                relativePath,
                tenantContext,
                cancellationToken);

            return IndexingResult.Indexed(
                relativePath,
                document.Id,
                chunks.Count,
                embedding.Length,
                [], // External docs don't track links
                stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to index external document: {RelativePath}", relativePath);
            return IndexingResult.Failed(relativePath, ex.Message, stopwatch.Elapsed);
        }
    }

    public async Task<bool> RemoveDocumentAsync(
        string relativePath,
        TenantContext tenantContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentNullException.ThrowIfNull(tenantContext);

        _logger.LogDebug("Removing document from index: {RelativePath}", relativePath);

        var existingDoc = await _documentRepository.GetByPathAsync(
            relativePath,
            tenantContext,
            cancellationToken);

        if (existingDoc == null)
        {
            _logger.LogDebug("Document not found in index: {RelativePath}", relativePath);
            return false;
        }

        // Delete chunks first (foreign key constraint)
        await _chunkRepository.DeleteByDocumentIdAsync(
            existingDoc.Id,
            tenantContext,
            cancellationToken);

        // Remove from graph
        await _graphService.RemoveDocumentAsync(
            existingDoc.Id,
            tenantContext,
            cancellationToken);

        // Delete document
        await _documentRepository.DeleteAsync(
            existingDoc.Id,
            tenantContext,
            cancellationToken);

        _logger.LogInformation("Removed document from index: {RelativePath}", relativePath);
        return true;
    }

    #region Private Helper Methods

    private async Task<List<DocumentChunk>> GenerateChunksAsync(
        CompoundDocument parentDocument,
        ParseResult parseResult,
        TenantContext tenantContext,
        CancellationToken cancellationToken)
    {
        // Use chunking service to split by H2/H3 headers
        var chunkContents = _chunkingService.ChunkByHeaders(
            parseResult.Document,
            parseResult.RawContent);

        var chunks = new List<DocumentChunk>();
        var chunkIndex = 0;

        foreach (var chunkContent in chunkContents)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Generate embedding for chunk
            var chunkEmbedding = await _embeddingService.GenerateEmbeddingAsync(
                chunkContent.Content,
                cancellationToken);

            var chunk = new DocumentChunk
            {
                Id = Guid.NewGuid().ToString(),
                DocumentId = parentDocument.Id,
                ProjectName = tenantContext.ProjectName,
                BranchName = tenantContext.BranchName,
                PathHash = tenantContext.PathHash,
                PromotionLevel = parentDocument.PromotionLevel,
                ChunkIndex = chunkIndex++,
                HeaderPath = chunkContent.HeaderPath,
                Content = chunkContent.Content,
                Embedding = chunkEmbedding
            };

            chunks.Add(chunk);
        }

        _logger.LogDebug(
            "Generated {ChunkCount} chunks for document {RelativePath}",
            chunks.Count,
            parentDocument.RelativePath);

        return chunks;
    }

    private async Task<List<ExternalDocumentChunk>> GenerateExternalChunksAsync(
        ExternalDocument parentDocument,
        ParseResult parseResult,
        TenantContext tenantContext,
        CancellationToken cancellationToken)
    {
        var chunkContents = _chunkingService.ChunkByHeaders(
            parseResult.Document,
            parseResult.RawContent);

        var chunks = new List<ExternalDocumentChunk>();
        var chunkIndex = 0;

        foreach (var chunkContent in chunkContents)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var chunkEmbedding = await _embeddingService.GenerateEmbeddingAsync(
                chunkContent.Content,
                cancellationToken);

            var chunk = new ExternalDocumentChunk
            {
                Id = Guid.NewGuid().ToString(),
                ExternalDocumentId = parentDocument.Id,
                ProjectName = tenantContext.ProjectName,
                BranchName = tenantContext.BranchName,
                PathHash = tenantContext.PathHash,
                ChunkIndex = chunkIndex++,
                HeaderPath = chunkContent.HeaderPath,
                Content = chunkContent.Content,
                Embedding = chunkEmbedding
            };

            chunks.Add(chunk);
        }

        return chunks;
    }

    private async Task StoreDocumentWithChunksAsync(
        CompoundDocument document,
        List<DocumentChunk> chunks,
        string relativePath,
        TenantContext tenantContext,
        CancellationToken cancellationToken)
    {
        // Check if document already exists (for update scenario)
        var existingDoc = await _documentRepository.GetByPathAsync(
            relativePath,
            tenantContext,
            cancellationToken);

        if (existingDoc != null)
        {
            // Delete old chunks before updating
            await _chunkRepository.DeleteByDocumentIdAsync(
                existingDoc.Id,
                tenantContext,
                cancellationToken);

            // Use existing ID for update
            document.Id = existingDoc.Id;
        }

        // Upsert document
        await _documentRepository.UpsertAsync(document, cancellationToken);

        // Insert new chunks
        if (chunks.Count > 0)
        {
            await _chunkRepository.InsertManyAsync(chunks, cancellationToken);
        }
    }

    private async Task StoreExternalDocumentWithChunksAsync(
        ExternalDocument document,
        List<ExternalDocumentChunk> chunks,
        string relativePath,
        TenantContext tenantContext,
        CancellationToken cancellationToken)
    {
        var existingDoc = await _documentRepository.GetExternalByPathAsync(
            relativePath,
            tenantContext,
            cancellationToken);

        if (existingDoc != null)
        {
            await _chunkRepository.DeleteExternalByDocumentIdAsync(
                existingDoc.Id,
                tenantContext,
                cancellationToken);

            document.Id = existingDoc.Id;
        }

        await _documentRepository.UpsertExternalAsync(document, cancellationToken);

        if (chunks.Count > 0)
        {
            await _chunkRepository.InsertManyExternalAsync(chunks, cancellationToken);
        }
    }

    #endregion
}
```

### 5. Document Chunking Service

```csharp
// src/CompoundDocs.McpServer/Services/IDocumentChunkingService.cs
namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Splits large documents into chunks at header boundaries.
/// </summary>
public interface IDocumentChunkingService
{
    /// <summary>
    /// Chunks a document by H2 (##) and H3 (###) markdown headers.
    /// </summary>
    /// <param name="document">Parsed Markdig document.</param>
    /// <param name="rawContent">Raw markdown content.</param>
    /// <returns>List of chunk content with header paths.</returns>
    IReadOnlyList<ChunkContent> ChunkByHeaders(
        MarkdownDocument document,
        string rawContent);
}

/// <summary>
/// Content extracted from a document chunk.
/// </summary>
public sealed record ChunkContent
{
    /// <summary>
    /// Header hierarchy path (e.g., "## Section > ### Subsection").
    /// </summary>
    public required string HeaderPath { get; init; }

    /// <summary>
    /// The text content of the chunk.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Starting line number in the source file.
    /// </summary>
    public required int StartLine { get; init; }

    /// <summary>
    /// Ending line number in the source file.
    /// </summary>
    public required int EndLine { get; init; }
}
```

```csharp
// src/CompoundDocs.McpServer/Services/DocumentChunkingService.cs
using Markdig;
using Markdig.Syntax;

namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Implements document chunking by markdown headers.
/// </summary>
public sealed class DocumentChunkingService : IDocumentChunkingService
{
    private readonly ILogger<DocumentChunkingService> _logger;

    /// <summary>
    /// Maximum lines for a single chunk before paragraph-level splitting.
    /// </summary>
    private const int MaxChunkLines = 1000;

    public DocumentChunkingService(ILogger<DocumentChunkingService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public IReadOnlyList<ChunkContent> ChunkByHeaders(
        MarkdownDocument document,
        string rawContent)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(rawContent);

        var lines = rawContent.Split('\n');
        var headers = document.Descendants<HeadingBlock>()
            .Where(h => h.Level == 2 || h.Level == 3)
            .OrderBy(h => h.Line)
            .ToList();

        if (headers.Count == 0)
        {
            // No H2/H3 headers - return entire document as single chunk
            _logger.LogDebug("No H2/H3 headers found, returning single chunk");
            return new[]
            {
                new ChunkContent
                {
                    HeaderPath = "(document)",
                    Content = rawContent,
                    StartLine = 1,
                    EndLine = lines.Length
                }
            };
        }

        var chunks = new List<ChunkContent>();
        var headerStack = new Stack<(int Level, string Text)>();

        for (var i = 0; i < headers.Count; i++)
        {
            var header = headers[i];
            var headerText = GetHeaderText(header);
            var headerLevel = header.Level;

            // Update header stack for path building
            while (headerStack.Count > 0 && headerStack.Peek().Level >= headerLevel)
            {
                headerStack.Pop();
            }
            headerStack.Push((headerLevel, headerText));

            // Calculate chunk boundaries
            var startLine = header.Line + 1; // Line is 0-indexed
            var endLine = i + 1 < headers.Count
                ? headers[i + 1].Line
                : lines.Length;

            // Extract content between headers
            var contentLines = lines.Skip(startLine).Take(endLine - startLine);
            var content = string.Join('\n', contentLines).Trim();

            if (string.IsNullOrWhiteSpace(content))
            {
                continue; // Skip empty sections
            }

            // Build header path
            var headerPath = BuildHeaderPath(headerStack);

            // Check if chunk is too large and needs sub-chunking
            var chunkLineCount = endLine - startLine;
            if (chunkLineCount > MaxChunkLines)
            {
                _logger.LogDebug(
                    "Chunk at '{HeaderPath}' exceeds {MaxLines} lines ({Lines}), sub-chunking",
                    headerPath,
                    MaxChunkLines,
                    chunkLineCount);

                var subChunks = SubChunkByParagraphs(
                    headerPath,
                    content,
                    startLine);
                chunks.AddRange(subChunks);
            }
            else
            {
                chunks.Add(new ChunkContent
                {
                    HeaderPath = headerPath,
                    Content = content,
                    StartLine = startLine + 1, // Convert to 1-indexed
                    EndLine = endLine
                });
            }
        }

        _logger.LogDebug("Generated {ChunkCount} chunks from document", chunks.Count);
        return chunks;
    }

    private static string GetHeaderText(HeadingBlock header)
    {
        var inline = header.Inline?.FirstChild;
        return inline?.ToString() ?? "(untitled)";
    }

    private static string BuildHeaderPath(Stack<(int Level, string Text)> headerStack)
    {
        var parts = headerStack.Reverse().Select(h =>
        {
            var prefix = new string('#', h.Level);
            return $"{prefix} {h.Text}";
        });

        return string.Join(" > ", parts);
    }

    private IReadOnlyList<ChunkContent> SubChunkByParagraphs(
        string parentHeaderPath,
        string content,
        int baseStartLine)
    {
        // Split at double newlines (paragraph boundaries)
        var paragraphs = content.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
        var chunks = new List<ChunkContent>();
        var currentChunk = new List<string>();
        var currentStartLine = baseStartLine;
        var currentLineCount = 0;

        foreach (var para in paragraphs)
        {
            var paraLines = para.Split('\n').Length;

            if (currentLineCount + paraLines > MaxChunkLines / 2 && currentChunk.Count > 0)
            {
                // Flush current chunk
                chunks.Add(new ChunkContent
                {
                    HeaderPath = $"{parentHeaderPath} (part {chunks.Count + 1})",
                    Content = string.Join("\n\n", currentChunk),
                    StartLine = currentStartLine + 1,
                    EndLine = currentStartLine + currentLineCount
                });

                currentStartLine += currentLineCount;
                currentChunk.Clear();
                currentLineCount = 0;
            }

            currentChunk.Add(para);
            currentLineCount += paraLines;
        }

        // Flush remaining content
        if (currentChunk.Count > 0)
        {
            chunks.Add(new ChunkContent
            {
                HeaderPath = chunks.Count > 0
                    ? $"{parentHeaderPath} (part {chunks.Count + 1})"
                    : parentHeaderPath,
                Content = string.Join("\n\n", currentChunk),
                StartLine = currentStartLine + 1,
                EndLine = currentStartLine + currentLineCount
            });
        }

        return chunks;
    }
}
```

### 6. Content Hash Service

```csharp
// src/CompoundDocs.Common/Services/IContentHashService.cs
namespace CompoundDocs.Common.Services;

/// <summary>
/// Computes content hashes for change detection.
/// </summary>
public interface IContentHashService
{
    /// <summary>
    /// Computes a SHA256 hash of the content.
    /// </summary>
    /// <param name="content">Content to hash.</param>
    /// <returns>Lowercase hex string of the hash.</returns>
    string ComputeHash(string content);
}
```

```csharp
// src/CompoundDocs.McpServer/Services/ContentHashService.cs
using System.Security.Cryptography;
using System.Text;
using CompoundDocs.Common.Services;

namespace CompoundDocs.McpServer.Services;

/// <summary>
/// SHA256-based content hashing for change detection.
/// </summary>
public sealed class ContentHashService : IContentHashService
{
    public string ComputeHash(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var bytes = Encoding.UTF8.GetBytes(content);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
```

### 7. Service Registration

```csharp
// In ServiceCollectionExtensions.cs
public static IServiceCollection AddDocumentIndexingServices(
    this IServiceCollection services)
{
    services.AddSingleton<IContentHashService, ContentHashService>();
    services.AddSingleton<IDocumentChunkingService, DocumentChunkingService>();
    services.AddScoped<IDocumentIndexingService, DocumentIndexingService>();

    return services;
}
```

---

## Dependencies

### Depends On

- **Phase 059**: Document Repository - CRUD operations for documents
- **Phase 029**: Embedding Service - Vector embedding generation
- **Phase 048**: Chunk Repository - CRUD operations for chunks
- **Phase 015**: Markdown Parser - Document parsing with Markdig
- **Phase 014**: Schema Validation - Frontmatter validation
- **Phase 016**: QuikGraph - Document graph service

### Blocks

- **Phase 069**: File Watcher Integration - Uses indexing service on file events
- **Phase 070**: Reconciliation Service - Uses indexing service for sync
- **Phase 075**: index_document MCP Tool - Exposes indexing to MCP clients

---

## Testing Verification

### Unit Tests

```csharp
// tests/CompoundDocs.Tests/Services/DocumentIndexingServiceTests.cs
public class DocumentIndexingServiceTests
{
    private readonly Mock<IMarkdownParserService> _mockParser;
    private readonly Mock<ISchemaValidationService> _mockValidator;
    private readonly Mock<IEmbeddingService> _mockEmbedding;
    private readonly Mock<IDocumentRepository> _mockDocRepo;
    private readonly Mock<IDocumentChunkRepository> _mockChunkRepo;
    private readonly Mock<IDocumentGraphService> _mockGraph;
    private readonly Mock<IContentHashService> _mockHash;
    private readonly Mock<IDocumentChunkingService> _mockChunking;
    private readonly DocumentIndexingService _service;

    public DocumentIndexingServiceTests()
    {
        // Setup mocks...
        _service = new DocumentIndexingService(
            _mockParser.Object,
            _mockValidator.Object,
            _mockEmbedding.Object,
            _mockDocRepo.Object,
            _mockChunkRepo.Object,
            _mockGraph.Object,
            _mockHash.Object,
            _mockChunking.Object,
            Mock.Of<ILogger<DocumentIndexingService>>());
    }

    [Fact]
    public async Task IndexDocumentAsync_NewDocument_IndexesSuccessfully()
    {
        // Arrange
        var tenantContext = CreateTestTenantContext();
        var relativePath = "problems/test-problem.md";

        SetupSuccessfulParse(relativePath);
        SetupSuccessfulValidation();
        SetupSuccessfulEmbedding();
        _mockDocRepo.Setup(r => r.GetByPathAsync(It.IsAny<string>(), It.IsAny<TenantContext>(), default))
            .ReturnsAsync((CompoundDocument?)null);

        // Act
        var result = await _service.IndexDocumentAsync(relativePath, tenantContext);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(IndexingAction.Indexed, result.Action);
        Assert.NotNull(result.DocumentId);
        _mockDocRepo.Verify(r => r.UpsertAsync(It.IsAny<CompoundDocument>(), default), Times.Once);
    }

    [Fact]
    public async Task IndexDocumentAsync_UnchangedDocument_ReturnsSkipped()
    {
        // Arrange
        var tenantContext = CreateTestTenantContext();
        var relativePath = "problems/unchanged.md";
        var existingHash = "abc123";

        SetupSuccessfulParse(relativePath);
        _mockHash.Setup(h => h.ComputeHash(It.IsAny<string>())).Returns(existingHash);
        _mockDocRepo.Setup(r => r.GetByPathAsync(relativePath, tenantContext, default))
            .ReturnsAsync(new CompoundDocument { Id = "existing-id", ContentHash = existingHash });

        // Act
        var result = await _service.IndexDocumentAsync(relativePath, tenantContext);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(IndexingAction.Skipped, result.Action);
        Assert.Equal("existing-id", result.DocumentId);
    }

    [Fact]
    public async Task IndexDocumentAsync_LargeDocument_CreatesChunks()
    {
        // Arrange
        var tenantContext = CreateTestTenantContext();
        var relativePath = "problems/large-doc.md";
        var largeContent = string.Join('\n', Enumerable.Repeat("Line content", 600));

        SetupSuccessfulParse(relativePath, largeContent);
        SetupSuccessfulValidation();
        SetupSuccessfulEmbedding();
        _mockDocRepo.Setup(r => r.GetByPathAsync(It.IsAny<string>(), It.IsAny<TenantContext>(), default))
            .ReturnsAsync((CompoundDocument?)null);

        _mockChunking.Setup(c => c.ChunkByHeaders(It.IsAny<MarkdownDocument>(), It.IsAny<string>()))
            .Returns(new[]
            {
                new ChunkContent { HeaderPath = "## Section 1", Content = "Content 1", StartLine = 1, EndLine = 200 },
                new ChunkContent { HeaderPath = "## Section 2", Content = "Content 2", StartLine = 201, EndLine = 400 },
                new ChunkContent { HeaderPath = "## Section 3", Content = "Content 3", StartLine = 401, EndLine = 600 }
            });

        // Act
        var result = await _service.IndexDocumentAsync(relativePath, tenantContext);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(3, result.ChunkCount);
        _mockChunkRepo.Verify(r => r.InsertManyAsync(
            It.Is<List<DocumentChunk>>(chunks => chunks.Count == 3),
            default), Times.Once);
    }

    [Fact]
    public async Task IndexDocumentAsync_ValidationFailure_ReturnsFailed()
    {
        // Arrange
        var tenantContext = CreateTestTenantContext();
        var relativePath = "problems/invalid.md";

        SetupSuccessfulParse(relativePath);
        _mockValidator.Setup(v => v.ValidateAsync(It.IsAny<object>(), It.IsAny<string>(), default))
            .ReturnsAsync(new ValidationResult { IsValid = false, Errors = new[] { "Missing required field" } });

        // Act
        var result = await _service.IndexDocumentAsync(relativePath, tenantContext);

        // Assert
        Assert.False(result.Success);
        Assert.Equal(IndexingAction.Failed, result.Action);
        Assert.Contains("Missing required field", result.ErrorMessage);
    }

    [Fact]
    public async Task IndexDocumentAsync_ForceReindex_IgnoresContentHash()
    {
        // Arrange
        var tenantContext = CreateTestTenantContext();
        var relativePath = "problems/force-reindex.md";
        var existingHash = "same-hash";

        SetupSuccessfulParse(relativePath);
        SetupSuccessfulValidation();
        SetupSuccessfulEmbedding();
        _mockHash.Setup(h => h.ComputeHash(It.IsAny<string>())).Returns(existingHash);
        _mockDocRepo.Setup(r => r.GetByPathAsync(relativePath, tenantContext, default))
            .ReturnsAsync(new CompoundDocument { Id = "existing-id", ContentHash = existingHash });

        // Act
        var result = await _service.IndexDocumentAsync(
            relativePath,
            tenantContext,
            forceReindex: true);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(IndexingAction.Indexed, result.Action);
        _mockDocRepo.Verify(r => r.UpsertAsync(It.IsAny<CompoundDocument>(), default), Times.Once);
    }

    [Fact]
    public async Task IndexDocumentsAsync_ReportsProgress()
    {
        // Arrange
        var tenantContext = CreateTestTenantContext();
        var paths = new[] { "doc1.md", "doc2.md", "doc3.md" };
        var progressReports = new List<IndexingProgress>();
        var progress = new Progress<IndexingProgress>(p => progressReports.Add(p));

        SetupSuccessfulParse("doc1.md");
        SetupSuccessfulParse("doc2.md");
        SetupSuccessfulParse("doc3.md");
        SetupSuccessfulValidation();
        SetupSuccessfulEmbedding();

        // Act
        var result = await _service.IndexDocumentsAsync(paths, tenantContext, progress);

        // Assert
        Assert.Equal(3, result.TotalDocuments);
        Assert.True(progressReports.Count >= 3);
        Assert.Equal(100, progressReports.Last().PercentComplete);
    }

    [Fact]
    public async Task RemoveDocumentAsync_ExistingDocument_RemovesSuccessfully()
    {
        // Arrange
        var tenantContext = CreateTestTenantContext();
        var relativePath = "problems/to-delete.md";
        var existingDoc = new CompoundDocument { Id = "doc-to-delete" };

        _mockDocRepo.Setup(r => r.GetByPathAsync(relativePath, tenantContext, default))
            .ReturnsAsync(existingDoc);

        // Act
        var result = await _service.RemoveDocumentAsync(relativePath, tenantContext);

        // Assert
        Assert.True(result);
        _mockChunkRepo.Verify(r => r.DeleteByDocumentIdAsync(existingDoc.Id, tenantContext, default), Times.Once);
        _mockGraph.Verify(g => g.RemoveDocumentAsync(existingDoc.Id, tenantContext, default), Times.Once);
        _mockDocRepo.Verify(r => r.DeleteAsync(existingDoc.Id, tenantContext, default), Times.Once);
    }
}
```

### Chunking Service Tests

```csharp
// tests/CompoundDocs.Tests/Services/DocumentChunkingServiceTests.cs
public class DocumentChunkingServiceTests
{
    private readonly DocumentChunkingService _service;

    public DocumentChunkingServiceTests()
    {
        _service = new DocumentChunkingService(
            Mock.Of<ILogger<DocumentChunkingService>>());
    }

    [Fact]
    public void ChunkByHeaders_NoHeaders_ReturnsSingleChunk()
    {
        // Arrange
        var content = "Just some content without headers\n\nMore content here.";
        var document = Markdown.Parse(content);

        // Act
        var chunks = _service.ChunkByHeaders(document, content);

        // Assert
        Assert.Single(chunks);
        Assert.Equal("(document)", chunks[0].HeaderPath);
    }

    [Fact]
    public void ChunkByHeaders_WithH2Headers_ChunksCorrectly()
    {
        // Arrange
        var content = @"# Title

## Section One
Content for section one.

## Section Two
Content for section two.

## Section Three
Content for section three.";

        var document = Markdown.Parse(content);

        // Act
        var chunks = _service.ChunkByHeaders(document, content);

        // Assert
        Assert.Equal(3, chunks.Count);
        Assert.Equal("## Section One", chunks[0].HeaderPath);
        Assert.Equal("## Section Two", chunks[1].HeaderPath);
        Assert.Equal("## Section Three", chunks[2].HeaderPath);
    }

    [Fact]
    public void ChunkByHeaders_WithNestedHeaders_BuildsCorrectPath()
    {
        // Arrange
        var content = @"# Title

## Parent Section
Parent content.

### Child Section
Child content.

## Another Section
More content.";

        var document = Markdown.Parse(content);

        // Act
        var chunks = _service.ChunkByHeaders(document, content);

        // Assert
        Assert.Equal(3, chunks.Count);
        Assert.Equal("## Parent Section", chunks[0].HeaderPath);
        Assert.Equal("## Parent Section > ### Child Section", chunks[1].HeaderPath);
        Assert.Equal("## Another Section", chunks[2].HeaderPath);
    }

    [Fact]
    public void ChunkByHeaders_EmptySection_SkipsChunk()
    {
        // Arrange
        var content = @"## Section One
Content here.

## Empty Section

## Section Three
More content.";

        var document = Markdown.Parse(content);

        // Act
        var chunks = _service.ChunkByHeaders(document, content);

        // Assert
        Assert.Equal(2, chunks.Count);
        Assert.DoesNotContain(chunks, c => c.HeaderPath.Contains("Empty"));
    }
}
```

### Integration Tests

```csharp
// tests/CompoundDocs.IntegrationTests/Services/DocumentIndexingServiceIntegrationTests.cs
[Trait("Category", "Integration")]
public class DocumentIndexingServiceIntegrationTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IDocumentIndexingService _service;
    private readonly TenantContext _tenantContext;

    public DocumentIndexingServiceIntegrationTests(IntegrationTestFixture fixture)
    {
        _service = fixture.GetService<IDocumentIndexingService>();
        _tenantContext = fixture.CreateTestTenantContext();
    }

    [Fact]
    public async Task IndexDocumentAsync_RealDocument_IndexesSuccessfully()
    {
        // Arrange
        var relativePath = "problems/test-integration-doc.md";
        await CreateTestDocumentAsync(relativePath);

        try
        {
            // Act
            var result = await _service.IndexDocumentAsync(relativePath, _tenantContext);

            // Assert
            Assert.True(result.Success);
            Assert.Equal(1024, result.EmbeddingDimensions);
            Assert.NotNull(result.DocumentId);
        }
        finally
        {
            await CleanupTestDocumentAsync(relativePath);
        }
    }

    [Fact]
    public async Task IndexDocumentAsync_LargeDocument_ChunksCorrectly()
    {
        // Arrange
        var relativePath = "problems/large-integration-doc.md";
        await CreateLargeTestDocumentAsync(relativePath, 700);

        try
        {
            // Act
            var result = await _service.IndexDocumentAsync(relativePath, _tenantContext);

            // Assert
            Assert.True(result.Success);
            Assert.True(result.ChunkCount > 0, "Large document should have chunks");
        }
        finally
        {
            await CleanupTestDocumentAsync(relativePath);
        }
    }

    [Fact]
    public async Task IndexDocumentAsync_Idempotent_SameResultOnReindex()
    {
        // Arrange
        var relativePath = "problems/idempotent-test.md";
        await CreateTestDocumentAsync(relativePath);

        try
        {
            // Act
            var result1 = await _service.IndexDocumentAsync(relativePath, _tenantContext);
            var result2 = await _service.IndexDocumentAsync(relativePath, _tenantContext);

            // Assert
            Assert.True(result1.Success);
            Assert.Equal(IndexingAction.Indexed, result1.Action);
            Assert.True(result2.Success);
            Assert.Equal(IndexingAction.Skipped, result2.Action);
            Assert.Equal(result1.DocumentId, result2.DocumentId);
        }
        finally
        {
            await CleanupTestDocumentAsync(relativePath);
        }
    }
}
```

---

## Files Created/Modified

| File | Action | Description |
|------|--------|-------------|
| `src/CompoundDocs.Common/Services/IDocumentIndexingService.cs` | Create | Interface definition |
| `src/CompoundDocs.Common/Models/IndexingResult.cs` | Create | Result models |
| `src/CompoundDocs.Common/Models/IndexingProgress.cs` | Create | Progress tracking |
| `src/CompoundDocs.Common/Services/IContentHashService.cs` | Create | Hash interface |
| `src/CompoundDocs.McpServer/Services/DocumentIndexingService.cs` | Create | Main implementation |
| `src/CompoundDocs.McpServer/Services/IDocumentChunkingService.cs` | Create | Chunking interface |
| `src/CompoundDocs.McpServer/Services/DocumentChunkingService.cs` | Create | Chunking implementation |
| `src/CompoundDocs.McpServer/Services/ContentHashService.cs` | Create | Hash implementation |
| `src/CompoundDocs.McpServer/Extensions/ServiceCollectionExtensions.cs` | Modify | Service registration |
| `tests/CompoundDocs.Tests/Services/DocumentIndexingServiceTests.cs` | Create | Unit tests |
| `tests/CompoundDocs.Tests/Services/DocumentChunkingServiceTests.cs` | Create | Chunking tests |
| `tests/CompoundDocs.IntegrationTests/Services/DocumentIndexingServiceIntegrationTests.cs` | Create | Integration tests |

---

## Idempotency Guarantees

| Scenario | Behavior |
|----------|----------|
| Same document indexed twice | Second call returns `Skipped` (content hash match) |
| Document modified then re-indexed | New content indexed, old chunks deleted |
| Force re-index flag set | Always processes, even if unchanged |
| Concurrent indexing attempts | Last-write-wins (no locking) |
| Partial failure mid-index | Next index attempt will complete |

---

## Performance Considerations

| Operation | Expected Time | Notes |
|-----------|---------------|-------|
| Parse + validate | < 50ms | In-memory operations |
| Content hash | < 5ms | SHA256 is fast |
| Embedding generation | 200-500ms | Depends on content size |
| Chunk embedding (per chunk) | 100-300ms | Parallelization limited by Ollama |
| Database upsert | < 50ms | Single document |
| Full indexing (no chunks) | ~500ms | Dominated by embedding |
| Full indexing (5 chunks) | ~2s | Sequential chunk embeddings |

### Optimization Notes

- Content hash check prevents unnecessary embedding calls
- Chunks are embedded sequentially to respect Ollama limits
- Consider batching for bulk operations (future enhancement)

---

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| Embedding service unavailable | Retry with circuit breaker; return failure with clear error |
| Large document causes timeout | Sub-chunk very large sections at paragraph boundaries |
| Concurrent modification | Last-write-wins; reconciliation on next activation |
| Chunk orphans after crash | Reconciliation detects parent hash mismatch and re-indexes |
| Memory pressure on large docs | Stream processing; limit chunk batch sizes |
| Invalid markdown | Parser returns error; document not indexed |
