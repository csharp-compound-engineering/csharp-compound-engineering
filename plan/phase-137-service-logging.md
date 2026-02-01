# Phase 137: Service-Specific Logging

> **Status**: NOT_STARTED
> **Effort Estimate**: 3-4 hours
> **Category**: Observability
> **Prerequisites**: Phase 018 (Logging Infrastructure)

---

## Spec References

This phase implements the service-specific logging patterns defined in:

- **spec/observability.md** - Service-Specific Logging section
- **research/hosted-services-background-tasks.md** - BackgroundService logging patterns
- **research/semantic-kernel-ollama-rag-research.md** - Embedding service error handling
- **research/mcp-csharp-sdk-research.md** - Tool execution logging patterns

---

## Objectives

1. Implement comprehensive logging patterns for the File Watcher Service
2. Implement logging patterns for the Embedding Service with retry visibility
3. Implement logging patterns for the Document Repository operations
4. Implement logging patterns for MCP Tool Execution with correlation tracking
5. Define and document recommended log levels per service and operation type
6. Create logging extension methods for consistent service-specific patterns

---

## Acceptance Criteria

### File Watcher Service Logging

- [ ] Startup logging includes watch path and configuration details
- [ ] File change detection logs include event type and relative path at `Information` level
- [ ] Debounce events are logged at `Warning` level with affected path
- [ ] Watcher errors and recovery attempts are logged at appropriate levels
- [ ] Graceful shutdown logging is implemented
- [ ] Buffer overflow conditions are logged at `Warning` level

Log Level Recommendations:
| Operation | Log Level |
|-----------|-----------|
| Watcher started/stopped | Debug |
| File change detected | Information |
| Debounce triggered | Warning |
| Watcher error/recovery | Error |
| Graceful shutdown | Debug |

### Embedding Service Logging

- [ ] Embedding generation start logs include content character count at `Debug` level
- [ ] Successful embedding generation logs elapsed time at `Information` level
- [ ] Retry attempts are logged at `Warning` level with attempt number and max attempts
- [ ] Final failure after retries is logged at `Error` level with exception details
- [ ] Model name and configuration are logged at startup
- [ ] Ollama connectivity issues are logged with actionable context

Log Level Recommendations:
| Operation | Log Level |
|-----------|-----------|
| Generation started | Debug |
| Generation completed | Information |
| Retry attempt | Warning |
| Generation failed | Error |
| Ollama unavailable | Error |
| Model loading | Debug |

### Document Repository Logging

- [ ] Document upsert operations log document ID at `Debug` level
- [ ] Successful upsert logs path and character count at `Information` level
- [ ] Search operations log result count and threshold at `Information` level
- [ ] Database connection issues are logged at `Error` level
- [ ] Batch operations log start, progress, and completion
- [ ] Tenant context (project/branch) is included via logging scope

Log Level Recommendations:
| Operation | Log Level |
|-----------|-----------|
| Upsert started | Debug |
| Upsert completed | Information |
| Search executed | Information |
| Delete operation | Information |
| Batch progress | Debug |
| Database error | Error |

### MCP Tool Execution Logging

- [ ] Tool invocation logs tool name at `Information` level
- [ ] Tool parameters are logged at `Debug` level (with sensitive data redaction)
- [ ] Tool completion logs tool name and elapsed time at `Information` level
- [ ] Tool failures log tool name, error code, and exception at `Error` level
- [ ] Correlation ID scope is established at tool invocation
- [ ] Input validation failures are logged at `Warning` level

Log Level Recommendations:
| Operation | Log Level |
|-----------|-----------|
| Tool invoked | Information |
| Tool parameters | Debug |
| Tool completed | Information |
| Tool failed | Error |
| Validation failed | Warning |
| Slow tool execution | Warning |

### Logging Extension Methods

- [ ] `LogToolInvocation` extension method for consistent tool logging
- [ ] `LogToolCompletion` extension method with elapsed time calculation
- [ ] `LogToolFailure` extension method with error code mapping
- [ ] `LogFileWatcherEvent` extension method for file system events
- [ ] `LogEmbeddingOperation` extension method for embedding service
- [ ] `LogRepositoryOperation` extension method for data operations

---

## Implementation Notes

### File Watcher Service Logging Patterns

Implement comprehensive logging for the file watcher lifecycle:

```csharp
public class FileWatcherService : BackgroundService
{
    private readonly ILogger<FileWatcherService> _logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogDebug("File watcher starting for: {WatchPath}", _watchPath);

        try
        {
            await StartWatchingAsync(stoppingToken);
            _logger.LogDebug("File watcher initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "File watcher failed to start for: {WatchPath}", _watchPath);
            throw;
        }
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        var relativePath = GetRelativePath(e.FullPath);

        _logger.LogInformation(
            "File change detected: {EventType} {Path}",
            e.ChangeType,
            relativePath);
    }

    private void OnDebounce(string path)
    {
        _logger.LogWarning(
            "Debounce: ignoring rapid change for {Path}",
            path);
    }

    private void OnError(object sender, ErrorEventArgs e)
    {
        _logger.LogError(
            e.GetException(),
            "File watcher error occurred, attempting recovery");
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("File watcher stopping gracefully");
        await base.StopAsync(cancellationToken);
        _logger.LogDebug("File watcher stopped");
    }
}
```

### Embedding Service Logging Patterns

Implement logging with retry visibility and timing:

```csharp
public class EmbeddingService : IEmbeddingService
{
    private readonly ILogger<EmbeddingService> _logger;

    public async Task<float[]> GenerateEmbeddingAsync(string content, CancellationToken ct)
    {
        _logger.LogDebug(
            "Generating embedding for content ({CharCount} chars)",
            content.Length);

        var stopwatch = Stopwatch.StartNew();
        int attempt = 0;
        const int maxAttempts = 3;

        while (attempt < maxAttempts)
        {
            attempt++;
            try
            {
                var embedding = await _embeddingGenerator.GenerateAsync(content, ct);
                stopwatch.Stop();

                _logger.LogInformation(
                    "Embedding generated in {ElapsedMs}ms",
                    stopwatch.ElapsedMilliseconds);

                return embedding;
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                _logger.LogWarning(
                    "Embedding retry {Attempt} of {MaxAttempts}: {ErrorMessage}",
                    attempt,
                    maxAttempts,
                    ex.Message);

                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
            }
        }

        _logger.LogError(
            "Embedding generation failed after {MaxAttempts} attempts",
            maxAttempts);

        throw new EmbeddingGenerationException("Failed to generate embedding");
    }
}
```

### Document Repository Logging Patterns

Implement logging with tenant context:

```csharp
public class DocumentRepository : IDocumentRepository
{
    private readonly ILogger<DocumentRepository> _logger;

    public async Task UpsertAsync(CompoundDocument document, CancellationToken ct)
    {
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            [LogFields.ProjectName] = document.TenantId,
            [LogFields.DocumentPath] = document.RelativePath
        }))
        {
            _logger.LogDebug("Upserting document: {Id}", document.Id);

            try
            {
                await _dbContext.UpsertAsync(document, ct);

                _logger.LogInformation(
                    "Document upserted: {Path} ({CharCount} chars)",
                    document.RelativePath,
                    document.Content?.Length ?? 0);
            }
            catch (DbException ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to upsert document: {Path}",
                    document.RelativePath);
                throw;
            }
        }
    }

    public async Task<IReadOnlyList<SearchResult>> SearchAsync(
        string query,
        float threshold,
        CancellationToken ct)
    {
        var results = await _vectorStore.SearchAsync(query, threshold, ct);

        _logger.LogInformation(
            "Search found {Count} results above threshold {Threshold}",
            results.Count,
            threshold);

        return results;
    }
}
```

### MCP Tool Execution Logging Patterns

Implement correlation tracking and consistent tool logging:

```csharp
public class RagQueryToolHandler
{
    private readonly ILogger<RagQueryToolHandler> _logger;

    public async Task<RagQueryResult> HandleAsync(RagQueryRequest request, CancellationToken ct)
    {
        var correlationId = CorrelationIdGenerator.Generate();
        var stopwatch = Stopwatch.StartNew();

        using (_logger.BeginScope(new Dictionary<string, object>
        {
            [LogFields.CorrelationId] = correlationId,
            [LogFields.ToolName] = "rag_query"
        }))
        {
            _logger.LogInformation("Tool invoked: {ToolName}", "rag_query");

            _logger.LogDebug(
                "Tool parameters: Query={Query}, MaxResults={MaxResults}",
                request.Query,
                request.MaxResults);

            try
            {
                var result = await ExecuteQueryAsync(request, ct);
                stopwatch.Stop();

                _logger.LogInformation(
                    "Tool completed: {ToolName} in {ElapsedMs}ms with {ResultCount} results",
                    "rag_query",
                    stopwatch.ElapsedMilliseconds,
                    result.Results.Count);

                // Warn on slow execution
                if (stopwatch.ElapsedMilliseconds > 5000)
                {
                    _logger.LogWarning(
                        "Slow tool execution: {ToolName} took {ElapsedMs}ms",
                        "rag_query",
                        stopwatch.ElapsedMilliseconds);
                }

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                var errorCode = ErrorCodeMapper.MapException(ex);

                _logger.LogError(
                    ex,
                    "Tool failed: {ToolName} with error {ErrorCode}",
                    "rag_query",
                    errorCode);

                throw;
            }
        }
    }
}
```

### Logging Extension Methods

Create extension methods for consistent patterns:

```csharp
public static class ServiceLoggingExtensions
{
    public static IDisposable BeginToolScope(
        this ILogger logger,
        string toolName,
        string? correlationId = null)
    {
        return logger.BeginScope(new Dictionary<string, object>
        {
            [LogFields.ToolName] = toolName,
            [LogFields.CorrelationId] = correlationId ?? CorrelationIdGenerator.Generate()
        });
    }

    public static void LogToolInvocation(
        this ILogger logger,
        string toolName)
    {
        logger.LogInformation("Tool invoked: {ToolName}", toolName);
    }

    public static void LogToolCompletion(
        this ILogger logger,
        string toolName,
        long elapsedMs,
        int? resultCount = null)
    {
        if (resultCount.HasValue)
        {
            logger.LogInformation(
                "Tool completed: {ToolName} in {ElapsedMs}ms with {ResultCount} results",
                toolName,
                elapsedMs,
                resultCount.Value);
        }
        else
        {
            logger.LogInformation(
                "Tool completed: {ToolName} in {ElapsedMs}ms",
                toolName,
                elapsedMs);
        }
    }

    public static void LogToolFailure(
        this ILogger logger,
        string toolName,
        string errorCode,
        Exception ex)
    {
        logger.LogError(
            ex,
            "Tool failed: {ToolName} with error {ErrorCode}",
            toolName,
            errorCode);
    }

    public static void LogFileWatcherEvent(
        this ILogger logger,
        WatcherChangeTypes changeType,
        string relativePath)
    {
        logger.LogInformation(
            "File change detected: {EventType} {Path}",
            changeType,
            relativePath);
    }

    public static void LogEmbeddingRetry(
        this ILogger logger,
        int attempt,
        int maxAttempts,
        string errorMessage)
    {
        logger.LogWarning(
            "Embedding retry {Attempt} of {MaxAttempts}: {ErrorMessage}",
            attempt,
            maxAttempts,
            errorMessage);
    }

    public static void LogRepositoryOperation(
        this ILogger logger,
        string operation,
        string documentPath,
        int? charCount = null)
    {
        if (charCount.HasValue)
        {
            logger.LogInformation(
                "Repository {Operation}: {Path} ({CharCount} chars)",
                operation,
                documentPath,
                charCount.Value);
        }
        else
        {
            logger.LogInformation(
                "Repository {Operation}: {Path}",
                operation,
                documentPath);
        }
    }
}
```

### Slow Operation Thresholds

Define thresholds for warning on slow operations:

```csharp
public static class PerformanceThresholds
{
    public const int ToolExecutionWarningMs = 5000;
    public const int EmbeddingGenerationWarningMs = 3000;
    public const int DatabaseQueryWarningMs = 1000;
    public const int FileWatcherProcessingWarningMs = 500;
}
```

---

## Dependencies

### Depends On

- Phase 018: Logging Infrastructure (prerequisite - logging configuration and patterns)
- Phase 053: File Watcher Service (service implementation to add logging)
- Phase 029: Embedding Service (service implementation to add logging)
- Phase 048: Document Repository (service implementation to add logging)
- Phase 026: Tool Execution (tool handlers to add logging)

### Blocks

- All diagnostic and troubleshooting capabilities
- Production debugging and monitoring
- Performance analysis and optimization

---

## Verification Steps

After completing this phase, verify:

1. **File Watcher Logging**: Start/stop the file watcher and verify debug logs appear on stderr
2. **Change Detection**: Modify a file and verify information-level log with event type and path
3. **Debounce Logging**: Rapidly modify a file and verify warning-level debounce log
4. **Embedding Logging**: Generate an embedding and verify timing log at information level
5. **Retry Visibility**: Simulate Ollama unavailability and verify retry warnings
6. **Repository Logging**: Upsert a document and verify operation log with char count
7. **Tool Logging**: Execute a tool and verify invocation/completion logs with correlation ID
8. **Slow Operation Warnings**: Artificially delay an operation and verify warning threshold triggers

### Test Commands

```bash
# Run with debug logging to see all service logs
dotnet run -- --verbosity Debug 2>&1 | grep -E "(File watcher|Embedding|Document|Tool)"

# Test file watcher logging
touch test-document.md && rm test-document.md

# Verify correlation ID consistency
dotnet run 2>&1 | grep "CorrelationId" | head -20

# Check for slow operation warnings
dotnet run 2>&1 | grep -i "slow"
```

---

## Diagnostic Scenarios

This phase enables the following diagnostic scenarios from the spec:

### "Why is RAG slow?"

Check logs for timing at each stage:
- `Generating embedding for query` -> `Embedding generated in {ElapsedMs}ms`
- `Searching vector store` -> `Search completed in {ElapsedMs}ms`
- `Loading linked documents` -> linked doc count and timing
- `Tool completed: rag_query in {ElapsedMs}ms`

### "Why is a document missing from search?"

Check logs for:
- `File change detected: {EventType} {Path}` - was file change detected?
- `Indexing document: {Path}` - was indexing attempted?
- `Document upserted` vs error logs - did it succeed?
- Verify tenant context (project/branch) matches query

### "Why did indexing fail?"

Check logs for:
- `Embedding retry` warnings - Ollama connectivity issues
- `Failed to upsert document` errors - PostgreSQL issues
- `SCHEMA_VALIDATION_FAILED` - Invalid frontmatter
- Stack traces in Error/Critical logs

---

## Notes

- All service-specific logging must use structured templates (not string interpolation)
- Log levels should be chosen carefully to avoid noise while ensuring diagnostic capability
- Correlation IDs must be propagated across all service boundaries within a request
- Performance thresholds should be configurable via appsettings.json in future iterations
- Consider adding log sampling for high-frequency operations in production
