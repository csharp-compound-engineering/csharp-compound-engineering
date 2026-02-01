# Phase 058: File Watcher Error Handling

> **Status**: NOT_STARTED
> **Effort Estimate**: 5-7 hours
> **Category**: Document Processing
> **Prerequisites**: Phase 055

---

## Spec References

This phase implements error handling for the file watcher service as defined in:

- **spec/mcp-server/file-watcher.md** - [Error Handling](../spec/mcp-server/file-watcher.md#error-handling) (lines 198-227)
- **spec/observability.md** - [File Watcher Service](../spec/observability.md#file-watcher-service) (lines 237-245)
- **spec/mcp-server/ollama-integration.md** - [Graceful Degradation](../spec/mcp-server/ollama-integration.md#graceful-degradation) (lines 183-219)
- **spec/mcp-server/ollama-integration.md** - [Retry Policies](../spec/mcp-server/ollama-integration.md#retry-policies) (lines 125-132)

---

## Objectives

1. Implement file system error handling (access denied, file in use, path too long)
2. Implement processing error handling (parse errors, schema validation failures)
3. Implement embedding failure handling with retry queue integration
4. Implement retry strategies for transient file system errors
5. Implement contextual error logging with structured fields
6. Implement graceful degradation to continue watching unaffected files

---

## Acceptance Criteria

### File System Error Handling

- [ ] `FileNotFoundException` after event detection: Log warning, skip file (file may have been deleted)
- [ ] `UnauthorizedAccessException` (permission denied): Log error, retry once, then skip file
- [ ] `PathTooLongException`: Log error with path, skip file permanently
- [ ] `IOException` (file in use): Log warning, add to retry queue with exponential backoff
- [ ] Invalid UTF-8 encoding: Log warning with file path, skip file
- [ ] Directory not found: Log error, continue watching other directories

### Processing Error Handling

- [ ] Markdown parse errors: Log warning with line number context, skip indexing
- [ ] Schema validation failures: Log warning with field-level errors, skip indexing
- [ ] Frontmatter parsing failures: Log warning with parse error details, skip indexing
- [ ] Content hash calculation failures: Log error, skip file

### Embedding Failure Handling

- [ ] `EMBEDDING_SERVICE_ERROR`: Queue document for retry (per ollama-integration.md)
- [ ] Circuit breaker open: Add to pending queue, log info with retry timing
- [ ] Retry exhausted: Log error with attempt count, mark document as failed
- [ ] Integration with reconciliation for failed documents on restart

### Retry Strategy Implementation

- [ ] `FileSystemRetryPolicy` class for transient file errors
- [ ] Maximum 3 retry attempts for file access errors
- [ ] Exponential backoff: 100ms, 200ms, 400ms delays
- [ ] Jitter applied to prevent thundering herd
- [ ] Retry only for transient errors (IOException, file in use)
- [ ] No retry for permanent errors (path too long, invalid encoding)

### Error Logging with Context

- [ ] All errors include `DocumentPath` (relative path)
- [ ] All errors include `EventType` (Created, Modified, Deleted, Renamed)
- [ ] All errors include `CorrelationId` for tracing
- [ ] Processing errors include `CharCount` when available
- [ ] Retry errors include `AttemptNumber` and `MaxAttempts`
- [ ] Log levels follow spec: Debug for events, Info for operations, Warning for recoverable, Error for unrecoverable

### Graceful Degradation

- [ ] Single file failure does not stop watching other files
- [ ] Error in one subdirectory does not affect other subdirectories
- [ ] Accumulating errors do not cause watcher to stop
- [ ] Failed files tracked for reconciliation on next activation
- [ ] Health status exposed for diagnostic queries

---

## Implementation Notes

### Error Classification

Create error classification for appropriate handling:

```csharp
/// <summary>
/// Classifies file watcher errors for appropriate handling strategy.
/// </summary>
public enum FileWatcherErrorCategory
{
    /// <summary>Error that may resolve on retry (file in use, temporary lock).</summary>
    Transient,

    /// <summary>Permanent error that won't resolve with retry (path too long, invalid encoding).</summary>
    Permanent,

    /// <summary>Error from downstream service (embedding, database).</summary>
    ServiceFailure,

    /// <summary>Error in document content (parse error, schema validation).</summary>
    ContentError
}

/// <summary>
/// Classifies exceptions for handling strategy.
/// </summary>
public static class FileWatcherErrorClassifier
{
    public static FileWatcherErrorCategory Classify(Exception ex) => ex switch
    {
        FileNotFoundException => FileWatcherErrorCategory.Transient,  // File may reappear
        UnauthorizedAccessException => FileWatcherErrorCategory.Transient,  // Permissions may change
        IOException when IsFileLocked(ex) => FileWatcherErrorCategory.Transient,
        PathTooLongException => FileWatcherErrorCategory.Permanent,
        DecoderFallbackException => FileWatcherErrorCategory.Permanent,  // Invalid encoding
        MarkdownParseException => FileWatcherErrorCategory.ContentError,
        SchemaValidationException => FileWatcherErrorCategory.ContentError,
        EmbeddingServiceException => FileWatcherErrorCategory.ServiceFailure,
        _ => FileWatcherErrorCategory.Permanent  // Default to permanent for unknown errors
    };

    private static bool IsFileLocked(IOException ex)
    {
        // Check for file lock error codes
        const int ERROR_SHARING_VIOLATION = 32;
        const int ERROR_LOCK_VIOLATION = 33;
        return ex.HResult == ERROR_SHARING_VIOLATION || ex.HResult == ERROR_LOCK_VIOLATION;
    }
}
```

### File System Error Handler

```csharp
/// <summary>
/// Handles file system errors during file watcher operations.
/// </summary>
public class FileSystemErrorHandler
{
    private readonly ILogger<FileSystemErrorHandler> _logger;
    private readonly FileWatcherRetryPolicy _retryPolicy;
    private readonly IFailedFileTracker _failedFileTracker;

    public FileSystemErrorHandler(
        ILogger<FileSystemErrorHandler> logger,
        FileWatcherRetryPolicy retryPolicy,
        IFailedFileTracker failedFileTracker)
    {
        _logger = logger;
        _retryPolicy = retryPolicy;
        _failedFileTracker = failedFileTracker;
    }

    /// <summary>
    /// Handles a file system error, applying retry logic if appropriate.
    /// </summary>
    /// <returns>True if the operation should be retried, false if it should be skipped.</returns>
    public async Task<bool> HandleFileSystemErrorAsync(
        Exception exception,
        string relativePath,
        WatcherChangeTypes eventType,
        int attemptNumber,
        CancellationToken ct)
    {
        var category = FileWatcherErrorClassifier.Classify(exception);

        switch (category)
        {
            case FileWatcherErrorCategory.Transient:
                return await HandleTransientErrorAsync(exception, relativePath, eventType, attemptNumber, ct);

            case FileWatcherErrorCategory.Permanent:
                HandlePermanentError(exception, relativePath, eventType);
                return false;

            case FileWatcherErrorCategory.ContentError:
                HandleContentError(exception, relativePath, eventType);
                return false;

            case FileWatcherErrorCategory.ServiceFailure:
                await HandleServiceFailureAsync(exception, relativePath, eventType, ct);
                return false;

            default:
                _logger.LogError(exception,
                    "Unclassified error processing file: {DocumentPath} Event: {EventType}",
                    relativePath, eventType);
                return false;
        }
    }

    private async Task<bool> HandleTransientErrorAsync(
        Exception exception,
        string relativePath,
        WatcherChangeTypes eventType,
        int attemptNumber,
        CancellationToken ct)
    {
        if (attemptNumber >= _retryPolicy.MaxAttempts)
        {
            _logger.LogError(exception,
                "File access failed after {MaxAttempts} attempts: {DocumentPath} Event: {EventType}",
                _retryPolicy.MaxAttempts, relativePath, eventType);

            await _failedFileTracker.TrackFailedFileAsync(relativePath, exception.Message, ct);
            return false;
        }

        var delay = _retryPolicy.GetDelay(attemptNumber);
        _logger.LogWarning(
            "Transient error accessing file, retry {AttemptNumber} of {MaxAttempts} in {DelayMs}ms: {DocumentPath} Error: {ErrorMessage}",
            attemptNumber + 1, _retryPolicy.MaxAttempts, delay.TotalMilliseconds, relativePath, exception.Message);

        await Task.Delay(delay, ct);
        return true;
    }

    private void HandlePermanentError(
        Exception exception,
        string relativePath,
        WatcherChangeTypes eventType)
    {
        switch (exception)
        {
            case PathTooLongException:
                _logger.LogError(
                    "Path too long, skipping file: {DocumentPath} Event: {EventType}",
                    relativePath, eventType);
                break;

            case DecoderFallbackException:
                _logger.LogWarning(
                    "Invalid UTF-8 encoding, skipping file: {DocumentPath} Event: {EventType}",
                    relativePath, eventType);
                break;

            default:
                _logger.LogError(exception,
                    "Permanent error, skipping file: {DocumentPath} Event: {EventType}",
                    relativePath, eventType);
                break;
        }
    }

    private void HandleContentError(
        Exception exception,
        string relativePath,
        WatcherChangeTypes eventType)
    {
        switch (exception)
        {
            case MarkdownParseException parseEx:
                _logger.LogWarning(
                    "Markdown parse error at line {LineNumber}, skipping indexing: {DocumentPath} Error: {ErrorMessage}",
                    parseEx.LineNumber, relativePath, parseEx.Message);
                break;

            case SchemaValidationException schemaEx:
                _logger.LogWarning(
                    "Schema validation failed, skipping indexing: {DocumentPath} Fields: {FailedFields}",
                    relativePath, string.Join(", ", schemaEx.FailedFields));
                break;

            default:
                _logger.LogWarning(exception,
                    "Content error, skipping indexing: {DocumentPath} Event: {EventType}",
                    relativePath, eventType);
                break;
        }
    }

    private async Task HandleServiceFailureAsync(
        Exception exception,
        string relativePath,
        WatcherChangeTypes eventType,
        CancellationToken ct)
    {
        _logger.LogWarning(
            "Embedding service unavailable, queuing for retry: {DocumentPath} Event: {EventType}",
            relativePath, eventType);

        await _failedFileTracker.QueueForRetryAsync(relativePath, eventType, ct);
    }
}
```

### Retry Policy Implementation

```csharp
/// <summary>
/// Retry policy for file watcher operations with exponential backoff.
/// </summary>
public class FileWatcherRetryPolicy
{
    private readonly Random _jitterRandom = new();

    /// <summary>
    /// Maximum number of retry attempts for transient errors.
    /// </summary>
    public int MaxAttempts { get; init; } = 3;

    /// <summary>
    /// Base delay for exponential backoff.
    /// </summary>
    public TimeSpan BaseDelay { get; init; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Maximum jitter as a percentage of the delay (0.0 to 1.0).
    /// </summary>
    public double JitterFactor { get; init; } = 0.2;

    /// <summary>
    /// Calculates the delay for a given attempt number (0-based).
    /// </summary>
    public TimeSpan GetDelay(int attemptNumber)
    {
        // Exponential backoff: 100ms, 200ms, 400ms
        var baseMs = BaseDelay.TotalMilliseconds * Math.Pow(2, attemptNumber);

        // Apply jitter to prevent thundering herd
        var jitterMs = baseMs * JitterFactor * _jitterRandom.NextDouble();
        var totalMs = baseMs + jitterMs;

        return TimeSpan.FromMilliseconds(totalMs);
    }
}
```

### Failed File Tracker

```csharp
/// <summary>
/// Tracks files that failed to process for later reconciliation.
/// </summary>
public interface IFailedFileTracker
{
    /// <summary>
    /// Records a file that permanently failed processing.
    /// </summary>
    Task TrackFailedFileAsync(string relativePath, string errorMessage, CancellationToken ct);

    /// <summary>
    /// Queues a file for retry when service becomes available.
    /// </summary>
    Task QueueForRetryAsync(string relativePath, WatcherChangeTypes eventType, CancellationToken ct);

    /// <summary>
    /// Gets all files pending retry.
    /// </summary>
    Task<IReadOnlyList<PendingFile>> GetPendingFilesAsync(CancellationToken ct);

    /// <summary>
    /// Clears a file from the pending queue after successful processing.
    /// </summary>
    Task ClearPendingFileAsync(string relativePath, CancellationToken ct);

    /// <summary>
    /// Gets summary of failed/pending files for diagnostics.
    /// </summary>
    Task<FileTrackerStatus> GetStatusAsync(CancellationToken ct);
}

/// <summary>
/// A file pending retry in the queue.
/// </summary>
public record PendingFile(
    string RelativePath,
    WatcherChangeTypes EventType,
    DateTimeOffset QueuedAt,
    int RetryCount);

/// <summary>
/// Status of the failed file tracker for diagnostics.
/// </summary>
public record FileTrackerStatus(
    int PendingCount,
    int FailedCount,
    DateTimeOffset? OldestPendingFile);
```

### In-Memory Failed File Tracker

```csharp
/// <summary>
/// In-memory implementation of failed file tracker.
/// Pending files are reprocessed via reconciliation on restart.
/// </summary>
public class InMemoryFailedFileTracker : IFailedFileTracker
{
    private readonly ConcurrentDictionary<string, PendingFile> _pendingFiles = new();
    private readonly ConcurrentDictionary<string, string> _failedFiles = new();
    private readonly ILogger<InMemoryFailedFileTracker> _logger;

    public InMemoryFailedFileTracker(ILogger<InMemoryFailedFileTracker> logger)
    {
        _logger = logger;
    }

    public Task TrackFailedFileAsync(string relativePath, string errorMessage, CancellationToken ct)
    {
        _failedFiles[relativePath] = errorMessage;
        _logger.LogDebug("Tracked failed file: {DocumentPath}", relativePath);
        return Task.CompletedTask;
    }

    public Task QueueForRetryAsync(string relativePath, WatcherChangeTypes eventType, CancellationToken ct)
    {
        var pending = new PendingFile(relativePath, eventType, DateTimeOffset.UtcNow, 0);
        _pendingFiles.AddOrUpdate(
            relativePath,
            pending,
            (_, existing) => existing with { RetryCount = existing.RetryCount + 1 });

        _logger.LogDebug("Queued file for retry: {DocumentPath} Event: {EventType}", relativePath, eventType);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PendingFile>> GetPendingFilesAsync(CancellationToken ct)
    {
        var files = _pendingFiles.Values
            .OrderBy(f => f.QueuedAt)
            .ToList();

        return Task.FromResult<IReadOnlyList<PendingFile>>(files);
    }

    public Task ClearPendingFileAsync(string relativePath, CancellationToken ct)
    {
        _pendingFiles.TryRemove(relativePath, out _);
        _logger.LogDebug("Cleared pending file: {DocumentPath}", relativePath);
        return Task.CompletedTask;
    }

    public Task<FileTrackerStatus> GetStatusAsync(CancellationToken ct)
    {
        var oldestPending = _pendingFiles.Values
            .OrderBy(f => f.QueuedAt)
            .FirstOrDefault()?.QueuedAt;

        var status = new FileTrackerStatus(
            _pendingFiles.Count,
            _failedFiles.Count,
            oldestPending);

        return Task.FromResult(status);
    }
}
```

### Integration with File Watcher Service

```csharp
/// <summary>
/// Processes a file change event with comprehensive error handling.
/// </summary>
private async Task ProcessFileChangeAsync(
    string relativePath,
    WatcherChangeTypes eventType,
    CancellationToken ct)
{
    var correlationId = Guid.NewGuid().ToString("N")[..8];

    using (_logger.BeginScope(new Dictionary<string, object>
    {
        ["CorrelationId"] = correlationId,
        ["DocumentPath"] = relativePath,
        ["EventType"] = eventType.ToString()
    }))
    {
        _logger.LogInformation("Processing file change: {DocumentPath} Event: {EventType}",
            relativePath, eventType);

        for (int attempt = 0; attempt < _retryPolicy.MaxAttempts; attempt++)
        {
            try
            {
                await ProcessFileInternalAsync(relativePath, eventType, ct);
                _logger.LogInformation("File processed successfully: {DocumentPath}", relativePath);
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                var shouldRetry = await _errorHandler.HandleFileSystemErrorAsync(
                    ex, relativePath, eventType, attempt, ct);

                if (!shouldRetry)
                {
                    return;
                }
            }
        }
    }
}
```

### Custom Exception Types

```csharp
/// <summary>
/// Exception thrown when markdown parsing fails.
/// </summary>
public class MarkdownParseException : Exception
{
    public int LineNumber { get; }
    public int ColumnNumber { get; }

    public MarkdownParseException(string message, int lineNumber, int columnNumber = 0)
        : base(message)
    {
        LineNumber = lineNumber;
        ColumnNumber = columnNumber;
    }
}

/// <summary>
/// Exception thrown when schema validation fails.
/// </summary>
public class SchemaValidationException : Exception
{
    public IReadOnlyList<string> FailedFields { get; }

    public SchemaValidationException(string message, IEnumerable<string> failedFields)
        : base(message)
    {
        FailedFields = failedFields.ToList();
    }
}

/// <summary>
/// Exception thrown when embedding service fails.
/// </summary>
public class EmbeddingServiceException : Exception
{
    public string CircuitState { get; }
    public int? RetryAfterSeconds { get; }

    public EmbeddingServiceException(string message, string circuitState, int? retryAfterSeconds = null)
        : base(message)
    {
        CircuitState = circuitState;
        RetryAfterSeconds = retryAfterSeconds;
    }
}
```

### Logging Configuration

Per the observability spec, file watcher events use the following log levels:

| Level | Events |
|-------|--------|
| Debug | All file events before debounce, retry delays |
| Info | Index/update/delete operations, successful processing |
| Warning | Schema validation failures, recoverable errors, retries |
| Error | Unrecoverable errors (permission denied, path too long) |

---

## Test Cases

### Unit Tests

```csharp
[Fact]
public void ErrorClassifier_ClassifiesFileNotFoundAsTransient()
{
    // Arrange
    var exception = new FileNotFoundException("File not found", "test.md");

    // Act
    var category = FileWatcherErrorClassifier.Classify(exception);

    // Assert
    Assert.Equal(FileWatcherErrorCategory.Transient, category);
}

[Fact]
public void ErrorClassifier_ClassifiesPathTooLongAsPermanent()
{
    // Arrange
    var exception = new PathTooLongException("Path too long");

    // Act
    var category = FileWatcherErrorClassifier.Classify(exception);

    // Assert
    Assert.Equal(FileWatcherErrorCategory.Permanent, category);
}

[Fact]
public void RetryPolicy_AppliesExponentialBackoff()
{
    // Arrange
    var policy = new FileWatcherRetryPolicy
    {
        BaseDelay = TimeSpan.FromMilliseconds(100),
        JitterFactor = 0  // Disable jitter for predictable testing
    };

    // Act & Assert
    Assert.Equal(100, policy.GetDelay(0).TotalMilliseconds);
    Assert.Equal(200, policy.GetDelay(1).TotalMilliseconds);
    Assert.Equal(400, policy.GetDelay(2).TotalMilliseconds);
}

[Fact]
public async Task ErrorHandler_RetriesTransientError()
{
    // Arrange
    var logger = new Mock<ILogger<FileSystemErrorHandler>>();
    var retryPolicy = new FileWatcherRetryPolicy { MaxAttempts = 3 };
    var tracker = new Mock<IFailedFileTracker>();
    var handler = new FileSystemErrorHandler(logger.Object, retryPolicy, tracker.Object);

    // Act
    var shouldRetry = await handler.HandleFileSystemErrorAsync(
        new IOException("File in use"),
        "test.md",
        WatcherChangeTypes.Modified,
        attemptNumber: 0,
        CancellationToken.None);

    // Assert
    Assert.True(shouldRetry);
}

[Fact]
public async Task ErrorHandler_DoesNotRetryPermanentError()
{
    // Arrange
    var logger = new Mock<ILogger<FileSystemErrorHandler>>();
    var retryPolicy = new FileWatcherRetryPolicy();
    var tracker = new Mock<IFailedFileTracker>();
    var handler = new FileSystemErrorHandler(logger.Object, retryPolicy, tracker.Object);

    // Act
    var shouldRetry = await handler.HandleFileSystemErrorAsync(
        new PathTooLongException("Path too long"),
        "test.md",
        WatcherChangeTypes.Created,
        attemptNumber: 0,
        CancellationToken.None);

    // Assert
    Assert.False(shouldRetry);
}

[Fact]
public async Task ErrorHandler_QueuesForRetryOnEmbeddingFailure()
{
    // Arrange
    var logger = new Mock<ILogger<FileSystemErrorHandler>>();
    var retryPolicy = new FileWatcherRetryPolicy();
    var tracker = new Mock<IFailedFileTracker>();
    var handler = new FileSystemErrorHandler(logger.Object, retryPolicy, tracker.Object);

    // Act
    await handler.HandleFileSystemErrorAsync(
        new EmbeddingServiceException("Service unavailable", "open", 30),
        "test.md",
        WatcherChangeTypes.Created,
        attemptNumber: 0,
        CancellationToken.None);

    // Assert
    tracker.Verify(t => t.QueueForRetryAsync("test.md", WatcherChangeTypes.Created, It.IsAny<CancellationToken>()), Times.Once);
}

[Fact]
public async Task FailedFileTracker_TracksAndReturnsPendingFiles()
{
    // Arrange
    var logger = new Mock<ILogger<InMemoryFailedFileTracker>>();
    var tracker = new InMemoryFailedFileTracker(logger.Object);

    // Act
    await tracker.QueueForRetryAsync("file1.md", WatcherChangeTypes.Created, CancellationToken.None);
    await tracker.QueueForRetryAsync("file2.md", WatcherChangeTypes.Modified, CancellationToken.None);
    var pending = await tracker.GetPendingFilesAsync(CancellationToken.None);

    // Assert
    Assert.Equal(2, pending.Count);
}

[Fact]
public async Task FailedFileTracker_ClearsPendingFile()
{
    // Arrange
    var logger = new Mock<ILogger<InMemoryFailedFileTracker>>();
    var tracker = new InMemoryFailedFileTracker(logger.Object);
    await tracker.QueueForRetryAsync("test.md", WatcherChangeTypes.Created, CancellationToken.None);

    // Act
    await tracker.ClearPendingFileAsync("test.md", CancellationToken.None);
    var pending = await tracker.GetPendingFilesAsync(CancellationToken.None);

    // Assert
    Assert.Empty(pending);
}
```

### Integration Tests

```csharp
[Fact]
public async Task FileWatcher_ContinuesAfterSingleFileError()
{
    // Test that a single file error doesn't stop processing of other files
}

[Fact]
public async Task FileWatcher_RetriesTransientErrorsWithBackoff()
{
    // Test retry behavior with real file system locks
}

[Fact]
public async Task FileWatcher_QueuesFailedEmbeddingsForReconciliation()
{
    // Test that embedding failures are queued and processed on next startup
}

[Fact]
public async Task FileWatcher_LogsWithCorrectStructuredFields()
{
    // Verify structured logging fields are present
}
```

---

## Dependencies

### Depends On

- Phase 055: File Watcher Service (base service implementation)
- Phase 030: Resilience Patterns (circuit breaker integration)
- Phase 018: Logging Infrastructure (structured logging)

### Blocks

- Subsequent phases that depend on robust file watcher error handling
- Reconciliation service phases that process failed file queue

---

## Verification Steps

After completing this phase, verify:

1. **Transient error retry**: Simulate file lock, verify 3 retry attempts with exponential backoff
2. **Permanent error skip**: Use path > 260 chars, verify immediate skip without retry
3. **Content error logging**: Create malformed markdown, verify warning log with details
4. **Embedding failure queue**: Mock circuit breaker open, verify file queued for retry
5. **Graceful degradation**: Cause error in one file, verify other files still processed
6. **Structured logging**: Verify CorrelationId, DocumentPath, EventType in all logs
7. **Tracker status**: Query tracker status, verify pending/failed counts accurate

---

## Notes

- The in-memory failed file tracker does not persist across restarts; reconciliation handles this
- Error classification may need adjustment as edge cases are discovered
- Consider adding telemetry counters for error categories in post-MVP metrics phase
- The jitter factor helps prevent retry storms when multiple files fail simultaneously
- Custom exceptions provide structured data for logging and potential future error responses
