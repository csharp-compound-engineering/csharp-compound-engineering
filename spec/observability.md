# Observability Specification

> **Status**: [DRAFT]
> **Parent**: [SPEC.md](../SPEC.md)

---

## Scope

This specification covers **logging only** for the MVP. Metrics, health endpoints, and alerting are explicitly out of scope for MVP and will be addressed in future iterations.

### In Scope (MVP)
- Structured logging using `ILogger<T>`
- Log levels and filtering
- Sensitive data handling

### Out of Scope (Post-MVP)
- Prometheus/OpenTelemetry metrics
- Health check endpoints (`/health`, `/ready`)
- Distributed tracing
- Alerting and dashboards

---

## Logging Architecture

### Critical Constraint: stdio Transport

**When using stdio transport for MCP, ALL logging MUST go to stderr because stdout is reserved for MCP protocol messages.**

This is configured at host startup:

```csharp
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});
```

> **Background**: Complete ILogger configuration patterns for MCP servers, including LogToStandardErrorThreshold options and configuration via appsettings.json. See [.NET Generic Host MCP Research](../research/dotnet-generic-host-mcp-research.md).

---

## Logging Configuration

### Log Providers

```csharp
builder.Logging.ClearProviders();

// Console output to stderr (required for MCP stdio transport)
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

// Debug output (development only)
builder.Logging.AddDebug();
```

### Log Levels

| Level | Usage |
|-------|-------|
| `Trace` | Detailed diagnostic information (disabled in production) |
| `Debug` | Development-time debugging information |
| `Information` | General operational events (e.g., "Document indexed successfully") |
| `Warning` | Unexpected but recoverable situations (e.g., "Retry attempt 2 of 3") |
| `Error` | Failures that affect the current operation (e.g., "Failed to generate embedding") |
| `Critical` | Application-wide failures (e.g., "Database connection lost") |

### Level Filtering

```csharp
builder.Logging.SetMinimumLevel(LogLevel.Information);

// Reduce noise from framework components
builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
builder.Logging.AddFilter("System", LogLevel.Warning);

// Enable debug for our components when needed
builder.Logging.AddFilter("CSharpCompoundDocs", LogLevel.Debug);
```

### Configuration via appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "System": "Warning",
      "CSharpCompoundDocs": "Debug"
    }
  }
}
```

---

## Structured Logging

### Pattern: Use Semantic Logging

Use message templates with named parameters for structured logging:

```csharp
// GOOD: Structured logging with semantic parameters
_logger.LogInformation(
    "Document indexed: {DocumentPath} in {ElapsedMs}ms",
    relativePath,
    stopwatch.ElapsedMilliseconds);

// BAD: String interpolation (loses structure)
_logger.LogInformation($"Document indexed: {relativePath} in {stopwatch.ElapsedMilliseconds}ms");
```

### Standard Log Fields

Include consistent fields across log entries:

| Field | Description | Example |
|-------|-------------|---------|
| `ProjectName` | Current tenant project | `"my-project"` |
| `BranchName` | Current git branch | `"feature/auth"` |
| `DocumentPath` | Relative document path | `"problems/db-issue.md"` |
| `ToolName` | MCP tool being executed | `"rag_query"` |
| `ElapsedMs` | Operation duration | `1234` |
| `CorrelationId` | Request correlation ID | `"a1b2c3d4-..."` |

### Correlation ID Pattern

Each MCP tool invocation generates a unique correlation ID to trace related log entries:

```csharp
public async Task<RagQueryResult> HandleRagQueryAsync(RagQueryRequest request, CancellationToken ct)
{
    var correlationId = Guid.NewGuid().ToString("N")[..8]; // Short ID for readability

    using (_logger.BeginScope(new Dictionary<string, object>
    {
        ["CorrelationId"] = correlationId,
        ["ToolName"] = "rag_query"
    }))
    {
        _logger.LogInformation("RAG query started: {Query}", request.Query);
        // ... operations all share the correlation ID ...
        _logger.LogInformation("RAG query completed with {ResultCount} results", results.Count);
    }
}
```

This allows filtering logs by `CorrelationId` to trace a complete request flow.

### Logging Scopes

Use scopes for contextual information that applies to multiple log entries:

```csharp
using (_logger.BeginScope(new Dictionary<string, object>
{
    ["ProjectName"] = projectName,
    ["BranchName"] = branchName
}))
{
    _logger.LogInformation("Activating project");
    // ... operations ...
    _logger.LogInformation("Project activated successfully");
}
```

---

## Sensitive Data Handling

### NEVER Log

- Document content (only paths)
- User credentials or tokens
- Full SQL queries with data
- Embedding vectors

### Safe to Log

- File paths (relative to repo)
- Document metadata (title, type, char count)
- Operation timing
- Error messages (without sensitive context)
- Document IDs and hashes

### Example: Error Logging

```csharp
// GOOD: Log path and error, not content
_logger.LogError(ex,
    "Failed to index document: {DocumentPath}",
    relativePath);

// BAD: Logging document content
_logger.LogError(ex,
    "Failed to index document with content: {Content}",
    documentContent);  // NEVER DO THIS
```

---

## Diagnostic Scenarios

### "Why is RAG slow?"

Check logs for:
- `Generating embedding for query` → `Embedding generated in {ElapsedMs}ms`
- `Searching vector store` → `Search completed in {ElapsedMs}ms`
- `Loading linked documents` → linked doc count and timing

### "Why is a document missing from search?"

Check logs for:
- `File watcher detected: {EventType} {Path}` - was file change detected?
- `Indexing document: {Path}` - was indexing attempted?
- `Document indexed successfully` vs error logs
- Verify tenant context (project/branch) matches query

### "Why did indexing fail?"

Check logs for:
- `EMBEDDING_SERVICE_ERROR` - Ollama connectivity
- `DATABASE_ERROR` - PostgreSQL connectivity
- `SCHEMA_VALIDATION_FAILED` - Invalid frontmatter
- Stack traces in Error/Critical logs

---

## Service-Specific Logging

### File Watcher Service

> **Background**: Comprehensive patterns for integrating logging with IHostedService and BackgroundService, including graceful shutdown logging and error handling. See [Hosted Services and Background Tasks Research](../research/hosted-services-background-tasks.md).

```csharp
_logger.LogDebug("File watcher started for: {WatchPath}", watchPath);
_logger.LogInformation("File change detected: {EventType} {Path}", eventType, relativePath);
_logger.LogWarning("Debounce: ignoring rapid change for {Path}", relativePath);
```

### Embedding Service

> **Background**: Error handling patterns, retry logic with exponential backoff, and timeout configuration for Ollama embedding operations. See [Semantic Kernel + Ollama RAG Research](../research/semantic-kernel-ollama-rag-research.md).

```csharp
_logger.LogDebug("Generating embedding for content ({CharCount} chars)", content.Length);
_logger.LogInformation("Embedding generated in {ElapsedMs}ms", elapsed);
_logger.LogWarning("Embedding retry {Attempt} of {MaxAttempts}", attempt, maxAttempts);
_logger.LogError(ex, "Embedding generation failed after {MaxAttempts} attempts", maxAttempts);
```

### Document Repository

```csharp
_logger.LogDebug("Upserting document: {Id}", document.Id);
_logger.LogInformation("Document upserted: {Path} ({CharCount} chars)", path, charCount);
_logger.LogInformation("Search found {Count} results above threshold {Threshold}", count, threshold);
```

### MCP Tool Execution

> **Background**: MCP tool implementation patterns including dependency injection of ILogger, error handling, and tool return types. See [MCP C# SDK Research](../research/mcp-csharp-sdk-research.md).

```csharp
_logger.LogInformation("Tool invoked: {ToolName}", toolName);
_logger.LogDebug("Tool parameters: {Parameters}", JsonSerializer.Serialize(parameters));
_logger.LogInformation("Tool completed: {ToolName} in {ElapsedMs}ms", toolName, elapsed);
_logger.LogError(ex, "Tool failed: {ToolName} with error {ErrorCode}", toolName, errorCode);
```

---

## Future Considerations (Post-MVP)

When expanding observability beyond MVP:

1. **Metrics**: Consider OpenTelemetry with Prometheus exporter
2. **Health Checks**: Add ASP.NET Core health check endpoints via `Microsoft.Extensions.Diagnostics.HealthChecks`
3. **Tracing**: Add distributed tracing for cross-service correlation
4. **Dashboards**: Grafana dashboards for operational visibility

> **Background**: .NET Aspire provides built-in observability during development including a dashboard with structured logs, distributed traces, and metrics aggregation. See [.NET Aspire Development Orchestrator Research](../research/aspire-development-orchestrator.md).

---

## References

### Research Documents
- [.NET Generic Host MCP Research](../research/dotnet-generic-host-mcp-research.md) - ILogger configuration for MCP, stdio transport logging
- [Hosted Services and Background Tasks Research](../research/hosted-services-background-tasks.md) - BackgroundService logging patterns
- [Semantic Kernel + Ollama RAG Research](../research/semantic-kernel-ollama-rag-research.md) - Embedding service error handling
- [MCP C# SDK Research](../research/mcp-csharp-sdk-research.md) - Tool execution logging patterns
- [.NET Aspire Development Orchestrator Research](../research/aspire-development-orchestrator.md) - Post-MVP observability dashboard

### External Documentation
- [Microsoft.Extensions.Logging Documentation](https://learn.microsoft.com/en-us/dotnet/core/extensions/logging)
