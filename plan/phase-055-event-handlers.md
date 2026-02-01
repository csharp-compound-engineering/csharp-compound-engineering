# Phase 055: File Event Handlers (Create/Modify/Delete/Rename)

> **Status**: PLANNED
> **Category**: Document Processing
> **Estimated Effort**: M
> **Prerequisites**: Phase 054 (File Watcher Core)

---

## Spec References

- [mcp-server/file-watcher.md - Events](../spec/mcp-server/file-watcher.md#events)
- [mcp-server/file-watcher.md - Event Processing](../spec/mcp-server/file-watcher.md#event-processing)
- [mcp-server/file-watcher.md - Error Handling](../spec/mcp-server/file-watcher.md#error-handling)
- [mcp-server/file-watcher.md - Logging](../spec/mcp-server/file-watcher.md#logging)

---

## Objectives

1. Implement `OnCreated` handler to index new documents with embedding generation
2. Implement `OnChanged` handler to re-generate embeddings and upsert to database
3. Implement `OnDeleted` handler to remove documents and chunks from database
4. Implement `OnRenamed` handler to update paths while preserving embeddings when content is unchanged
5. Ensure handler error isolation so one handler failure doesn't affect others
6. Add comprehensive logging for each event type at appropriate log levels

---

## Acceptance Criteria

- [ ] `OnCreated` handler parses markdown, validates schema, generates embedding, and inserts to DB
- [ ] `OnChanged` handler detects content changes, re-generates embedding, and upserts to DB
- [ ] `OnDeleted` handler removes parent document and all associated chunks from DB
- [ ] `OnRenamed` handler updates file path in DB, regenerates embedding only if content changed
- [ ] Each handler is isolated with try-catch to prevent cascading failures
- [ ] File system errors (not found, permission denied, path too long) are handled gracefully
- [ ] Processing errors (schema validation, embedding, DB) have appropriate retry/skip logic
- [ ] Logging follows spec levels: Debug for all events, Info for operations, Warning for recoverable errors, Error for unrecoverable
- [ ] Unit tests cover each handler with mocked dependencies
- [ ] Integration tests verify full event-to-database flow

---

## Implementation Notes

### 1. Event Handler Interface

Define a common interface for all file event handlers:

```csharp
// src/CompoundDocs.McpServer/FileWatcher/IFileEventHandler.cs
namespace CompoundDocs.McpServer.FileWatcher;

/// <summary>
/// Handles a specific type of file system event.
/// </summary>
public interface IFileEventHandler
{
    /// <summary>
    /// Handles the file system event.
    /// </summary>
    /// <param name="eventArgs">The debounced file event arguments.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if handled successfully, false if error occurred.</returns>
    Task<bool> HandleAsync(
        DebouncedFileEvent eventArgs,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a debounced file system event.
/// </summary>
public record DebouncedFileEvent(
    string FullPath,
    string RelativePath,
    WatcherChangeTypes ChangeType,
    string? OldPath = null);
```

### 2. OnCreated Handler

```csharp
// src/CompoundDocs.McpServer/FileWatcher/Handlers/FileCreatedHandler.cs
using CompoundDocs.Common.Services;
using CompoundDocs.McpServer.Services;

namespace CompoundDocs.McpServer.FileWatcher.Handlers;

/// <summary>
/// Handles file creation events by indexing new documents.
/// </summary>
public sealed class FileCreatedHandler : IFileEventHandler
{
    private readonly IMarkdownParser _markdownParser;
    private readonly ISchemaValidator _schemaValidator;
    private readonly IEmbeddingService _embeddingService;
    private readonly IDocumentRepository _documentRepository;
    private readonly IChunkingService _chunkingService;
    private readonly ILogger<FileCreatedHandler> _logger;

    public FileCreatedHandler(
        IMarkdownParser markdownParser,
        ISchemaValidator schemaValidator,
        IEmbeddingService embeddingService,
        IDocumentRepository documentRepository,
        IChunkingService chunkingService,
        ILogger<FileCreatedHandler> logger)
    {
        _markdownParser = markdownParser;
        _schemaValidator = schemaValidator;
        _embeddingService = embeddingService;
        _documentRepository = documentRepository;
        _chunkingService = chunkingService;
        _logger = logger;
    }

    public async Task<bool> HandleAsync(
        DebouncedFileEvent eventArgs,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Processing created file: {Path}", eventArgs.RelativePath);

        try
        {
            // Read file content
            var content = await ReadFileContentAsync(eventArgs.FullPath, cancellationToken);
            if (content is null)
            {
                return false; // File not found or unreadable
            }

            // Parse markdown
            var parseResult = _markdownParser.Parse(content);
            if (!parseResult.IsValid)
            {
                _logger.LogWarning(
                    "Markdown parse failed for {Path}: {Error}",
                    eventArgs.RelativePath,
                    parseResult.ErrorMessage);
                return false;
            }

            // Validate schema
            var validationResult = _schemaValidator.Validate(parseResult.Document);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning(
                    "Schema validation failed for {Path}: {Errors}",
                    eventArgs.RelativePath,
                    string.Join(", ", validationResult.Errors));
                return false;
            }

            // Compute content hash
            var contentHash = ComputeContentHash(content);

            // Generate embedding for document
            var embedding = await _embeddingService.GenerateEmbeddingAsync(
                parseResult.Document.GetEmbeddingContent(),
                cancellationToken);

            // Create document record
            var document = new DocumentRecord
            {
                Id = Guid.NewGuid(),
                FilePath = eventArgs.RelativePath,
                ContentHash = contentHash,
                Embedding = embedding.ToArray(),
                Title = parseResult.Document.Title,
                DocumentType = parseResult.Document.Type,
                Status = parseResult.Document.Status,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Insert to database
            await _documentRepository.InsertAsync(document, cancellationToken);

            _logger.LogInformation(
                "Indexed new document: {Path} (ID: {Id})",
                eventArgs.RelativePath,
                document.Id);

            // Handle chunking for large documents
            var lineCount = content.Split('\n').Length;
            if (lineCount > 500)
            {
                await ProcessChunksAsync(document, content, cancellationToken);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to index created file: {Path}", eventArgs.RelativePath);
            return false;
        }
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
            _logger.LogWarning("File not found (may have been deleted): {Path}", fullPath);
            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Permission denied reading file: {Path}", fullPath);
            return null;
        }
        catch (PathTooLongException ex)
        {
            _logger.LogError(ex, "Path too long: {Path}", fullPath);
            return null;
        }
        catch (IOException ex) when (ex.Message.Contains("UTF-8"))
        {
            _logger.LogWarning("Invalid UTF-8 encoding in file: {Path}", fullPath);
            return null;
        }
    }

    private static string ComputeContentHash(string content)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private async Task ProcessChunksAsync(
        DocumentRecord document,
        string content,
        CancellationToken cancellationToken)
    {
        var chunks = _chunkingService.ChunkDocument(content);

        foreach (var chunk in chunks)
        {
            var chunkEmbedding = await _embeddingService.GenerateEmbeddingAsync(
                chunk.Content,
                cancellationToken);

            var chunkRecord = new ChunkRecord
            {
                Id = Guid.NewGuid(),
                ParentDocumentId = document.Id,
                ChunkIndex = chunk.Index,
                StartLine = chunk.StartLine,
                EndLine = chunk.EndLine,
                Content = chunk.Content,
                Embedding = chunkEmbedding.ToArray(),
                CreatedAt = DateTime.UtcNow
            };

            await _documentRepository.InsertChunkAsync(chunkRecord, cancellationToken);
        }

        _logger.LogInformation(
            "Created {Count} chunks for document: {Path}",
            chunks.Count,
            document.FilePath);
    }
}
```

### 3. OnChanged Handler

```csharp
// src/CompoundDocs.McpServer/FileWatcher/Handlers/FileChangedHandler.cs
namespace CompoundDocs.McpServer.FileWatcher.Handlers;

/// <summary>
/// Handles file modification events by updating document embeddings.
/// </summary>
public sealed class FileChangedHandler : IFileEventHandler
{
    private readonly IMarkdownParser _markdownParser;
    private readonly ISchemaValidator _schemaValidator;
    private readonly IEmbeddingService _embeddingService;
    private readonly IDocumentRepository _documentRepository;
    private readonly IChunkingService _chunkingService;
    private readonly ILogger<FileChangedHandler> _logger;

    public FileChangedHandler(
        IMarkdownParser markdownParser,
        ISchemaValidator schemaValidator,
        IEmbeddingService embeddingService,
        IDocumentRepository documentRepository,
        IChunkingService chunkingService,
        ILogger<FileChangedHandler> logger)
    {
        _markdownParser = markdownParser;
        _schemaValidator = schemaValidator;
        _embeddingService = embeddingService;
        _documentRepository = documentRepository;
        _chunkingService = chunkingService;
        _logger = logger;
    }

    public async Task<bool> HandleAsync(
        DebouncedFileEvent eventArgs,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Processing modified file: {Path}", eventArgs.RelativePath);

        try
        {
            // Read file content
            var content = await ReadFileContentAsync(eventArgs.FullPath, cancellationToken);
            if (content is null)
            {
                return false;
            }

            // Compute content hash
            var contentHash = ComputeContentHash(content);

            // Check if content actually changed
            var existingDoc = await _documentRepository.GetByPathAsync(
                eventArgs.RelativePath,
                cancellationToken);

            if (existingDoc is not null && existingDoc.ContentHash == contentHash)
            {
                _logger.LogDebug(
                    "Content unchanged for {Path}, skipping update",
                    eventArgs.RelativePath);
                return true;
            }

            // Parse markdown
            var parseResult = _markdownParser.Parse(content);
            if (!parseResult.IsValid)
            {
                _logger.LogWarning(
                    "Markdown parse failed for {Path}: {Error}",
                    eventArgs.RelativePath,
                    parseResult.ErrorMessage);
                return false;
            }

            // Validate schema
            var validationResult = _schemaValidator.Validate(parseResult.Document);
            if (!validationResult.IsValid)
            {
                _logger.LogWarning(
                    "Schema validation failed for {Path}: {Errors}",
                    eventArgs.RelativePath,
                    string.Join(", ", validationResult.Errors));
                return false;
            }

            // Generate new embedding
            var embedding = await _embeddingService.GenerateEmbeddingAsync(
                parseResult.Document.GetEmbeddingContent(),
                cancellationToken);

            // Update or insert document
            var document = new DocumentRecord
            {
                Id = existingDoc?.Id ?? Guid.NewGuid(),
                FilePath = eventArgs.RelativePath,
                ContentHash = contentHash,
                Embedding = embedding.ToArray(),
                Title = parseResult.Document.Title,
                DocumentType = parseResult.Document.Type,
                Status = parseResult.Document.Status,
                CreatedAt = existingDoc?.CreatedAt ?? DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _documentRepository.UpsertAsync(document, cancellationToken);

            _logger.LogInformation(
                "Updated document: {Path} (ID: {Id})",
                eventArgs.RelativePath,
                document.Id);

            // Regenerate chunks if needed
            var lineCount = content.Split('\n').Length;
            if (lineCount > 500)
            {
                // Delete existing chunks first
                await _documentRepository.DeleteChunksByDocumentIdAsync(
                    document.Id,
                    cancellationToken);

                await ProcessChunksAsync(document, content, cancellationToken);
            }
            else if (existingDoc is not null)
            {
                // Document no longer needs chunking, remove any existing chunks
                await _documentRepository.DeleteChunksByDocumentIdAsync(
                    existingDoc.Id,
                    cancellationToken);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update modified file: {Path}", eventArgs.RelativePath);
            return false;
        }
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
            _logger.LogWarning("File not found (may have been deleted): {Path}", fullPath);
            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Permission denied reading file: {Path}", fullPath);
            return null;
        }
        catch (PathTooLongException ex)
        {
            _logger.LogError(ex, "Path too long: {Path}", fullPath);
            return null;
        }
    }

    private static string ComputeContentHash(string content)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private async Task ProcessChunksAsync(
        DocumentRecord document,
        string content,
        CancellationToken cancellationToken)
    {
        var chunks = _chunkingService.ChunkDocument(content);

        foreach (var chunk in chunks)
        {
            var chunkEmbedding = await _embeddingService.GenerateEmbeddingAsync(
                chunk.Content,
                cancellationToken);

            var chunkRecord = new ChunkRecord
            {
                Id = Guid.NewGuid(),
                ParentDocumentId = document.Id,
                ChunkIndex = chunk.Index,
                StartLine = chunk.StartLine,
                EndLine = chunk.EndLine,
                Content = chunk.Content,
                Embedding = chunkEmbedding.ToArray(),
                CreatedAt = DateTime.UtcNow
            };

            await _documentRepository.InsertChunkAsync(chunkRecord, cancellationToken);
        }

        _logger.LogInformation(
            "Regenerated {Count} chunks for document: {Path}",
            chunks.Count,
            document.FilePath);
    }
}
```

### 4. OnDeleted Handler

```csharp
// src/CompoundDocs.McpServer/FileWatcher/Handlers/FileDeletedHandler.cs
namespace CompoundDocs.McpServer.FileWatcher.Handlers;

/// <summary>
/// Handles file deletion events by removing documents from the database.
/// </summary>
public sealed class FileDeletedHandler : IFileEventHandler
{
    private readonly IDocumentRepository _documentRepository;
    private readonly ILogger<FileDeletedHandler> _logger;

    public FileDeletedHandler(
        IDocumentRepository documentRepository,
        ILogger<FileDeletedHandler> logger)
    {
        _documentRepository = documentRepository;
        _logger = logger;
    }

    public async Task<bool> HandleAsync(
        DebouncedFileEvent eventArgs,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Processing deleted file: {Path}", eventArgs.RelativePath);

        try
        {
            // Find existing document
            var document = await _documentRepository.GetByPathAsync(
                eventArgs.RelativePath,
                cancellationToken);

            if (document is null)
            {
                _logger.LogDebug(
                    "Document not found in database for deleted file: {Path}",
                    eventArgs.RelativePath);
                return true; // Not an error - file may never have been indexed
            }

            // Delete chunks first (foreign key constraint)
            var chunksDeleted = await _documentRepository.DeleteChunksByDocumentIdAsync(
                document.Id,
                cancellationToken);

            if (chunksDeleted > 0)
            {
                _logger.LogDebug(
                    "Deleted {Count} chunks for document: {Path}",
                    chunksDeleted,
                    eventArgs.RelativePath);
            }

            // Delete document
            await _documentRepository.DeleteAsync(document.Id, cancellationToken);

            _logger.LogInformation(
                "Removed document from database: {Path} (ID: {Id})",
                eventArgs.RelativePath,
                document.Id);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove deleted file: {Path}", eventArgs.RelativePath);
            return false;
        }
    }
}
```

### 5. OnRenamed Handler

```csharp
// src/CompoundDocs.McpServer/FileWatcher/Handlers/FileRenamedHandler.cs
namespace CompoundDocs.McpServer.FileWatcher.Handlers;

/// <summary>
/// Handles file rename events by updating paths and conditionally regenerating embeddings.
/// </summary>
public sealed class FileRenamedHandler : IFileEventHandler
{
    private readonly IMarkdownParser _markdownParser;
    private readonly ISchemaValidator _schemaValidator;
    private readonly IEmbeddingService _embeddingService;
    private readonly IDocumentRepository _documentRepository;
    private readonly ILogger<FileRenamedHandler> _logger;

    public FileRenamedHandler(
        IMarkdownParser markdownParser,
        ISchemaValidator schemaValidator,
        IEmbeddingService embeddingService,
        IDocumentRepository documentRepository,
        ILogger<FileRenamedHandler> logger)
    {
        _markdownParser = markdownParser;
        _schemaValidator = schemaValidator;
        _embeddingService = embeddingService;
        _documentRepository = documentRepository;
        _logger = logger;
    }

    public async Task<bool> HandleAsync(
        DebouncedFileEvent eventArgs,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Processing renamed file: {OldPath} -> {NewPath}",
            eventArgs.OldPath,
            eventArgs.RelativePath);

        if (string.IsNullOrEmpty(eventArgs.OldPath))
        {
            _logger.LogWarning(
                "Rename event missing old path for: {Path}",
                eventArgs.RelativePath);
            return false;
        }

        try
        {
            // Find existing document by old path
            var document = await _documentRepository.GetByPathAsync(
                eventArgs.OldPath,
                cancellationToken);

            if (document is null)
            {
                _logger.LogDebug(
                    "Document not found for renamed file, treating as new: {Path}",
                    eventArgs.RelativePath);

                // Treat as a new file - delegate to created handler logic
                // This can happen if the old file was never indexed
                return await HandleAsNewFileAsync(eventArgs, cancellationToken);
            }

            // Read current content
            var content = await ReadFileContentAsync(eventArgs.FullPath, cancellationToken);
            if (content is null)
            {
                return false;
            }

            // Check if content changed during rename (some editors do this)
            var contentHash = ComputeContentHash(content);
            var contentChanged = document.ContentHash != contentHash;

            if (contentChanged)
            {
                _logger.LogDebug(
                    "Content changed during rename for {Path}, regenerating embedding",
                    eventArgs.RelativePath);

                // Parse and validate
                var parseResult = _markdownParser.Parse(content);
                if (!parseResult.IsValid)
                {
                    _logger.LogWarning(
                        "Markdown parse failed for renamed file {Path}: {Error}",
                        eventArgs.RelativePath,
                        parseResult.ErrorMessage);
                    return false;
                }

                var validationResult = _schemaValidator.Validate(parseResult.Document);
                if (!validationResult.IsValid)
                {
                    _logger.LogWarning(
                        "Schema validation failed for renamed file {Path}: {Errors}",
                        eventArgs.RelativePath,
                        string.Join(", ", validationResult.Errors));
                    return false;
                }

                // Generate new embedding
                var embedding = await _embeddingService.GenerateEmbeddingAsync(
                    parseResult.Document.GetEmbeddingContent(),
                    cancellationToken);

                document = document with
                {
                    FilePath = eventArgs.RelativePath,
                    ContentHash = contentHash,
                    Embedding = embedding.ToArray(),
                    Title = parseResult.Document.Title,
                    DocumentType = parseResult.Document.Type,
                    Status = parseResult.Document.Status,
                    UpdatedAt = DateTime.UtcNow
                };

                await _documentRepository.UpsertAsync(document, cancellationToken);

                _logger.LogInformation(
                    "Renamed and updated document: {OldPath} -> {NewPath} (ID: {Id})",
                    eventArgs.OldPath,
                    eventArgs.RelativePath,
                    document.Id);
            }
            else
            {
                // Content unchanged - just update the path
                await _documentRepository.UpdatePathAsync(
                    document.Id,
                    eventArgs.RelativePath,
                    cancellationToken);

                _logger.LogInformation(
                    "Renamed document (content unchanged): {OldPath} -> {NewPath} (ID: {Id})",
                    eventArgs.OldPath,
                    eventArgs.RelativePath,
                    document.Id);
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to process renamed file: {OldPath} -> {NewPath}",
                eventArgs.OldPath,
                eventArgs.RelativePath);
            return false;
        }
    }

    private async Task<bool> HandleAsNewFileAsync(
        DebouncedFileEvent eventArgs,
        CancellationToken cancellationToken)
    {
        // Simplified new file handling for rename scenarios
        var content = await ReadFileContentAsync(eventArgs.FullPath, cancellationToken);
        if (content is null)
        {
            return false;
        }

        var parseResult = _markdownParser.Parse(content);
        if (!parseResult.IsValid)
        {
            _logger.LogWarning(
                "Markdown parse failed for {Path}: {Error}",
                eventArgs.RelativePath,
                parseResult.ErrorMessage);
            return false;
        }

        var validationResult = _schemaValidator.Validate(parseResult.Document);
        if (!validationResult.IsValid)
        {
            _logger.LogWarning(
                "Schema validation failed for {Path}: {Errors}",
                eventArgs.RelativePath,
                string.Join(", ", validationResult.Errors));
            return false;
        }

        var contentHash = ComputeContentHash(content);
        var embedding = await _embeddingService.GenerateEmbeddingAsync(
            parseResult.Document.GetEmbeddingContent(),
            cancellationToken);

        var document = new DocumentRecord
        {
            Id = Guid.NewGuid(),
            FilePath = eventArgs.RelativePath,
            ContentHash = contentHash,
            Embedding = embedding.ToArray(),
            Title = parseResult.Document.Title,
            DocumentType = parseResult.Document.Type,
            Status = parseResult.Document.Status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _documentRepository.InsertAsync(document, cancellationToken);

        _logger.LogInformation(
            "Indexed renamed file as new document: {Path} (ID: {Id})",
            eventArgs.RelativePath,
            document.Id);

        return true;
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
            _logger.LogError(ex, "Permission denied reading file: {Path}", fullPath);
            return null;
        }
        catch (PathTooLongException ex)
        {
            _logger.LogError(ex, "Path too long: {Path}", fullPath);
            return null;
        }
    }

    private static string ComputeContentHash(string content)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
```

### 6. Event Dispatcher with Error Isolation

```csharp
// src/CompoundDocs.McpServer/FileWatcher/FileEventDispatcher.cs
namespace CompoundDocs.McpServer.FileWatcher;

/// <summary>
/// Dispatches file events to appropriate handlers with error isolation.
/// </summary>
public sealed class FileEventDispatcher
{
    private readonly FileCreatedHandler _createdHandler;
    private readonly FileChangedHandler _changedHandler;
    private readonly FileDeletedHandler _deletedHandler;
    private readonly FileRenamedHandler _renamedHandler;
    private readonly ILogger<FileEventDispatcher> _logger;

    public FileEventDispatcher(
        FileCreatedHandler createdHandler,
        FileChangedHandler changedHandler,
        FileDeletedHandler deletedHandler,
        FileRenamedHandler renamedHandler,
        ILogger<FileEventDispatcher> logger)
    {
        _createdHandler = createdHandler;
        _changedHandler = changedHandler;
        _deletedHandler = deletedHandler;
        _renamedHandler = renamedHandler;
        _logger = logger;
    }

    /// <summary>
    /// Dispatches a debounced file event to the appropriate handler.
    /// Errors are isolated per handler - one failure doesn't affect others.
    /// </summary>
    public async Task<bool> DispatchAsync(
        DebouncedFileEvent fileEvent,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Dispatching {ChangeType} event for: {Path}",
            fileEvent.ChangeType,
            fileEvent.RelativePath);

        try
        {
            return fileEvent.ChangeType switch
            {
                WatcherChangeTypes.Created => await HandleWithIsolationAsync(
                    _createdHandler,
                    fileEvent,
                    "Created",
                    cancellationToken),

                WatcherChangeTypes.Changed => await HandleWithIsolationAsync(
                    _changedHandler,
                    fileEvent,
                    "Changed",
                    cancellationToken),

                WatcherChangeTypes.Deleted => await HandleWithIsolationAsync(
                    _deletedHandler,
                    fileEvent,
                    "Deleted",
                    cancellationToken),

                WatcherChangeTypes.Renamed => await HandleWithIsolationAsync(
                    _renamedHandler,
                    fileEvent,
                    "Renamed",
                    cancellationToken),

                _ => LogUnhandledEvent(fileEvent)
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug(
                "Event dispatch cancelled for: {Path}",
                fileEvent.RelativePath);
            throw;
        }
        catch (Exception ex)
        {
            // This should never happen since HandleWithIsolationAsync catches exceptions
            _logger.LogError(
                ex,
                "Unexpected error dispatching event for: {Path}",
                fileEvent.RelativePath);
            return false;
        }
    }

    private async Task<bool> HandleWithIsolationAsync(
        IFileEventHandler handler,
        DebouncedFileEvent fileEvent,
        string eventType,
        CancellationToken cancellationToken)
    {
        try
        {
            return await handler.HandleAsync(fileEvent, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw; // Propagate cancellation
        }
        catch (Exception ex)
        {
            // Isolate handler errors - log and return false
            _logger.LogError(
                ex,
                "{EventType} handler failed for: {Path}",
                eventType,
                fileEvent.RelativePath);
            return false;
        }
    }

    private bool LogUnhandledEvent(DebouncedFileEvent fileEvent)
    {
        _logger.LogWarning(
            "Unhandled file event type {ChangeType} for: {Path}",
            fileEvent.ChangeType,
            fileEvent.RelativePath);
        return false;
    }
}
```

### 7. Service Registration

```csharp
// In ServiceCollectionExtensions.cs
public static IServiceCollection AddFileEventHandlers(this IServiceCollection services)
{
    // Register individual handlers
    services.AddScoped<FileCreatedHandler>();
    services.AddScoped<FileChangedHandler>();
    services.AddScoped<FileDeletedHandler>();
    services.AddScoped<FileRenamedHandler>();

    // Register dispatcher
    services.AddScoped<FileEventDispatcher>();

    return services;
}
```

---

## Dependencies

### Depends On

- **Phase 054**: File Watcher Core - Provides debounced events to handlers
- **Phase 015**: Markdown Parser - Parses markdown content
- **Phase 014**: Schema Validation - Validates document schema
- **Phase 029**: Embedding Service - Generates document embeddings
- **Phase 038+**: Document Repository - Database operations

### Blocks

- **Phase 056**: Startup Reconciliation - Uses handlers for sync operations
- **Phase 057+**: External Docs Watcher - Reuses handler patterns

---

## Testing Verification

### Unit Tests

```csharp
// tests/CompoundDocs.Tests/FileWatcher/Handlers/FileCreatedHandlerTests.cs
public class FileCreatedHandlerTests
{
    [Fact]
    public async Task HandleAsync_ValidDocument_IndexesSuccessfully()
    {
        // Arrange
        var mockParser = new Mock<IMarkdownParser>();
        mockParser.Setup(p => p.Parse(It.IsAny<string>()))
            .Returns(new ParseResult { IsValid = true, Document = new MockDocument() });

        var mockValidator = new Mock<ISchemaValidator>();
        mockValidator.Setup(v => v.Validate(It.IsAny<IDocument>()))
            .Returns(ValidationResult.Success);

        var mockEmbedding = new Mock<IEmbeddingService>();
        mockEmbedding.Setup(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), default))
            .ReturnsAsync(new float[1024]);

        var mockRepo = new Mock<IDocumentRepository>();

        var handler = new FileCreatedHandler(
            mockParser.Object,
            mockValidator.Object,
            mockEmbedding.Object,
            mockRepo.Object,
            Mock.Of<IChunkingService>(),
            Mock.Of<ILogger<FileCreatedHandler>>());

        // Act
        var result = await handler.HandleAsync(new DebouncedFileEvent(
            "/full/path/test.md",
            "test.md",
            WatcherChangeTypes.Created));

        // Assert
        Assert.True(result);
        mockRepo.Verify(r => r.InsertAsync(It.IsAny<DocumentRecord>(), default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_SchemaValidationFails_ReturnsFalse()
    {
        // Arrange
        var mockParser = new Mock<IMarkdownParser>();
        mockParser.Setup(p => p.Parse(It.IsAny<string>()))
            .Returns(new ParseResult { IsValid = true, Document = new MockDocument() });

        var mockValidator = new Mock<ISchemaValidator>();
        mockValidator.Setup(v => v.Validate(It.IsAny<IDocument>()))
            .Returns(new ValidationResult { IsValid = false, Errors = ["Missing title"] });

        var handler = new FileCreatedHandler(
            mockParser.Object,
            mockValidator.Object,
            Mock.Of<IEmbeddingService>(),
            Mock.Of<IDocumentRepository>(),
            Mock.Of<IChunkingService>(),
            Mock.Of<ILogger<FileCreatedHandler>>());

        // Act
        var result = await handler.HandleAsync(new DebouncedFileEvent(
            "/full/path/test.md",
            "test.md",
            WatcherChangeTypes.Created));

        // Assert
        Assert.False(result);
    }
}

// tests/CompoundDocs.Tests/FileWatcher/Handlers/FileDeletedHandlerTests.cs
public class FileDeletedHandlerTests
{
    [Fact]
    public async Task HandleAsync_ExistingDocument_RemovesFromDatabase()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var mockRepo = new Mock<IDocumentRepository>();
        mockRepo.Setup(r => r.GetByPathAsync("test.md", default))
            .ReturnsAsync(new DocumentRecord { Id = documentId });

        var handler = new FileDeletedHandler(
            mockRepo.Object,
            Mock.Of<ILogger<FileDeletedHandler>>());

        // Act
        var result = await handler.HandleAsync(new DebouncedFileEvent(
            "/full/path/test.md",
            "test.md",
            WatcherChangeTypes.Deleted));

        // Assert
        Assert.True(result);
        mockRepo.Verify(r => r.DeleteChunksByDocumentIdAsync(documentId, default), Times.Once);
        mockRepo.Verify(r => r.DeleteAsync(documentId, default), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_NonExistentDocument_ReturnsTrue()
    {
        // Arrange
        var mockRepo = new Mock<IDocumentRepository>();
        mockRepo.Setup(r => r.GetByPathAsync("test.md", default))
            .ReturnsAsync((DocumentRecord?)null);

        var handler = new FileDeletedHandler(
            mockRepo.Object,
            Mock.Of<ILogger<FileDeletedHandler>>());

        // Act
        var result = await handler.HandleAsync(new DebouncedFileEvent(
            "/full/path/test.md",
            "test.md",
            WatcherChangeTypes.Deleted));

        // Assert
        Assert.True(result); // Not an error
        mockRepo.Verify(r => r.DeleteAsync(It.IsAny<Guid>(), default), Times.Never);
    }
}

// tests/CompoundDocs.Tests/FileWatcher/Handlers/FileRenamedHandlerTests.cs
public class FileRenamedHandlerTests
{
    [Fact]
    public async Task HandleAsync_ContentUnchanged_UpdatesPathOnly()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var contentHash = "abc123";

        var mockRepo = new Mock<IDocumentRepository>();
        mockRepo.Setup(r => r.GetByPathAsync("old.md", default))
            .ReturnsAsync(new DocumentRecord { Id = documentId, ContentHash = contentHash });

        var mockEmbedding = new Mock<IEmbeddingService>();

        var handler = new FileRenamedHandler(
            Mock.Of<IMarkdownParser>(),
            Mock.Of<ISchemaValidator>(),
            mockEmbedding.Object,
            mockRepo.Object,
            Mock.Of<ILogger<FileRenamedHandler>>());

        // Act - file content produces same hash
        var result = await handler.HandleAsync(new DebouncedFileEvent(
            "/full/path/new.md",
            "new.md",
            WatcherChangeTypes.Renamed,
            "old.md"));

        // Assert
        mockRepo.Verify(r => r.UpdatePathAsync(documentId, "new.md", default), Times.Once);
        mockEmbedding.Verify(e => e.GenerateEmbeddingAsync(It.IsAny<string>(), default), Times.Never);
    }
}

// tests/CompoundDocs.Tests/FileWatcher/FileEventDispatcherTests.cs
public class FileEventDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_HandlerThrows_IsolatesError()
    {
        // Arrange
        var mockCreatedHandler = new Mock<FileCreatedHandler>();
        mockCreatedHandler.Setup(h => h.HandleAsync(It.IsAny<DebouncedFileEvent>(), default))
            .ThrowsAsync(new InvalidOperationException("Test error"));

        var dispatcher = new FileEventDispatcher(
            mockCreatedHandler.Object,
            Mock.Of<FileChangedHandler>(),
            Mock.Of<FileDeletedHandler>(),
            Mock.Of<FileRenamedHandler>(),
            Mock.Of<ILogger<FileEventDispatcher>>());

        // Act
        var result = await dispatcher.DispatchAsync(new DebouncedFileEvent(
            "/path/test.md",
            "test.md",
            WatcherChangeTypes.Created));

        // Assert
        Assert.False(result); // Error is isolated, not thrown
    }
}
```

### Integration Tests

```csharp
// tests/CompoundDocs.IntegrationTests/FileWatcher/FileEventHandlerIntegrationTests.cs
[Trait("Category", "Integration")]
public class FileEventHandlerIntegrationTests : IClassFixture<DatabaseFixture>
{
    [Fact]
    public async Task FullEventCycle_CreateModifyRenameDelete_ProcessesCorrectly()
    {
        // Test the full lifecycle of a document through all event types
        // Create -> Modify -> Rename -> Delete
    }
}
```

---

## Files Created/Modified

| File | Action | Description |
|------|--------|-------------|
| `src/CompoundDocs.McpServer/FileWatcher/IFileEventHandler.cs` | Create | Handler interface |
| `src/CompoundDocs.McpServer/FileWatcher/DebouncedFileEvent.cs` | Create | Event record |
| `src/CompoundDocs.McpServer/FileWatcher/Handlers/FileCreatedHandler.cs` | Create | OnCreated handler |
| `src/CompoundDocs.McpServer/FileWatcher/Handlers/FileChangedHandler.cs` | Create | OnChanged handler |
| `src/CompoundDocs.McpServer/FileWatcher/Handlers/FileDeletedHandler.cs` | Create | OnDeleted handler |
| `src/CompoundDocs.McpServer/FileWatcher/Handlers/FileRenamedHandler.cs` | Create | OnRenamed handler |
| `src/CompoundDocs.McpServer/FileWatcher/FileEventDispatcher.cs` | Create | Event dispatcher |
| `src/CompoundDocs.McpServer/Extensions/ServiceCollectionExtensions.cs` | Modify | Register handlers |
| `tests/CompoundDocs.Tests/FileWatcher/Handlers/*.cs` | Create | Unit tests |
| `tests/CompoundDocs.IntegrationTests/FileWatcher/*.cs` | Create | Integration tests |

---

## Error Handling Reference

### File System Errors

| Error | Handler Response | Log Level |
|-------|------------------|-----------|
| File not found (after event) | Return false, skip | Warning |
| Permission denied | Return false, skip | Error |
| Path too long | Return false, skip | Error |
| Invalid UTF-8 | Return false, skip | Warning |

### Processing Errors

| Error | Handler Response | Log Level |
|-------|------------------|-----------|
| Markdown parse failure | Return false, skip | Warning |
| Schema validation failure | Return false, skip | Warning |
| Embedding generation failure | Throw (for retry upstream) | Error |
| Database write failure | Throw (for retry upstream) | Error |

---

## Logging Reference

| Level | Events |
|-------|--------|
| Debug | All file events received, content hash comparisons, skip decisions |
| Info | Successful index/update/delete operations with document ID |
| Warning | Schema validation failures, file not found, recoverable errors |
| Error | Permission denied, path too long, handler failures |

---

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| Handler throws unhandled exception | Try-catch isolation in dispatcher |
| File deleted between event and read | Graceful handling with warning log |
| Concurrent events for same file | Debouncing in Phase 054 prevents this |
| Large document embedding timeout | 5-minute timeout from Phase 029 |
| Database transaction failure | Throw for upstream retry handling |
