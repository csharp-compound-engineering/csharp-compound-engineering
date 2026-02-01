# Phase 136: Diagnostic Scenarios

> **Status**: NOT_STARTED
> **Effort Estimate**: 3-4 hours
> **Category**: Observability
> **Prerequisites**: Phase 018 (Logging Infrastructure)

---

## Spec References

This phase implements the diagnostic scenarios defined in:

- **spec/observability.md** - Diagnostic Scenarios section

---

## Objectives

1. Document troubleshooting workflow for slow RAG queries with specific log patterns to investigate
2. Document troubleshooting workflow for missing documents in search results
3. Document troubleshooting workflow for indexing failures
4. Create diagnostic helper utilities for common investigation patterns
5. Implement diagnostic logging enhancements to support each scenario
6. Document log patterns and expected sequences for each diagnostic scenario

---

## Acceptance Criteria

### Slow RAG Query Diagnostics

- [ ] Log timing breakdown is captured at each RAG pipeline stage:
  - Embedding generation timing
  - Vector search timing
  - Linked document loading timing
  - Total query timing
- [ ] Diagnostic documentation explains how to identify bottlenecks from logs
- [ ] Warning threshold logging is implemented for slow operations (configurable)
- [ ] Log patterns document expected sequence: `Generating embedding for query` -> `Embedding generated in {ElapsedMs}ms` -> `Searching vector store` -> `Search completed in {ElapsedMs}ms` -> `Loading linked documents` -> `Linked documents loaded in {ElapsedMs}ms`

### Missing Documents in Search Diagnostics

- [ ] File watcher events are logged with sufficient detail: `File watcher detected: {EventType} {Path}`
- [ ] Indexing attempts are logged: `Indexing document: {Path}`
- [ ] Indexing outcomes are logged: `Document indexed successfully` or error with reason
- [ ] Tenant context (project/branch) is logged with every search operation
- [ ] Diagnostic documentation explains how to trace document lifecycle from file change to search availability
- [ ] Reconciliation operations log document discovery and comparison results

### Indexing Failure Diagnostics

- [ ] Embedding service errors are logged with error code `EMBEDDING_SERVICE_ERROR` and include Ollama connectivity details
- [ ] Database errors are logged with error code `DATABASE_ERROR` and include PostgreSQL connectivity details
- [ ] Schema validation failures are logged with error code `SCHEMA_VALIDATION_FAILED` and include specific validation errors
- [ ] Stack traces are captured at Error/Critical levels for debugging
- [ ] Retry attempts are logged with attempt number and max attempts
- [ ] Diagnostic documentation maps error codes to root causes and resolutions

### Diagnostic Workflow Documentation

- [ ] README or diagnostic guide is created documenting each scenario
- [ ] Each scenario includes: symptoms, log patterns to search, investigation steps, common causes, resolutions
- [ ] Example log outputs are provided for each scenario
- [ ] Log grep/filter patterns are documented for each diagnostic scenario

### Diagnostic Helper Utilities

- [ ] `DiagnosticLogger` helper class provides scenario-specific logging methods
- [ ] Timing helper captures operation duration with automatic warning on slow operations
- [ ] Correlation ID is propagated through all diagnostic-relevant log entries
- [ ] Log scope helpers ensure tenant context is always present in diagnostic logs

---

## Implementation Notes

### Diagnostic Logging Patterns

#### RAG Query Timing Breakdown

```csharp
public class RagDiagnosticLogger
{
    private readonly ILogger<RagDiagnosticLogger> _logger;
    private readonly TimeSpan _slowQueryThreshold;

    public void LogEmbeddingPhase(string query, TimeSpan elapsed)
    {
        _logger.LogInformation(
            "Embedding generated for query in {ElapsedMs}ms (query length: {QueryLength})",
            elapsed.TotalMilliseconds,
            query.Length);

        if (elapsed > _slowQueryThreshold)
        {
            _logger.LogWarning(
                "Slow embedding generation: {ElapsedMs}ms exceeds threshold {ThresholdMs}ms",
                elapsed.TotalMilliseconds,
                _slowQueryThreshold.TotalMilliseconds);
        }
    }

    public void LogVectorSearchPhase(int resultCount, TimeSpan elapsed)
    {
        _logger.LogInformation(
            "Vector search completed in {ElapsedMs}ms with {ResultCount} results",
            elapsed.TotalMilliseconds,
            resultCount);
    }

    public void LogLinkedDocumentPhase(int linkedDocCount, TimeSpan elapsed)
    {
        _logger.LogInformation(
            "Loaded {LinkedDocCount} linked documents in {ElapsedMs}ms",
            linkedDocCount,
            elapsed.TotalMilliseconds);
    }

    public void LogTotalQueryTime(string query, int resultCount, TimeSpan totalElapsed)
    {
        _logger.LogInformation(
            "RAG query completed: {ResultCount} results in {TotalElapsedMs}ms",
            resultCount,
            totalElapsed.TotalMilliseconds);

        if (totalElapsed > _slowQueryThreshold * 3) // Total threshold is 3x single operation
        {
            _logger.LogWarning(
                "Slow RAG query detected: {TotalElapsedMs}ms - check individual phase timings",
                totalElapsed.TotalMilliseconds);
        }
    }
}
```

#### Missing Document Diagnostic Logging

```csharp
public class DocumentDiagnosticLogger
{
    private readonly ILogger<DocumentDiagnosticLogger> _logger;

    public void LogFileWatcherEvent(WatcherChangeTypes eventType, string relativePath)
    {
        _logger.LogInformation(
            "File watcher detected: {EventType} {Path}",
            eventType,
            relativePath);
    }

    public void LogIndexingAttempt(string relativePath, string projectName, string branchName)
    {
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["ProjectName"] = projectName,
            ["BranchName"] = branchName,
            ["DocumentPath"] = relativePath
        }))
        {
            _logger.LogInformation("Indexing document: {Path}", relativePath);
        }
    }

    public void LogIndexingSuccess(string relativePath, string documentId, int chunkCount)
    {
        _logger.LogInformation(
            "Document indexed successfully: {Path} (ID: {DocumentId}, Chunks: {ChunkCount})",
            relativePath,
            documentId,
            chunkCount);
    }

    public void LogSearchContext(string projectName, string branchName, string query)
    {
        _logger.LogDebug(
            "Search context: Project={ProjectName}, Branch={BranchName}, Query={Query}",
            projectName,
            branchName,
            query);
    }

    public void LogReconciliationStart(string watchPath, int existingDocCount)
    {
        _logger.LogInformation(
            "Starting reconciliation for {WatchPath}: {ExistingDocCount} documents in database",
            watchPath,
            existingDocCount);
    }

    public void LogReconciliationResult(int added, int updated, int removed)
    {
        _logger.LogInformation(
            "Reconciliation complete: Added={Added}, Updated={Updated}, Removed={Removed}",
            added,
            updated,
            removed);
    }
}
```

#### Indexing Failure Diagnostic Logging

```csharp
public class IndexingFailureDiagnosticLogger
{
    private readonly ILogger<IndexingFailureDiagnosticLogger> _logger;

    public void LogEmbeddingServiceError(Exception ex, string documentPath, int attempt, int maxAttempts)
    {
        _logger.LogError(
            ex,
            "EMBEDDING_SERVICE_ERROR: Failed to generate embedding for {DocumentPath} " +
            "(Attempt {Attempt}/{MaxAttempts}). Check Ollama connectivity.",
            documentPath,
            attempt,
            maxAttempts);
    }

    public void LogDatabaseError(Exception ex, string operation, string documentPath)
    {
        _logger.LogError(
            ex,
            "DATABASE_ERROR: {Operation} failed for {DocumentPath}. Check PostgreSQL connectivity.",
            operation,
            documentPath);
    }

    public void LogSchemaValidationError(string documentPath, IEnumerable<string> validationErrors)
    {
        _logger.LogWarning(
            "SCHEMA_VALIDATION_FAILED: {DocumentPath} has invalid frontmatter: {Errors}",
            documentPath,
            string.Join("; ", validationErrors));
    }

    public void LogRetryAttempt(string operation, int attempt, int maxAttempts, TimeSpan delay)
    {
        _logger.LogWarning(
            "Retry {Attempt}/{MaxAttempts} for {Operation} after {DelayMs}ms delay",
            attempt,
            maxAttempts,
            operation,
            delay.TotalMilliseconds);
    }
}
```

### Diagnostic Workflow Documentation Structure

Create a `DIAGNOSTICS.md` file documenting each scenario:

#### Scenario: Slow RAG Queries

**Symptoms:**
- RAG query tool takes longer than expected to return results
- Users report slow response times when querying documents

**Log Patterns to Search:**
```bash
# Find slow embedding generation
grep "Slow embedding generation" logs.txt

# Find RAG query timing breakdown
grep -E "(Embedding generated|Vector search completed|Loaded.*linked documents|RAG query completed)" logs.txt | grep <correlation-id>

# Find slow total query time
grep "Slow RAG query detected" logs.txt
```

**Investigation Steps:**
1. Get the correlation ID from the slow query
2. Filter logs by correlation ID to see complete request flow
3. Compare timing at each phase: embedding, search, linked docs
4. Identify which phase is the bottleneck

**Common Causes:**
- Ollama server under load (slow embedding generation)
- Large number of linked documents (slow linked doc loading)
- Missing HNSW index (slow vector search)
- Network latency to database or Ollama

**Resolutions:**
- Scale Ollama resources if embedding is slow
- Optimize link following depth if linked docs are slow
- Verify HNSW index exists if search is slow
- Check network connectivity

#### Scenario: Missing Documents in Search

**Symptoms:**
- Document exists on disk but doesn't appear in search results
- Recently modified document shows stale content

**Log Patterns to Search:**
```bash
# Check if file change was detected
grep "File watcher detected" logs.txt | grep "<filename>"

# Check if indexing was attempted
grep "Indexing document" logs.txt | grep "<filename>"

# Check if indexing succeeded
grep "Document indexed successfully" logs.txt | grep "<filename>"

# Check search context
grep "Search context" logs.txt | grep "<correlation-id>"
```

**Investigation Steps:**
1. Verify file watcher detected the file change
2. Verify indexing was attempted for the file
3. Check for indexing errors
4. Verify tenant context matches (project/branch)
5. Check reconciliation logs if file existed before watcher started

**Common Causes:**
- File watcher not started or watching wrong directory
- File filtered by ignore patterns
- Tenant context mismatch (wrong project/branch)
- Indexing failed silently (check error logs)
- Document debounced and waiting to be indexed

**Resolutions:**
- Verify file watcher configuration
- Check ignore patterns in configuration
- Ensure correct project is activated
- Trigger manual reindex if needed
- Wait for debounce period to complete

#### Scenario: Indexing Failures

**Symptoms:**
- Error logs showing EMBEDDING_SERVICE_ERROR, DATABASE_ERROR, or SCHEMA_VALIDATION_FAILED
- Documents not being indexed despite file changes

**Log Patterns to Search:**
```bash
# Find embedding service errors
grep "EMBEDDING_SERVICE_ERROR" logs.txt

# Find database errors
grep "DATABASE_ERROR" logs.txt

# Find schema validation errors
grep "SCHEMA_VALIDATION_FAILED" logs.txt

# Find retry attempts
grep "Retry" logs.txt | grep -E "[0-9]+/[0-9]+"
```

**Investigation Steps:**
1. Identify the error code in logs
2. Check the associated exception and stack trace
3. Verify external service connectivity (Ollama, PostgreSQL)
4. Check document frontmatter for schema errors

**Error Code Reference:**

| Error Code | Root Cause | Resolution |
|------------|------------|------------|
| EMBEDDING_SERVICE_ERROR | Ollama unavailable or timeout | Check Ollama is running, verify network, increase timeout |
| DATABASE_ERROR | PostgreSQL unavailable or query failed | Check PostgreSQL is running, verify connection string, check disk space |
| SCHEMA_VALIDATION_FAILED | Invalid document frontmatter | Fix frontmatter schema, check required fields |

**Resolutions:**
- Restart Ollama service if embedding errors persist
- Check PostgreSQL connection string and credentials
- Validate document frontmatter against schema
- Check application logs for stack traces

### Timing Helper Implementation

```csharp
public class DiagnosticTimer : IDisposable
{
    private readonly ILogger _logger;
    private readonly string _operationName;
    private readonly Stopwatch _stopwatch;
    private readonly TimeSpan _warningThreshold;
    private readonly Dictionary<string, object> _context;

    public DiagnosticTimer(
        ILogger logger,
        string operationName,
        TimeSpan? warningThreshold = null,
        Dictionary<string, object>? context = null)
    {
        _logger = logger;
        _operationName = operationName;
        _warningThreshold = warningThreshold ?? TimeSpan.FromSeconds(5);
        _context = context ?? new Dictionary<string, object>();
        _stopwatch = Stopwatch.StartNew();

        _logger.LogDebug("Starting operation: {OperationName}", operationName);
    }

    public TimeSpan Elapsed => _stopwatch.Elapsed;

    public void Dispose()
    {
        _stopwatch.Stop();
        var elapsed = _stopwatch.Elapsed;

        if (elapsed > _warningThreshold)
        {
            _logger.LogWarning(
                "Slow operation: {OperationName} took {ElapsedMs}ms (threshold: {ThresholdMs}ms)",
                _operationName,
                elapsed.TotalMilliseconds,
                _warningThreshold.TotalMilliseconds);
        }
        else
        {
            _logger.LogInformation(
                "Operation completed: {OperationName} in {ElapsedMs}ms",
                _operationName,
                elapsed.TotalMilliseconds);
        }
    }
}

// Usage
using (new DiagnosticTimer(_logger, "EmbeddingGeneration", TimeSpan.FromSeconds(2)))
{
    await _embeddingService.GenerateEmbeddingAsync(content, ct);
}
```

---

## Dependencies

### Depends On

- Phase 018: Logging Infrastructure (provides ILogger configuration, correlation IDs, structured logging patterns)

### Blocks

- No direct blockers (this phase provides diagnostic documentation and utilities)

---

## Verification Steps

After completing this phase, verify:

1. **Slow RAG Query Diagnosis**: Simulate a slow query and verify all timing phases are logged
2. **Missing Document Diagnosis**: Add a document and trace its lifecycle through logs
3. **Indexing Failure Diagnosis**: Simulate failures (stop Ollama, invalid frontmatter) and verify error codes are logged
4. **Diagnostic Documentation**: Verify DIAGNOSTICS.md contains all scenarios with examples
5. **Correlation Tracing**: Verify all logs for a single operation share the same correlation ID

### Test Commands

```bash
# Simulate slow query diagnostics
grep -E "Embedding generated|Vector search completed|Loaded.*linked documents" logs.txt

# Verify file watcher logging
grep "File watcher detected" logs.txt

# Verify error code logging
grep -E "(EMBEDDING_SERVICE_ERROR|DATABASE_ERROR|SCHEMA_VALIDATION_FAILED)" logs.txt

# Trace a request by correlation ID
CORRELATION_ID="abc12345"
grep "$CORRELATION_ID" logs.txt
```

---

## Files to Create/Modify

### New Files

- `src/CSharpCompoundDocs.Core/Diagnostics/RagDiagnosticLogger.cs`
- `src/CSharpCompoundDocs.Core/Diagnostics/DocumentDiagnosticLogger.cs`
- `src/CSharpCompoundDocs.Core/Diagnostics/IndexingFailureDiagnosticLogger.cs`
- `src/CSharpCompoundDocs.Core/Diagnostics/DiagnosticTimer.cs`
- `docs/DIAGNOSTICS.md` - Diagnostic workflow documentation

### Modified Files

- Services that require enhanced diagnostic logging (file watcher, embedding service, document repository, RAG service)

---

## Notes

- Diagnostic logging should have minimal performance impact when not actively investigating
- Use Debug level for detailed diagnostic information that's not needed in normal operation
- Warning level should be used for threshold violations to make them easy to find
- The DIAGNOSTICS.md file should be kept updated as new scenarios are identified
- Consider adding a diagnostic mode that enables more verbose logging temporarily
