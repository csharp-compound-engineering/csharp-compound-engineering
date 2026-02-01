# Phase 018: Logging Infrastructure

> **Status**: NOT_STARTED
> **Effort Estimate**: 2-3 hours
> **Category**: Infrastructure Setup
> **Prerequisites**: Phase 017 (Error Handling Infrastructure)

---

## Spec References

This phase implements the logging infrastructure defined in:

- **spec/observability.md** - Complete logging specification
- **structure/observability.md** - Logging summary

---

## Objectives

1. Configure `ILogger<T>` with console provider directing all output to stderr (required for MCP stdio transport)
2. Implement log level filtering to reduce noise from framework components
3. Create `appsettings.json` logging configuration
4. Establish structured logging patterns with semantic message templates
5. Implement correlation ID pattern for request tracing
6. Define standard log fields for consistency across services

---

## Acceptance Criteria

### ILogger Configuration

- [ ] `ILogger<T>` is properly configured in the host builder
- [ ] Console provider is added with `LogToStandardErrorThreshold = LogLevel.Trace`
- [ ] Debug provider is conditionally added for development environment
- [ ] All log output goes to stderr (stdout reserved for MCP protocol messages)

### Log Level Filtering

- [ ] Default minimum level is set to `Information`
- [ ] `Microsoft.*` namespace filtered to `Warning` level
- [ ] `System.*` namespace filtered to `Warning` level
- [ ] `CSharpCompoundDocs.*` namespace configurable (default `Debug` in development)

### Configuration File

- [ ] `appsettings.json` contains `Logging` section with log level configuration
- [ ] `appsettings.Development.json` contains development-specific overrides
- [ ] Log levels can be changed without recompilation

### Structured Logging Patterns

- [ ] Semantic message templates with named parameters are documented and demonstrated
- [ ] Standard log fields are defined (ProjectName, BranchName, DocumentPath, ToolName, ElapsedMs, CorrelationId)
- [ ] Logging scope helpers are implemented for contextual information
- [ ] String interpolation is explicitly avoided in favor of structured templates

### Correlation ID Pattern

- [ ] Correlation ID generation helper method exists
- [ ] Logging scope pattern for correlation IDs is documented
- [ ] Example implementation shows tracing across a request flow

---

## Implementation Notes

### Critical Constraint: stdio Transport

**When using stdio transport for MCP, ALL logging MUST go to stderr because stdout is reserved for MCP protocol messages.**

This is the most critical requirement for this phase. Any log output to stdout will corrupt the MCP JSON-RPC communication stream.

### Host Builder Configuration

Configure logging in the MCP server's host builder:

```csharp
var builder = Host.CreateApplicationBuilder(args);

// Clear default providers to have full control
builder.Logging.ClearProviders();

// Console output to stderr (REQUIRED for MCP stdio transport)
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

// Debug output (development only)
if (builder.Environment.IsDevelopment())
{
    builder.Logging.AddDebug();
}

// Set minimum level and filters
builder.Logging.SetMinimumLevel(LogLevel.Information);
builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
builder.Logging.AddFilter("System", LogLevel.Warning);
builder.Logging.AddFilter("CSharpCompoundDocs", LogLevel.Debug);
```

### appsettings.json Configuration

Create or update `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "System": "Warning",
      "CSharpCompoundDocs": "Debug"
    },
    "Console": {
      "LogToStandardErrorThreshold": "Trace"
    }
  }
}
```

Create `appsettings.Development.json` for development overrides:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "CSharpCompoundDocs": "Trace"
    }
  }
}
```

### Log Level Reference

| Level | Usage |
|-------|-------|
| `Trace` | Detailed diagnostic information (disabled in production) |
| `Debug` | Development-time debugging information |
| `Information` | General operational events (e.g., "Document indexed successfully") |
| `Warning` | Unexpected but recoverable situations (e.g., "Retry attempt 2 of 3") |
| `Error` | Failures that affect the current operation (e.g., "Failed to generate embedding") |
| `Critical` | Application-wide failures (e.g., "Database connection lost") |

### Structured Logging Patterns

**GOOD: Semantic logging with named parameters**

```csharp
_logger.LogInformation(
    "Document indexed: {DocumentPath} in {ElapsedMs}ms",
    relativePath,
    stopwatch.ElapsedMilliseconds);
```

**BAD: String interpolation (loses structure)**

```csharp
// DO NOT DO THIS - loses structured data
_logger.LogInformation($"Document indexed: {relativePath} in {stopwatch.ElapsedMilliseconds}ms");
```

### Standard Log Fields

Define constants for consistent field names:

```csharp
public static class LogFields
{
    public const string ProjectName = "ProjectName";
    public const string BranchName = "BranchName";
    public const string DocumentPath = "DocumentPath";
    public const string ToolName = "ToolName";
    public const string ElapsedMs = "ElapsedMs";
    public const string CorrelationId = "CorrelationId";
}
```

### Correlation ID Pattern

Implement correlation ID generation and scope usage:

```csharp
public static class CorrelationIdGenerator
{
    public static string Generate() => Guid.NewGuid().ToString("N")[..8];
}

// Usage in tool handlers
public async Task<RagQueryResult> HandleRagQueryAsync(RagQueryRequest request, CancellationToken ct)
{
    var correlationId = CorrelationIdGenerator.Generate();

    using (_logger.BeginScope(new Dictionary<string, object>
    {
        [LogFields.CorrelationId] = correlationId,
        [LogFields.ToolName] = "rag_query"
    }))
    {
        _logger.LogInformation("RAG query started: {Query}", request.Query);
        // ... operations all share the correlation ID ...
        _logger.LogInformation("RAG query completed with {ResultCount} results", results.Count);
    }
}
```

### Logging Scopes for Context

Use scopes for contextual information that applies to multiple log entries:

```csharp
using (_logger.BeginScope(new Dictionary<string, object>
{
    [LogFields.ProjectName] = projectName,
    [LogFields.BranchName] = branchName
}))
{
    _logger.LogInformation("Activating project");
    // ... operations ...
    _logger.LogInformation("Project activated successfully");
}
```

### Sensitive Data Guidelines

**NEVER Log:**
- Document content (only paths)
- User credentials or tokens
- Full SQL queries with data
- Embedding vectors

**Safe to Log:**
- File paths (relative to repo)
- Document metadata (title, type, char count)
- Operation timing
- Error messages (without sensitive context)
- Document IDs and hashes

---

## Dependencies

### Depends On

- Phase 017: Error Handling Infrastructure (prerequisite - error codes are logged)

### Blocks

- All service implementations that require logging
- File Watcher Service (uses logging for change detection events)
- Embedding Service (uses logging for retry and timing)
- Document Repository (uses logging for operations)
- MCP Tool Handlers (use correlation IDs and tool logging)

---

## Verification Steps

After completing this phase, verify:

1. **stderr output**: Run the MCP server and confirm all log output appears on stderr, not stdout
2. **Log level filtering**: Verify Microsoft/System logs only appear at Warning+ levels
3. **Structured logging**: Check that log entries contain named parameters (not interpolated strings)
4. **Configuration**: Modify `appsettings.json` log levels and verify changes take effect without recompilation
5. **Correlation IDs**: Execute a tool and verify all related log entries share the same correlation ID

### Test Commands

```bash
# Run server and capture stderr separately
dotnet run 2>logs.txt

# Verify no output on stdout during normal operation
dotnet run 2>/dev/null  # Should produce no output unless MCP messages are sent

# Check log format
grep "CorrelationId" logs.txt  # Should show correlation IDs in structured format
```

---

## Service-Specific Logging Examples

### File Watcher Service

```csharp
_logger.LogDebug("File watcher started for: {WatchPath}", watchPath);
_logger.LogInformation("File change detected: {EventType} {Path}", eventType, relativePath);
_logger.LogWarning("Debounce: ignoring rapid change for {Path}", relativePath);
```

### Embedding Service

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

```csharp
_logger.LogInformation("Tool invoked: {ToolName}", toolName);
_logger.LogDebug("Tool parameters: {Parameters}", JsonSerializer.Serialize(parameters));
_logger.LogInformation("Tool completed: {ToolName} in {ElapsedMs}ms", toolName, elapsed);
_logger.LogError(ex, "Tool failed: {ToolName} with error {ErrorCode}", toolName, errorCode);
```

---

## Notes

- The `LogToStandardErrorThreshold = LogLevel.Trace` setting ensures ALL log levels go to stderr
- This phase establishes patterns that all subsequent service implementations must follow
- Correlation IDs are essential for debugging issues in production where multiple requests interleave
- Consider adding a logging extension methods class to enforce consistent patterns across the codebase
