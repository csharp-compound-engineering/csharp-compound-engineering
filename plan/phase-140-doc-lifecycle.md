# Phase 140: Document Lifecycle Events

> **Status**: PLANNED
> **Category**: Document Processing
> **Estimated Effort**: L
> **Prerequisites**: Phase 055 (File Event Handlers), Phase 048 (Document Repository Service)

---

## Spec References

- [doc-types.md - Document Lifecycle](../spec/doc-types.md#document-lifecycle)
- [mcp-server/file-watcher.md - Event Processing](../spec/mcp-server/file-watcher.md#event-processing)
- [research/dotnet-file-watcher-embeddings-research.md - Embedding Operations Mapping](../research/dotnet-file-watcher-embeddings-research.md#8-embedding-operations-mapping)

---

## Overview

This phase implements the complete document lifecycle management system, orchestrating the flow from file system events through validation, embedding generation, and vector database synchronization. The lifecycle manager serves as the central coordinator between the file watcher service, document repository, and embedding service.

---

## Objectives

1. Implement `IDocumentLifecycleManager` interface for coordinating document operations
2. Implement document creation workflow (file watcher event to vector DB insertion)
3. Implement document update workflow with content hash comparison for efficient embedding regeneration
4. Implement document deletion workflow with cascade cleanup of chunks
5. Integrate with file watcher debounced events for reactive document processing
6. Implement promotion level preservation during updates
7. Add comprehensive lifecycle event logging and metrics

---

## Acceptance Criteria

### Document Creation Workflow

- [ ] `IDocumentLifecycleManager.HandleDocumentCreatedAsync()` implemented
- [ ] Markdown file content read and parsed via `IMarkdownParser`
- [ ] YAML frontmatter extracted and validated against doc-type schema
- [ ] Filename generated using pattern: `{sanitized-title}-{YYYYMMDD}.md`
- [ ] Document stored in correct doc-type folder: `./csharp-compounding-docs/{doc-type}/`
- [ ] Content hash computed (SHA256) and stored for change detection
- [ ] Embedding generated via `IEmbeddingService`
- [ ] Document upserted to vector DB with all metadata
- [ ] Chunks created for documents exceeding 500 lines
- [ ] Chunk embeddings generated and stored with parent document reference
- [ ] Creation success logged with document ID and path

### Document Update Workflow

- [ ] `IDocumentLifecycleManager.HandleDocumentUpdatedAsync()` implemented
- [ ] Content hash compared to detect actual changes
- [ ] No-op when content unchanged (hash matches)
- [ ] YAML frontmatter re-validated on content change
- [ ] Embedding re-generated only when content changes
- [ ] Vector DB record upserted with same document ID
- [ ] Existing chunks deleted and regenerated on content change
- [ ] Promotion level preserved during updates
- [ ] Update success logged with document ID and change summary

### Document Deletion Workflow

- [ ] `IDocumentLifecycleManager.HandleDocumentDeletedAsync()` implemented
- [ ] Document record removed from vector DB by path
- [ ] All associated chunks removed atomically
- [ ] Graceful handling of non-existent documents (log and continue)
- [ ] Deletion success logged with document ID

### Document Rename Workflow

- [ ] `IDocumentLifecycleManager.HandleDocumentRenamedAsync()` implemented
- [ ] Old path resolved to document ID
- [ ] Path updated in database when content unchanged
- [ ] Full re-index performed when content changed during rename
- [ ] Rename success logged with old and new paths

### File Watcher Integration

- [ ] `DocumentLifecycleHandler` implements `IFileEventHandler`
- [ ] Debounced events dispatched to appropriate lifecycle methods
- [ ] Error isolation ensures one file's failure doesn't block others
- [ ] Batch processing support for startup reconciliation

### Error Handling

- [ ] Schema validation failures logged with specific field errors
- [ ] Embedding generation failures trigger retry with exponential backoff
- [ ] Database write failures trigger retry with exponential backoff
- [ ] Unrecoverable errors quarantine document for manual review
- [ ] All errors include correlation ID for tracing

---

## Implementation Notes

### 1. IDocumentLifecycleManager Interface

```csharp
// src/CompoundDocs.McpServer/Services/IDocumentLifecycleManager.cs
namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Coordinates document lifecycle operations: creation, update, deletion, and rename.
/// Orchestrates the flow between file watcher events, validation, embedding generation,
/// and vector database synchronization.
/// </summary>
public interface IDocumentLifecycleManager
{
    /// <summary>
    /// Handles a new document being created in the watched directory.
    /// </summary>
    /// <param name="fullPath">Full filesystem path to the created file.</param>
    /// <param name="tenantContext">Tenant context for multi-tenant isolation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating success or failure with details.</returns>
    Task<DocumentLifecycleResult> HandleDocumentCreatedAsync(
        string fullPath,
        TenantContext tenantContext,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles an existing document being modified.
    /// </summary>
    /// <param name="fullPath">Full filesystem path to the modified file.</param>
    /// <param name="tenantContext">Tenant context for multi-tenant isolation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating success or failure with details.</returns>
    Task<DocumentLifecycleResult> HandleDocumentUpdatedAsync(
        string fullPath,
        TenantContext tenantContext,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles a document being deleted from the watched directory.
    /// </summary>
    /// <param name="fullPath">Full filesystem path to the deleted file.</param>
    /// <param name="tenantContext">Tenant context for multi-tenant isolation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating success or failure with details.</returns>
    Task<DocumentLifecycleResult> HandleDocumentDeletedAsync(
        string fullPath,
        TenantContext tenantContext,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles a document being renamed.
    /// </summary>
    /// <param name="oldFullPath">Previous full filesystem path.</param>
    /// <param name="newFullPath">New full filesystem path.</param>
    /// <param name="tenantContext">Tenant context for multi-tenant isolation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating success or failure with details.</returns>
    Task<DocumentLifecycleResult> HandleDocumentRenamedAsync(
        string oldFullPath,
        string newFullPath,
        TenantContext tenantContext,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Processes multiple documents in batch (used for startup reconciliation).
    /// </summary>
    /// <param name="filePaths">Collection of file paths to process.</param>
    /// <param name="tenantContext">Tenant context for multi-tenant isolation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Summary of batch processing results.</returns>
    Task<BatchLifecycleResult> ProcessBatchAsync(
        IEnumerable<string> filePaths,
        TenantContext tenantContext,
        CancellationToken cancellationToken = default);
}
```

### 2. DocumentLifecycleResult Record

```csharp
// src/CompoundDocs.McpServer/Services/DocumentLifecycleResult.cs
namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Result of a document lifecycle operation.
/// </summary>
public sealed record DocumentLifecycleResult
{
    /// <summary>
    /// Whether the operation completed successfully.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// The document ID (if available).
    /// </summary>
    public string? DocumentId { get; init; }

    /// <summary>
    /// The relative path of the document.
    /// </summary>
    public string? RelativePath { get; init; }

    /// <summary>
    /// The type of operation performed.
    /// </summary>
    public required DocumentLifecycleOperation Operation { get; init; }

    /// <summary>
    /// Error message if operation failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Detailed validation errors if schema validation failed.
    /// </summary>
    public IReadOnlyList<string>? ValidationErrors { get; init; }

    /// <summary>
    /// Whether the operation was skipped (e.g., content unchanged).
    /// </summary>
    public bool Skipped { get; init; }

    /// <summary>
    /// Reason for skipping, if applicable.
    /// </summary>
    public string? SkipReason { get; init; }

    /// <summary>
    /// Number of chunks created/updated (for large documents).
    /// </summary>
    public int ChunkCount { get; init; }

    /// <summary>
    /// Creates a success result.
    /// </summary>
    public static DocumentLifecycleResult Succeeded(
        DocumentLifecycleOperation operation,
        string documentId,
        string relativePath,
        int chunkCount = 0) => new()
    {
        Success = true,
        Operation = operation,
        DocumentId = documentId,
        RelativePath = relativePath,
        ChunkCount = chunkCount
    };

    /// <summary>
    /// Creates a skipped result.
    /// </summary>
    public static DocumentLifecycleResult SkippedResult(
        DocumentLifecycleOperation operation,
        string relativePath,
        string reason) => new()
    {
        Success = true,
        Operation = operation,
        RelativePath = relativePath,
        Skipped = true,
        SkipReason = reason
    };

    /// <summary>
    /// Creates a failure result.
    /// </summary>
    public static DocumentLifecycleResult Failed(
        DocumentLifecycleOperation operation,
        string relativePath,
        string errorMessage,
        IReadOnlyList<string>? validationErrors = null) => new()
    {
        Success = false,
        Operation = operation,
        RelativePath = relativePath,
        ErrorMessage = errorMessage,
        ValidationErrors = validationErrors
    };
}

/// <summary>
/// Types of document lifecycle operations.
/// </summary>
public enum DocumentLifecycleOperation
{
    Created,
    Updated,
    Deleted,
    Renamed
}

/// <summary>
/// Result of batch document processing.
/// </summary>
public sealed record BatchLifecycleResult
{
    public required int TotalCount { get; init; }
    public required int SuccessCount { get; init; }
    public required int FailedCount { get; init; }
    public required int SkippedCount { get; init; }
    public required TimeSpan Duration { get; init; }
    public IReadOnlyList<DocumentLifecycleResult> FailedResults { get; init; } = [];
}
```

### 3. DocumentLifecycleManager Implementation

```csharp
// src/CompoundDocs.McpServer/Services/DocumentLifecycleManager.cs
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using CompoundDocs.Common.Data;
using CompoundDocs.McpServer.FileWatcher;

namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Implements document lifecycle management, coordinating between
/// file watcher events, validation, embedding generation, and database operations.
/// </summary>
public sealed class DocumentLifecycleManager : IDocumentLifecycleManager
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IMarkdownParser _markdownParser;
    private readonly ISchemaValidator _schemaValidator;
    private readonly IEmbeddingService _embeddingService;
    private readonly IChunkingService _chunkingService;
    private readonly ILogger<DocumentLifecycleManager> _logger;

    private const int LargeDocumentLineThreshold = 500;

    public DocumentLifecycleManager(
        IDocumentRepository documentRepository,
        IMarkdownParser markdownParser,
        ISchemaValidator schemaValidator,
        IEmbeddingService embeddingService,
        IChunkingService chunkingService,
        ILogger<DocumentLifecycleManager> logger)
    {
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _markdownParser = markdownParser ?? throw new ArgumentNullException(nameof(markdownParser));
        _schemaValidator = schemaValidator ?? throw new ArgumentNullException(nameof(schemaValidator));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _chunkingService = chunkingService ?? throw new ArgumentNullException(nameof(chunkingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<DocumentLifecycleResult> HandleDocumentCreatedAsync(
        string fullPath,
        TenantContext tenantContext,
        CancellationToken cancellationToken = default)
    {
        var relativePath = GetRelativePath(fullPath, tenantContext);

        _logger.LogDebug(
            "Processing document creation: {RelativePath}",
            relativePath);

        try
        {
            // 1. Read file content
            var content = await ReadFileContentAsync(fullPath, cancellationToken);
            if (content is null)
            {
                return DocumentLifecycleResult.Failed(
                    DocumentLifecycleOperation.Created,
                    relativePath,
                    "File could not be read (may have been deleted)");
            }

            // 2. Parse markdown and extract frontmatter
            var parseResult = _markdownParser.Parse(content);
            if (!parseResult.IsValid)
            {
                _logger.LogWarning(
                    "Markdown parse failed for {RelativePath}: {Error}",
                    relativePath,
                    parseResult.ErrorMessage);

                return DocumentLifecycleResult.Failed(
                    DocumentLifecycleOperation.Created,
                    relativePath,
                    $"Markdown parse error: {parseResult.ErrorMessage}");
            }

            // 3. Validate against doc-type schema
            var validationResult = _schemaValidator.Validate(parseResult.Document);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning(
                    "Schema validation failed for {RelativePath}: {Errors}",
                    relativePath,
                    string.Join(", ", validationResult.Errors));

                return DocumentLifecycleResult.Failed(
                    DocumentLifecycleOperation.Created,
                    relativePath,
                    "Schema validation failed",
                    validationResult.Errors);
            }

            // 4. Compute content hash
            var contentHash = ComputeContentHash(content);

            // 5. Generate embedding
            var embeddingContent = parseResult.Document.GetEmbeddingContent();
            var embedding = await _embeddingService.GenerateEmbeddingAsync(
                embeddingContent,
                cancellationToken);

            // 6. Create document record
            var documentId = GenerateDocumentId();
            var document = CreateDocumentRecord(
                documentId,
                relativePath,
                parseResult.Document,
                contentHash,
                embedding,
                tenantContext);

            // 7. Create chunks for large documents
            var chunks = await CreateChunksIfNeededAsync(
                document,
                content,
                tenantContext,
                cancellationToken);

            // 8. Persist to database
            await _documentRepository.UpsertAsync(document, chunks, cancellationToken);

            _logger.LogInformation(
                "Document created: {RelativePath} (ID: {DocumentId}, Chunks: {ChunkCount})",
                relativePath,
                documentId,
                chunks?.Count ?? 0);

            return DocumentLifecycleResult.Succeeded(
                DocumentLifecycleOperation.Created,
                documentId,
                relativePath,
                chunks?.Count ?? 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create document: {RelativePath}", relativePath);
            return DocumentLifecycleResult.Failed(
                DocumentLifecycleOperation.Created,
                relativePath,
                ex.Message);
        }
    }

    public async Task<DocumentLifecycleResult> HandleDocumentUpdatedAsync(
        string fullPath,
        TenantContext tenantContext,
        CancellationToken cancellationToken = default)
    {
        var relativePath = GetRelativePath(fullPath, tenantContext);

        _logger.LogDebug(
            "Processing document update: {RelativePath}",
            relativePath);

        try
        {
            // 1. Read file content
            var content = await ReadFileContentAsync(fullPath, cancellationToken);
            if (content is null)
            {
                return DocumentLifecycleResult.Failed(
                    DocumentLifecycleOperation.Updated,
                    relativePath,
                    "File could not be read");
            }

            // 2. Compute content hash
            var contentHash = ComputeContentHash(content);

            // 3. Check if content actually changed
            var existingDoc = await _documentRepository.GetByPathAsync(
                relativePath,
                tenantContext,
                cancellationToken);

            if (existingDoc is not null && existingDoc.ContentHash == contentHash)
            {
                _logger.LogDebug(
                    "Content unchanged for {RelativePath}, skipping update",
                    relativePath);

                return DocumentLifecycleResult.SkippedResult(
                    DocumentLifecycleOperation.Updated,
                    relativePath,
                    "Content unchanged (hash match)");
            }

            // 4. Parse markdown
            var parseResult = _markdownParser.Parse(content);
            if (!parseResult.IsValid)
            {
                _logger.LogWarning(
                    "Markdown parse failed for {RelativePath}: {Error}",
                    relativePath,
                    parseResult.ErrorMessage);

                return DocumentLifecycleResult.Failed(
                    DocumentLifecycleOperation.Updated,
                    relativePath,
                    $"Markdown parse error: {parseResult.ErrorMessage}");
            }

            // 5. Validate schema
            var validationResult = _schemaValidator.Validate(parseResult.Document);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning(
                    "Schema validation failed for {RelativePath}: {Errors}",
                    relativePath,
                    string.Join(", ", validationResult.Errors));

                return DocumentLifecycleResult.Failed(
                    DocumentLifecycleOperation.Updated,
                    relativePath,
                    "Schema validation failed",
                    validationResult.Errors);
            }

            // 6. Generate new embedding
            var embeddingContent = parseResult.Document.GetEmbeddingContent();
            var embedding = await _embeddingService.GenerateEmbeddingAsync(
                embeddingContent,
                cancellationToken);

            // 7. Create/update document record (preserve ID and promotion level)
            var documentId = existingDoc?.Id ?? GenerateDocumentId();
            var promotionLevel = existingDoc?.PromotionLevel ?? "standard";

            var document = CreateDocumentRecord(
                documentId,
                relativePath,
                parseResult.Document,
                contentHash,
                embedding,
                tenantContext,
                promotionLevel);

            // 8. Regenerate chunks if needed
            var chunks = await CreateChunksIfNeededAsync(
                document,
                content,
                tenantContext,
                cancellationToken);

            // 9. Persist to database
            await _documentRepository.UpsertAsync(document, chunks, cancellationToken);

            _logger.LogInformation(
                "Document updated: {RelativePath} (ID: {DocumentId}, Chunks: {ChunkCount})",
                relativePath,
                documentId,
                chunks?.Count ?? 0);

            return DocumentLifecycleResult.Succeeded(
                DocumentLifecycleOperation.Updated,
                documentId,
                relativePath,
                chunks?.Count ?? 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update document: {RelativePath}", relativePath);
            return DocumentLifecycleResult.Failed(
                DocumentLifecycleOperation.Updated,
                relativePath,
                ex.Message);
        }
    }

    public async Task<DocumentLifecycleResult> HandleDocumentDeletedAsync(
        string fullPath,
        TenantContext tenantContext,
        CancellationToken cancellationToken = default)
    {
        var relativePath = GetRelativePath(fullPath, tenantContext);

        _logger.LogDebug(
            "Processing document deletion: {RelativePath}",
            relativePath);

        try
        {
            // Find existing document
            var existingDoc = await _documentRepository.GetByPathAsync(
                relativePath,
                tenantContext,
                cancellationToken);

            if (existingDoc is null)
            {
                _logger.LogDebug(
                    "Document not found for deletion: {RelativePath}",
                    relativePath);

                return DocumentLifecycleResult.SkippedResult(
                    DocumentLifecycleOperation.Deleted,
                    relativePath,
                    "Document not found in database");
            }

            // Delete document (chunks are deleted atomically by repository)
            var deleted = await _documentRepository.DeleteAsync(
                existingDoc.Id,
                cancellationToken);

            if (deleted)
            {
                _logger.LogInformation(
                    "Document deleted: {RelativePath} (ID: {DocumentId})",
                    relativePath,
                    existingDoc.Id);

                return DocumentLifecycleResult.Succeeded(
                    DocumentLifecycleOperation.Deleted,
                    existingDoc.Id,
                    relativePath);
            }

            return DocumentLifecycleResult.Failed(
                DocumentLifecycleOperation.Deleted,
                relativePath,
                "Delete operation returned false");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete document: {RelativePath}", relativePath);
            return DocumentLifecycleResult.Failed(
                DocumentLifecycleOperation.Deleted,
                relativePath,
                ex.Message);
        }
    }

    public async Task<DocumentLifecycleResult> HandleDocumentRenamedAsync(
        string oldFullPath,
        string newFullPath,
        TenantContext tenantContext,
        CancellationToken cancellationToken = default)
    {
        var oldRelativePath = GetRelativePath(oldFullPath, tenantContext);
        var newRelativePath = GetRelativePath(newFullPath, tenantContext);

        _logger.LogDebug(
            "Processing document rename: {OldPath} -> {NewPath}",
            oldRelativePath,
            newRelativePath);

        try
        {
            // Find existing document by old path
            var existingDoc = await _documentRepository.GetByPathAsync(
                oldRelativePath,
                tenantContext,
                cancellationToken);

            if (existingDoc is null)
            {
                _logger.LogDebug(
                    "Document not found for rename, treating as new: {NewPath}",
                    newRelativePath);

                // Treat as new document
                return await HandleDocumentCreatedAsync(
                    newFullPath,
                    tenantContext,
                    cancellationToken);
            }

            // Read new file content
            var content = await ReadFileContentAsync(newFullPath, cancellationToken);
            if (content is null)
            {
                return DocumentLifecycleResult.Failed(
                    DocumentLifecycleOperation.Renamed,
                    newRelativePath,
                    "File could not be read after rename");
            }

            // Check if content changed during rename
            var contentHash = ComputeContentHash(content);
            var contentChanged = existingDoc.ContentHash != contentHash;

            if (contentChanged)
            {
                _logger.LogDebug(
                    "Content changed during rename for {Path}, performing full re-index",
                    newRelativePath);

                // Delete old document and create new
                await _documentRepository.DeleteAsync(existingDoc.Id, cancellationToken);
                return await HandleDocumentCreatedAsync(
                    newFullPath,
                    tenantContext,
                    cancellationToken);
            }

            // Content unchanged - just update the path
            // Note: This requires a path update method in the repository
            // For now, we upsert with the same content but new path
            var parseResult = _markdownParser.Parse(content);
            if (!parseResult.IsValid)
            {
                return DocumentLifecycleResult.Failed(
                    DocumentLifecycleOperation.Renamed,
                    newRelativePath,
                    $"Markdown parse error: {parseResult.ErrorMessage}");
            }

            var document = existingDoc with
            {
                RelativePath = newRelativePath
            };

            await _documentRepository.UpsertAsync(document, null, cancellationToken);

            _logger.LogInformation(
                "Document renamed: {OldPath} -> {NewPath} (ID: {DocumentId})",
                oldRelativePath,
                newRelativePath,
                existingDoc.Id);

            return DocumentLifecycleResult.Succeeded(
                DocumentLifecycleOperation.Renamed,
                existingDoc.Id,
                newRelativePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to rename document: {OldPath} -> {NewPath}",
                oldRelativePath,
                newRelativePath);

            return DocumentLifecycleResult.Failed(
                DocumentLifecycleOperation.Renamed,
                newRelativePath,
                ex.Message);
        }
    }

    public async Task<BatchLifecycleResult> ProcessBatchAsync(
        IEnumerable<string> filePaths,
        TenantContext tenantContext,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var paths = filePaths.ToList();
        var results = new List<DocumentLifecycleResult>();

        _logger.LogInformation(
            "Starting batch processing of {Count} documents",
            paths.Count);

        foreach (var path in paths)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                var result = File.Exists(path)
                    ? await HandleDocumentCreatedAsync(path, tenantContext, cancellationToken)
                    : await HandleDocumentDeletedAsync(path, tenantContext, cancellationToken);

                results.Add(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Batch processing error for: {Path}", path);
                results.Add(DocumentLifecycleResult.Failed(
                    DocumentLifecycleOperation.Created,
                    GetRelativePath(path, tenantContext),
                    ex.Message));
            }
        }

        stopwatch.Stop();

        var batchResult = new BatchLifecycleResult
        {
            TotalCount = paths.Count,
            SuccessCount = results.Count(r => r.Success && !r.Skipped),
            FailedCount = results.Count(r => !r.Success),
            SkippedCount = results.Count(r => r.Skipped),
            Duration = stopwatch.Elapsed,
            FailedResults = results.Where(r => !r.Success).ToList()
        };

        _logger.LogInformation(
            "Batch processing completed: {Success} succeeded, {Failed} failed, {Skipped} skipped in {Duration}ms",
            batchResult.SuccessCount,
            batchResult.FailedCount,
            batchResult.SkippedCount,
            batchResult.Duration.TotalMilliseconds);

        return batchResult;
    }

    // --- Private Helper Methods ---

    private static string GetRelativePath(string fullPath, TenantContext tenantContext)
    {
        // Extract relative path from full path based on tenant context
        // This assumes fullPath is within the compounding docs directory
        var normalizedPath = fullPath.Replace('\\', '/');
        var docsMarker = "/csharp-compounding-docs/";
        var markerIndex = normalizedPath.IndexOf(docsMarker, StringComparison.OrdinalIgnoreCase);

        if (markerIndex >= 0)
        {
            return normalizedPath[(markerIndex + docsMarker.Length)..];
        }

        // Fallback to filename
        return Path.GetFileName(fullPath);
    }

    private static string GenerateDocumentId()
    {
        return Guid.NewGuid().ToString("N");
    }

    private static string ComputeContentHash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private async Task<string?> ReadFileContentAsync(
        string fullPath,
        CancellationToken cancellationToken)
    {
        try
        {
            return await File.ReadAllTextAsync(fullPath, cancellationToken);
        }
        catch (FileNotFoundException)
        {
            _logger.LogWarning("File not found: {Path}", fullPath);
            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Permission denied: {Path}", fullPath);
            return null;
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "IO error reading file: {Path}", fullPath);
            return null;
        }
    }

    private CompoundDocument CreateDocumentRecord(
        string documentId,
        string relativePath,
        ParsedDocument parsedDocument,
        string contentHash,
        ReadOnlyMemory<float> embedding,
        TenantContext tenantContext,
        string promotionLevel = "standard")
    {
        return new CompoundDocument
        {
            Id = documentId,
            ProjectName = tenantContext.ProjectName,
            BranchName = tenantContext.BranchName,
            PathHash = tenantContext.PathHash,
            RelativePath = relativePath,
            Title = parsedDocument.Title,
            Summary = parsedDocument.Summary,
            DocType = parsedDocument.DocType,
            PromotionLevel = promotionLevel,
            ContentHash = contentHash,
            CharCount = parsedDocument.Content.Length,
            FrontmatterJson = parsedDocument.FrontmatterJson,
            Embedding = embedding
        };
    }

    private async Task<IReadOnlyList<DocumentChunk>?> CreateChunksIfNeededAsync(
        CompoundDocument document,
        string content,
        TenantContext tenantContext,
        CancellationToken cancellationToken)
    {
        var lineCount = content.Split('\n').Length;

        if (lineCount <= LargeDocumentLineThreshold)
        {
            return null; // No chunking needed
        }

        _logger.LogDebug(
            "Document {Path} has {Lines} lines, creating chunks",
            document.RelativePath,
            lineCount);

        var chunkContents = _chunkingService.ChunkDocument(content);
        var chunks = new List<DocumentChunk>();

        foreach (var (chunkContent, index) in chunkContents.Select((c, i) => (c, i)))
        {
            var chunkEmbedding = await _embeddingService.GenerateEmbeddingAsync(
                chunkContent.Content,
                cancellationToken);

            var chunk = new DocumentChunk
            {
                Id = $"{document.Id}-chunk-{index:D4}",
                DocumentId = document.Id,
                ProjectName = tenantContext.ProjectName,
                BranchName = tenantContext.BranchName,
                PathHash = tenantContext.PathHash,
                PromotionLevel = document.PromotionLevel,
                ChunkIndex = index,
                HeaderPath = chunkContent.HeaderPath,
                Content = chunkContent.Content,
                Embedding = chunkEmbedding
            };

            chunks.Add(chunk);
        }

        _logger.LogDebug(
            "Created {ChunkCount} chunks for document {Path}",
            chunks.Count,
            document.RelativePath);

        return chunks;
    }
}
```

### 4. DocumentLifecycleEventHandler (File Watcher Integration)

```csharp
// src/CompoundDocs.McpServer/FileWatcher/DocumentLifecycleEventHandler.cs
namespace CompoundDocs.McpServer.FileWatcher;

/// <summary>
/// Bridges file watcher events to the document lifecycle manager.
/// Implements IFileEventHandler to receive debounced file system events.
/// </summary>
public sealed class DocumentLifecycleEventHandler : IFileEventHandler
{
    private readonly IDocumentLifecycleManager _lifecycleManager;
    private readonly ITenantContextProvider _tenantContextProvider;
    private readonly ILogger<DocumentLifecycleEventHandler> _logger;

    public DocumentLifecycleEventHandler(
        IDocumentLifecycleManager lifecycleManager,
        ITenantContextProvider tenantContextProvider,
        ILogger<DocumentLifecycleEventHandler> logger)
    {
        _lifecycleManager = lifecycleManager ?? throw new ArgumentNullException(nameof(lifecycleManager));
        _tenantContextProvider = tenantContextProvider ?? throw new ArgumentNullException(nameof(tenantContextProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> HandleAsync(
        DebouncedFileEvent eventArgs,
        CancellationToken cancellationToken = default)
    {
        var tenantContext = _tenantContextProvider.GetCurrentTenantContext();

        if (tenantContext is null)
        {
            _logger.LogWarning(
                "No active tenant context for file event: {Path}",
                eventArgs.RelativePath);
            return false;
        }

        _logger.LogDebug(
            "Handling {ChangeType} event for: {Path}",
            eventArgs.ChangeType,
            eventArgs.RelativePath);

        var result = eventArgs.ChangeType switch
        {
            WatcherChangeTypes.Created => await _lifecycleManager.HandleDocumentCreatedAsync(
                eventArgs.FullPath,
                tenantContext,
                cancellationToken),

            WatcherChangeTypes.Changed => await _lifecycleManager.HandleDocumentUpdatedAsync(
                eventArgs.FullPath,
                tenantContext,
                cancellationToken),

            WatcherChangeTypes.Deleted => await _lifecycleManager.HandleDocumentDeletedAsync(
                eventArgs.FullPath,
                tenantContext,
                cancellationToken),

            WatcherChangeTypes.Renamed => await _lifecycleManager.HandleDocumentRenamedAsync(
                eventArgs.OldPath!,
                eventArgs.FullPath,
                tenantContext,
                cancellationToken),

            _ => DocumentLifecycleResult.Failed(
                DocumentLifecycleOperation.Updated,
                eventArgs.RelativePath,
                $"Unhandled change type: {eventArgs.ChangeType}")
        };

        if (!result.Success)
        {
            _logger.LogWarning(
                "Document lifecycle operation failed: {Operation} on {Path} - {Error}",
                result.Operation,
                result.RelativePath,
                result.ErrorMessage);
        }

        return result.Success;
    }
}
```

### 5. Service Registration

```csharp
// In ServiceCollectionExtensions.cs
public static IServiceCollection AddDocumentLifecycleServices(
    this IServiceCollection services)
{
    // Lifecycle manager
    services.AddScoped<IDocumentLifecycleManager, DocumentLifecycleManager>();

    // File watcher event handler
    services.AddScoped<DocumentLifecycleEventHandler>();

    // Register as file event handler
    services.AddScoped<IFileEventHandler, DocumentLifecycleEventHandler>();

    return services;
}
```

---

## Dependencies

### Depends On

- **Phase 055**: File Event Handlers - Provides `IFileEventHandler` interface and `DebouncedFileEvent`
- **Phase 048**: Document Repository - Provides `IDocumentRepository` for database operations
- **Phase 015**: Markdown Parser - Provides `IMarkdownParser` for content parsing
- **Phase 014**: Schema Validation - Provides `ISchemaValidator` for frontmatter validation
- **Phase 029**: Embedding Service - Provides `IEmbeddingService` for vector generation
- **Phase 061**: Chunking Service - Provides `IChunkingService` for large document handling
- **Phase 053**: File Watcher Service - Provides file system event detection
- **Phase 054**: Event Debouncing - Provides debounced events to handlers

### Blocks

- **Phase 056**: Startup Reconciliation - Uses batch processing for initial sync
- **Phase 068**: Indexing Service - May orchestrate lifecycle operations
- **Phase 078**: Update Promotion Tool - Uses lifecycle manager for promotion updates

---

## Testing Verification

### Unit Tests

```csharp
// tests/CompoundDocs.Tests/Services/DocumentLifecycleManagerTests.cs
public class DocumentLifecycleManagerTests
{
    private readonly Mock<IDocumentRepository> _mockRepo;
    private readonly Mock<IMarkdownParser> _mockParser;
    private readonly Mock<ISchemaValidator> _mockValidator;
    private readonly Mock<IEmbeddingService> _mockEmbedding;
    private readonly Mock<IChunkingService> _mockChunking;
    private readonly DocumentLifecycleManager _manager;
    private readonly TenantContext _testTenant;

    public DocumentLifecycleManagerTests()
    {
        _mockRepo = new Mock<IDocumentRepository>();
        _mockParser = new Mock<IMarkdownParser>();
        _mockValidator = new Mock<ISchemaValidator>();
        _mockEmbedding = new Mock<IEmbeddingService>();
        _mockChunking = new Mock<IChunkingService>();

        _manager = new DocumentLifecycleManager(
            _mockRepo.Object,
            _mockParser.Object,
            _mockValidator.Object,
            _mockEmbedding.Object,
            _mockChunking.Object,
            Mock.Of<ILogger<DocumentLifecycleManager>>());

        _testTenant = TenantContext.Create("test-project", "main", "/test/path");
    }

    [Fact]
    public async Task HandleDocumentCreatedAsync_ValidDocument_ReturnsSuccess()
    {
        // Arrange
        SetupValidDocumentPipeline();

        // Act
        var result = await _manager.HandleDocumentCreatedAsync(
            "/test/path/csharp-compounding-docs/problem/test.md",
            _testTenant);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(DocumentLifecycleOperation.Created, result.Operation);
        Assert.NotNull(result.DocumentId);
        _mockRepo.Verify(r => r.UpsertAsync(It.IsAny<CompoundDocument>(), null, default), Times.Once);
    }

    [Fact]
    public async Task HandleDocumentCreatedAsync_SchemaValidationFails_ReturnsFailure()
    {
        // Arrange
        SetupParsedDocument();
        _mockValidator.Setup(v => v.Validate(It.IsAny<ParsedDocument>()))
            .Returns(new ValidationResult
            {
                IsValid = false,
                Errors = ["Missing required field: title"]
            });

        // Act
        var result = await _manager.HandleDocumentCreatedAsync(
            "/test/path/csharp-compounding-docs/problem/test.md",
            _testTenant);

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Missing required field: title", result.ValidationErrors);
        _mockRepo.Verify(r => r.UpsertAsync(It.IsAny<CompoundDocument>(), It.IsAny<IReadOnlyList<DocumentChunk>?>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleDocumentUpdatedAsync_ContentUnchanged_SkipsProcessing()
    {
        // Arrange
        var existingDoc = CreateExistingDocument();
        _mockRepo.Setup(r => r.GetByPathAsync(It.IsAny<string>(), _testTenant, default))
            .ReturnsAsync(existingDoc);

        // Same content hash
        var content = "# Test\nContent";
        // ... setup file read to return content with same hash as existingDoc

        // Act
        var result = await _manager.HandleDocumentUpdatedAsync(
            "/test/path/csharp-compounding-docs/problem/test.md",
            _testTenant);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.Skipped);
        Assert.Equal("Content unchanged (hash match)", result.SkipReason);
        _mockEmbedding.Verify(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleDocumentUpdatedAsync_ContentChanged_RegeneratesEmbedding()
    {
        // Arrange
        SetupValidDocumentPipeline();
        var existingDoc = CreateExistingDocument();
        existingDoc = existingDoc with { ContentHash = "different-hash" };
        _mockRepo.Setup(r => r.GetByPathAsync(It.IsAny<string>(), _testTenant, default))
            .ReturnsAsync(existingDoc);

        // Act
        var result = await _manager.HandleDocumentUpdatedAsync(
            "/test/path/csharp-compounding-docs/problem/test.md",
            _testTenant);

        // Assert
        Assert.True(result.Success);
        Assert.False(result.Skipped);
        _mockEmbedding.Verify(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), default), Times.Once);
    }

    [Fact]
    public async Task HandleDocumentUpdatedAsync_PreservesPromotionLevel()
    {
        // Arrange
        SetupValidDocumentPipeline();
        var existingDoc = CreateExistingDocument() with { PromotionLevel = "critical" };
        _mockRepo.Setup(r => r.GetByPathAsync(It.IsAny<string>(), _testTenant, default))
            .ReturnsAsync(existingDoc);

        CompoundDocument? savedDoc = null;
        _mockRepo.Setup(r => r.UpsertAsync(It.IsAny<CompoundDocument>(), It.IsAny<IReadOnlyList<DocumentChunk>?>(), default))
            .Callback<CompoundDocument, IReadOnlyList<DocumentChunk>?, CancellationToken>((d, _, _) => savedDoc = d);

        // Act
        await _manager.HandleDocumentUpdatedAsync(
            "/test/path/csharp-compounding-docs/problem/test.md",
            _testTenant);

        // Assert
        Assert.NotNull(savedDoc);
        Assert.Equal("critical", savedDoc.PromotionLevel);
    }

    [Fact]
    public async Task HandleDocumentDeletedAsync_ExistingDocument_RemovesFromDatabase()
    {
        // Arrange
        var existingDoc = CreateExistingDocument();
        _mockRepo.Setup(r => r.GetByPathAsync(It.IsAny<string>(), _testTenant, default))
            .ReturnsAsync(existingDoc);
        _mockRepo.Setup(r => r.DeleteAsync(existingDoc.Id, default))
            .ReturnsAsync(true);

        // Act
        var result = await _manager.HandleDocumentDeletedAsync(
            "/test/path/csharp-compounding-docs/problem/test.md",
            _testTenant);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(existingDoc.Id, result.DocumentId);
        _mockRepo.Verify(r => r.DeleteAsync(existingDoc.Id, default), Times.Once);
    }

    [Fact]
    public async Task HandleDocumentDeletedAsync_NonExistentDocument_SkipsGracefully()
    {
        // Arrange
        _mockRepo.Setup(r => r.GetByPathAsync(It.IsAny<string>(), _testTenant, default))
            .ReturnsAsync((CompoundDocument?)null);

        // Act
        var result = await _manager.HandleDocumentDeletedAsync(
            "/test/path/csharp-compounding-docs/problem/test.md",
            _testTenant);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.Skipped);
        _mockRepo.Verify(r => r.DeleteAsync(It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task HandleDocumentRenamedAsync_ContentUnchanged_UpdatesPathOnly()
    {
        // Arrange
        var existingDoc = CreateExistingDocument();
        _mockRepo.Setup(r => r.GetByPathAsync("problem/old.md", _testTenant, default))
            .ReturnsAsync(existingDoc);
        // Setup file read to return content with same hash

        // Act
        var result = await _manager.HandleDocumentRenamedAsync(
            "/test/path/csharp-compounding-docs/problem/old.md",
            "/test/path/csharp-compounding-docs/problem/new.md",
            _testTenant);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(DocumentLifecycleOperation.Renamed, result.Operation);
        _mockEmbedding.Verify(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), default), Times.Never);
    }

    [Fact]
    public async Task ProcessBatchAsync_MultipleDocuments_ReturnsAggregateResults()
    {
        // Arrange
        SetupValidDocumentPipeline();
        var paths = new[]
        {
            "/test/path/csharp-compounding-docs/problem/doc1.md",
            "/test/path/csharp-compounding-docs/problem/doc2.md",
            "/test/path/csharp-compounding-docs/problem/doc3.md"
        };

        // Act
        var result = await _manager.ProcessBatchAsync(paths, _testTenant);

        // Assert
        Assert.Equal(3, result.TotalCount);
        Assert.True(result.Duration > TimeSpan.Zero);
    }

    // Helper methods
    private void SetupValidDocumentPipeline()
    {
        SetupParsedDocument();
        _mockValidator.Setup(v => v.Validate(It.IsAny<ParsedDocument>()))
            .Returns(new ValidationResult { IsValid = true });
        _mockEmbedding.Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), default))
            .ReturnsAsync(new float[1024]);
    }

    private void SetupParsedDocument()
    {
        _mockParser.Setup(p => p.Parse(It.IsAny<string>()))
            .Returns(new ParseResult
            {
                IsValid = true,
                Document = new ParsedDocument
                {
                    Title = "Test Document",
                    Summary = "Test summary",
                    DocType = "problem",
                    Content = "Test content"
                }
            });
    }

    private CompoundDocument CreateExistingDocument() => new()
    {
        Id = "existing-id",
        ProjectName = _testTenant.ProjectName,
        BranchName = _testTenant.BranchName,
        PathHash = _testTenant.PathHash,
        RelativePath = "problem/test.md",
        Title = "Existing Document",
        DocType = "problem",
        PromotionLevel = "standard",
        ContentHash = "existing-hash",
        CharCount = 100,
        Embedding = new float[1024]
    };
}
```

### Integration Tests

```csharp
// tests/CompoundDocs.IntegrationTests/Services/DocumentLifecycleIntegrationTests.cs
[Trait("Category", "Integration")]
public class DocumentLifecycleIntegrationTests : IClassFixture<DatabaseFixture>, IDisposable
{
    private readonly IDocumentLifecycleManager _lifecycleManager;
    private readonly IDocumentRepository _repository;
    private readonly TenantContext _tenant;
    private readonly string _testDocsDir;

    public DocumentLifecycleIntegrationTests(DatabaseFixture fixture)
    {
        _lifecycleManager = fixture.GetService<IDocumentLifecycleManager>();
        _repository = fixture.GetService<IDocumentRepository>();
        _tenant = TenantContext.Create("test-project", "main", "/test/path");

        _testDocsDir = Path.Combine(Path.GetTempPath(), $"lifecycle-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(Path.Combine(_testDocsDir, "csharp-compounding-docs", "problem"));
    }

    [Fact]
    public async Task FullLifecycle_CreateUpdateDelete_WorksCorrectly()
    {
        // Arrange
        var docPath = Path.Combine(_testDocsDir, "csharp-compounding-docs", "problem", "test-lifecycle.md");
        var initialContent = CreateValidMarkdown("Initial Title", "Initial summary");
        await File.WriteAllTextAsync(docPath, initialContent);

        // Act 1: Create
        var createResult = await _lifecycleManager.HandleDocumentCreatedAsync(docPath, _tenant);

        // Assert 1
        Assert.True(createResult.Success);
        Assert.NotNull(createResult.DocumentId);

        var created = await _repository.GetByIdAsync(createResult.DocumentId);
        Assert.NotNull(created);
        Assert.Equal("Initial Title", created.Title);

        // Act 2: Update
        var updatedContent = CreateValidMarkdown("Updated Title", "Updated summary");
        await File.WriteAllTextAsync(docPath, updatedContent);
        var updateResult = await _lifecycleManager.HandleDocumentUpdatedAsync(docPath, _tenant);

        // Assert 2
        Assert.True(updateResult.Success);
        var updated = await _repository.GetByIdAsync(createResult.DocumentId);
        Assert.Equal("Updated Title", updated?.Title);

        // Act 3: Delete
        File.Delete(docPath);
        var deleteResult = await _lifecycleManager.HandleDocumentDeletedAsync(docPath, _tenant);

        // Assert 3
        Assert.True(deleteResult.Success);
        var deleted = await _repository.GetByIdAsync(createResult.DocumentId);
        Assert.Null(deleted);
    }

    [Fact]
    public async Task LargeDocument_CreatesChunks()
    {
        // Arrange
        var docPath = Path.Combine(_testDocsDir, "csharp-compounding-docs", "problem", "large-doc.md");
        var largeContent = CreateLargeMarkdown(600); // 600 lines
        await File.WriteAllTextAsync(docPath, largeContent);

        // Act
        var result = await _lifecycleManager.HandleDocumentCreatedAsync(docPath, _tenant);

        // Assert
        Assert.True(result.Success);
        Assert.True(result.ChunkCount > 0);
    }

    private static string CreateValidMarkdown(string title, string summary) => $"""
        ---
        doc_type: problem
        title: "{title}"
        date: "2024-01-15"
        summary: "{summary}"
        significance: behavioral
        ---

        # {title}

        {summary}
        """;

    private static string CreateLargeMarkdown(int lines)
    {
        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine("doc_type: problem");
        sb.AppendLine("title: \"Large Document\"");
        sb.AppendLine("date: \"2024-01-15\"");
        sb.AppendLine("summary: \"A large document for chunking\"");
        sb.AppendLine("significance: behavioral");
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("# Large Document");
        sb.AppendLine();

        for (int i = 0; i < lines; i++)
        {
            sb.AppendLine($"Line {i}: Lorem ipsum dolor sit amet, consectetur adipiscing elit.");
        }

        return sb.ToString();
    }

    public void Dispose()
    {
        try { Directory.Delete(_testDocsDir, recursive: true); } catch { }
    }
}
```

---

## Files Created/Modified

| File | Action | Description |
|------|--------|-------------|
| `src/CompoundDocs.McpServer/Services/IDocumentLifecycleManager.cs` | Create | Lifecycle manager interface |
| `src/CompoundDocs.McpServer/Services/DocumentLifecycleResult.cs` | Create | Result records and enums |
| `src/CompoundDocs.McpServer/Services/DocumentLifecycleManager.cs` | Create | Main lifecycle implementation |
| `src/CompoundDocs.McpServer/FileWatcher/DocumentLifecycleEventHandler.cs` | Create | File watcher integration |
| `src/CompoundDocs.McpServer/Extensions/ServiceCollectionExtensions.cs` | Modify | Add lifecycle service registration |
| `tests/CompoundDocs.Tests/Services/DocumentLifecycleManagerTests.cs` | Create | Unit tests |
| `tests/CompoundDocs.IntegrationTests/Services/DocumentLifecycleIntegrationTests.cs` | Create | Integration tests |

---

## Logging Reference

| Level | Events |
|-------|--------|
| Debug | Processing start for each operation, content hash comparisons, skip decisions |
| Info | Successful create/update/delete operations with document ID and path |
| Warning | Schema validation failures, file not found, operation failures |
| Error | Unrecoverable errors with stack traces |

---

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| Embedding service unavailable | Retry with exponential backoff (handled by Phase 029) |
| Database write failure | Retry with exponential backoff, transaction rollback |
| Large batch processing timeout | Process in configurable batch sizes with progress logging |
| Race condition between events | Debouncing (Phase 054) prevents rapid duplicate processing |
| Memory pressure from large documents | Chunking service handles large documents incrementally |
| Content hash collision | SHA256 has negligible collision probability |
| Promotion level lost on update | Explicitly preserve promotion level from existing document |

---

## Sequence Diagrams

### Document Creation Flow

```
FileSystemWatcher    Debouncer       LifecycleManager    Parser    Validator   Embedding    Repository
       |                 |                  |               |           |           |            |
       |--Created------->|                  |               |           |           |            |
       |                 |--[500ms wait]--->|               |           |           |            |
       |                 |--DebouncedEvent->|               |           |           |            |
       |                 |                  |--Parse------->|           |           |            |
       |                 |                  |<--Document----|           |           |            |
       |                 |                  |--Validate--------------->|           |            |
       |                 |                  |<--Valid-------------------|           |            |
       |                 |                  |--GenerateEmbedding----------------->|            |
       |                 |                  |<--Vector-------------------------|            |
       |                 |                  |--Upsert---------------------------------------->|
       |                 |                  |<--Success---------------------------------------|
```

### Document Update Flow (Content Changed)

```
LifecycleManager    Repository    Parser    Validator   Embedding
       |                |            |           |           |
       |--GetByPath---->|            |           |           |
       |<--ExistingDoc--|            |           |           |
       |--[Hash Compare: Different]--|           |           |
       |--Parse-------------------->|           |           |
       |<--Document-----------------|           |           |
       |--Validate-------------------------->|           |
       |<--Valid-----------------------------|           |
       |--GenerateEmbedding----------------------------->|
       |<--Vector----------------------------------------|
       |--Upsert (same ID)-->|           |           |           |
       |<--Success-----------|           |           |           |
```
