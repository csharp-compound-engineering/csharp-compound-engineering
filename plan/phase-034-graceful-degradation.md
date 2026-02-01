# Phase 034: Graceful Degradation When Ollama Unavailable

> **Status**: NOT_STARTED
> **Effort Estimate**: 5-7 hours
> **Category**: MCP Server Core
> **Prerequisites**: Phase 030 (Ollama Integration - HttpClient, Resilience Handler)

---

## Spec References

This phase implements the graceful degradation behavior defined in:

- **spec/mcp-server/ollama-integration.md** - [Graceful Degradation](../spec/mcp-server/ollama-integration.md#graceful-degradation) section (lines 183-219)
- **spec/mcp-server/ollama-integration.md** - [Circuit Breaker States](../spec/mcp-server/ollama-integration.md#circuit-breaker-states) (lines 147-179)
- **spec/mcp-server/ollama-integration.md** - [Apple Silicon Note](../spec/mcp-server/ollama-integration.md#apple-silicon-note) (lines 223-250)
- **spec/mcp-server/file-watcher.md** - [Crash Recovery](../spec/mcp-server/file-watcher.md#crash-recovery) (lines 125-144)

---

## Objectives

1. Implement error responses for RAG queries when Ollama is unavailable
2. Implement error responses for semantic search when Ollama is unavailable
3. Create file watcher queue for deferred indexing during Ollama outages
4. Build health status indicators for circuit breaker state
5. Provide user-friendly error messages with retry guidance
6. Implement recovery detection and deferred queue processing

---

## Acceptance Criteria

### Error Response Handling

- [ ] `OllamaUnavailableException` created with circuit breaker state information
- [ ] RAG query returns structured error when Ollama unavailable:
  - [ ] Error code: `EMBEDDING_SERVICE_ERROR`
  - [ ] Message includes circuit breaker state
  - [ ] Details include `retry_after_seconds` when circuit is open
- [ ] Semantic search returns structured error when Ollama unavailable:
  - [ ] Error code: `EMBEDDING_SERVICE_ERROR`
  - [ ] Clear message that semantic search cannot be performed
- [ ] Apple Silicon detection returns specific `OLLAMA_NOT_RUNNING` error

### Deferred Indexing Queue

- [ ] `IDeferredIndexingQueue` interface created
- [ ] In-memory queue implementation for pending file changes
- [ ] Queue operations: Enqueue, Dequeue, Peek, Count, Clear
- [ ] File watcher enqueues changes when Ollama unavailable
- [ ] Queue is not persisted (reconciliation on restart handles recovery)
- [ ] Maximum queue size configurable (default: 1000 items)
- [ ] Queue overflow handling (oldest items dropped with warning)

### Health Status Indicators

- [ ] `IOllamaHealthService` interface for health checking
- [ ] Health status includes:
  - [ ] `IsAvailable` (bool)
  - [ ] `CircuitState` (Closed, Open, HalfOpen)
  - [ ] `RetryAfterSeconds` (when circuit is open)
  - [ ] `LastSuccessfulRequest` (timestamp)
  - [ ] `FailureCount` (current failure count)
- [ ] Health status exposed to tools for pre-flight checks

### Recovery Detection

- [ ] Circuit breaker state change events raised
- [ ] Recovery handler triggers deferred queue processing
- [ ] Queue processing happens in background (non-blocking)
- [ ] Processing respects rate limiter (no burst flooding)
- [ ] Partial processing supported (continues where left off)

### User-Friendly Error Messages

- [ ] Error messages are actionable and specific
- [ ] Apple Silicon message suggests starting native Ollama
- [ ] Circuit breaker open message includes wait time
- [ ] Retry guidance provided in all error responses

---

## Implementation Notes

### OllamaUnavailableException

```csharp
namespace CompoundDocs.McpServer.Exceptions;

/// <summary>
/// Exception thrown when Ollama embedding service is unavailable.
/// </summary>
public sealed class OllamaUnavailableException : EmbeddingServiceException
{
    /// <summary>
    /// Current circuit breaker state.
    /// </summary>
    public CircuitState CircuitState { get; }

    /// <summary>
    /// Seconds until retry should be attempted (when circuit is open).
    /// </summary>
    public int? RetryAfterSeconds { get; }

    /// <summary>
    /// Whether this is an Apple Silicon platform requiring native Ollama.
    /// </summary>
    public bool IsAppleSilicon { get; }

    public override object? Details => new
    {
        CircuitState = CircuitState.ToString().ToLowerInvariant(),
        RetryAfterSeconds,
        Platform = IsAppleSilicon ? "darwin-arm64" : null,
        ExpectedHost = IsAppleSilicon ? "http://localhost:11434" : null
    };

    public OllamaUnavailableException(CircuitState state, int? retryAfterSeconds = null)
        : base(BuildMessage(state, retryAfterSeconds, false))
    {
        CircuitState = state;
        RetryAfterSeconds = retryAfterSeconds;
        IsAppleSilicon = false;
    }

    public OllamaUnavailableException(bool isAppleSilicon)
        : base(BuildAppleSiliconMessage())
    {
        CircuitState = CircuitState.Open;
        IsAppleSilicon = isAppleSilicon;
    }

    private static string BuildMessage(CircuitState state, int? retryAfterSeconds, bool isAppleSilicon)
    {
        return state switch
        {
            CircuitState.Open when retryAfterSeconds.HasValue =>
                $"Embedding service unavailable. The circuit breaker is open. Try again in {retryAfterSeconds} seconds.",
            CircuitState.Open =>
                "Embedding service unavailable. The circuit breaker is open.",
            CircuitState.HalfOpen =>
                "Embedding service is recovering. Please retry your request.",
            _ =>
                "Embedding service is temporarily unavailable."
        };
    }

    private static string BuildAppleSiliconMessage()
    {
        return "Ollama server not detected. On Apple Silicon, Ollama must be running natively for Metal acceleration. " +
               "Please start Ollama before using this tool.";
    }
}

/// <summary>
/// Circuit breaker states.
/// </summary>
public enum CircuitState
{
    /// <summary>Normal operation, requests flow through.</summary>
    Closed,

    /// <summary>Failure threshold exceeded, all requests fail fast.</summary>
    Open,

    /// <summary>Testing with single request after break duration.</summary>
    HalfOpen
}
```

### Deferred Indexing Queue Interface

```csharp
namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Queue for file changes that couldn't be indexed due to Ollama unavailability.
/// </summary>
public interface IDeferredIndexingQueue
{
    /// <summary>
    /// Enqueue a file change for deferred indexing.
    /// </summary>
    /// <param name="item">The file change to queue.</param>
    /// <returns>True if enqueued, false if queue is full.</returns>
    bool Enqueue(DeferredIndexItem item);

    /// <summary>
    /// Try to dequeue the next item.
    /// </summary>
    /// <param name="item">The dequeued item if successful.</param>
    /// <returns>True if an item was dequeued.</returns>
    bool TryDequeue(out DeferredIndexItem item);

    /// <summary>
    /// Peek at the next item without removing it.
    /// </summary>
    /// <param name="item">The next item if available.</param>
    /// <returns>True if an item is available.</returns>
    bool TryPeek(out DeferredIndexItem item);

    /// <summary>
    /// Number of items currently in the queue.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Clear all items from the queue.
    /// </summary>
    void Clear();

    /// <summary>
    /// Maximum queue capacity.
    /// </summary>
    int MaxCapacity { get; }
}

/// <summary>
/// Represents a deferred file indexing operation.
/// </summary>
public sealed record DeferredIndexItem
{
    /// <summary>
    /// Relative path to the file.
    /// </summary>
    public required string RelativePath { get; init; }

    /// <summary>
    /// Type of change that triggered indexing.
    /// </summary>
    public required FileChangeType ChangeType { get; init; }

    /// <summary>
    /// Timestamp when the change was detected.
    /// </summary>
    public required DateTimeOffset DetectedAt { get; init; }

    /// <summary>
    /// Number of times this item has been attempted.
    /// </summary>
    public int AttemptCount { get; init; }
}

/// <summary>
/// Type of file system change.
/// </summary>
public enum FileChangeType
{
    Created,
    Modified,
    Deleted,
    Renamed
}
```

### Deferred Indexing Queue Implementation

```csharp
namespace CompoundDocs.McpServer.Services;

/// <summary>
/// In-memory queue for deferred file indexing operations.
/// Thread-safe for concurrent access from file watcher and recovery processor.
/// </summary>
public sealed class DeferredIndexingQueue : IDeferredIndexingQueue
{
    private readonly ConcurrentQueue<DeferredIndexItem> _queue = new();
    private readonly ILogger<DeferredIndexingQueue> _logger;
    private int _count;

    public int MaxCapacity { get; }

    public int Count => _count;

    public DeferredIndexingQueue(
        ILogger<DeferredIndexingQueue> logger,
        IOptions<DeferredIndexingOptions> options)
    {
        _logger = logger;
        MaxCapacity = options.Value.MaxQueueSize;
    }

    public bool Enqueue(DeferredIndexItem item)
    {
        // Check capacity before enqueueing
        if (_count >= MaxCapacity)
        {
            // Drop oldest item to make room
            if (_queue.TryDequeue(out var dropped))
            {
                Interlocked.Decrement(ref _count);
                _logger.LogWarning(
                    "Deferred indexing queue at capacity ({MaxCapacity}). Dropped oldest item: {DroppedPath}",
                    MaxCapacity, dropped.RelativePath);
            }
        }

        _queue.Enqueue(item);
        Interlocked.Increment(ref _count);

        _logger.LogDebug(
            "Enqueued deferred index item: {Path} ({ChangeType}). Queue size: {Count}",
            item.RelativePath, item.ChangeType, _count);

        return true;
    }

    public bool TryDequeue(out DeferredIndexItem item)
    {
        if (_queue.TryDequeue(out item!))
        {
            Interlocked.Decrement(ref _count);
            return true;
        }
        return false;
    }

    public bool TryPeek(out DeferredIndexItem item)
    {
        return _queue.TryPeek(out item!);
    }

    public void Clear()
    {
        while (_queue.TryDequeue(out _))
        {
            Interlocked.Decrement(ref _count);
        }

        _logger.LogInformation("Deferred indexing queue cleared");
    }
}

/// <summary>
/// Configuration options for deferred indexing queue.
/// </summary>
public sealed class DeferredIndexingOptions
{
    /// <summary>
    /// Maximum number of items in the queue. Default: 1000.
    /// </summary>
    public int MaxQueueSize { get; set; } = 1000;

    /// <summary>
    /// Maximum number of retry attempts per item. Default: 3.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;
}
```

### Ollama Health Service

```csharp
namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Service for monitoring Ollama availability and circuit breaker state.
/// </summary>
public interface IOllamaHealthService
{
    /// <summary>
    /// Get current health status.
    /// </summary>
    OllamaHealthStatus GetStatus();

    /// <summary>
    /// Check if Ollama is available for requests.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Event raised when circuit breaker state changes.
    /// </summary>
    event EventHandler<CircuitStateChangedEventArgs>? CircuitStateChanged;
}

/// <summary>
/// Ollama service health status.
/// </summary>
public sealed record OllamaHealthStatus
{
    /// <summary>
    /// Whether Ollama is available for requests.
    /// </summary>
    public required bool IsAvailable { get; init; }

    /// <summary>
    /// Current circuit breaker state.
    /// </summary>
    public required CircuitState CircuitState { get; init; }

    /// <summary>
    /// Seconds until circuit breaker allows retry (when open).
    /// </summary>
    public int? RetryAfterSeconds { get; init; }

    /// <summary>
    /// Timestamp of last successful request.
    /// </summary>
    public DateTimeOffset? LastSuccessfulRequest { get; init; }

    /// <summary>
    /// Current failure count in sampling window.
    /// </summary>
    public int FailureCount { get; init; }

    /// <summary>
    /// Whether running on Apple Silicon (requires native Ollama).
    /// </summary>
    public bool IsAppleSilicon { get; init; }
}

/// <summary>
/// Event args for circuit breaker state changes.
/// </summary>
public sealed class CircuitStateChangedEventArgs : EventArgs
{
    /// <summary>
    /// Previous circuit state.
    /// </summary>
    public required CircuitState PreviousState { get; init; }

    /// <summary>
    /// New circuit state.
    /// </summary>
    public required CircuitState NewState { get; init; }

    /// <summary>
    /// Timestamp of state change.
    /// </summary>
    public required DateTimeOffset ChangedAt { get; init; }
}
```

### Ollama Health Service Implementation

```csharp
namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Monitors Ollama health and circuit breaker state.
/// Integrates with Polly's circuit breaker events.
/// </summary>
public sealed class OllamaHealthService : IOllamaHealthService, IDisposable
{
    private readonly ILogger<OllamaHealthService> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly bool _isAppleSilicon;

    private CircuitState _currentState = CircuitState.Closed;
    private DateTimeOffset? _lastSuccessfulRequest;
    private DateTimeOffset? _circuitOpenedAt;
    private int _failureCount;

    private const int CircuitBreakDurationSeconds = 30;

    public event EventHandler<CircuitStateChangedEventArgs>? CircuitStateChanged;

    public bool IsAvailable => _currentState == CircuitState.Closed ||
                               _currentState == CircuitState.HalfOpen;

    public OllamaHealthService(
        ILogger<OllamaHealthService> logger,
        TimeProvider timeProvider,
        IPlatformDetector platformDetector)
    {
        _logger = logger;
        _timeProvider = timeProvider;
        _isAppleSilicon = platformDetector.IsAppleSilicon();
    }

    public OllamaHealthStatus GetStatus()
    {
        var now = _timeProvider.GetUtcNow();
        int? retryAfterSeconds = null;

        if (_currentState == CircuitState.Open && _circuitOpenedAt.HasValue)
        {
            var elapsed = (now - _circuitOpenedAt.Value).TotalSeconds;
            var remaining = CircuitBreakDurationSeconds - (int)elapsed;
            retryAfterSeconds = Math.Max(0, remaining);
        }

        return new OllamaHealthStatus
        {
            IsAvailable = IsAvailable,
            CircuitState = _currentState,
            RetryAfterSeconds = retryAfterSeconds,
            LastSuccessfulRequest = _lastSuccessfulRequest,
            FailureCount = _failureCount,
            IsAppleSilicon = _isAppleSilicon
        };
    }

    /// <summary>
    /// Called by Polly circuit breaker on state transitions.
    /// </summary>
    public void OnCircuitStateChanged(CircuitState previousState, CircuitState newState)
    {
        var previousStateValue = _currentState;
        _currentState = newState;

        if (newState == CircuitState.Open)
        {
            _circuitOpenedAt = _timeProvider.GetUtcNow();
        }
        else if (newState == CircuitState.Closed)
        {
            _circuitOpenedAt = null;
            _failureCount = 0;
        }

        _logger.LogInformation(
            "Ollama circuit breaker state changed: {PreviousState} -> {NewState}",
            previousStateValue, newState);

        CircuitStateChanged?.Invoke(this, new CircuitStateChangedEventArgs
        {
            PreviousState = previousStateValue,
            NewState = newState,
            ChangedAt = _timeProvider.GetUtcNow()
        });
    }

    /// <summary>
    /// Record a successful request.
    /// </summary>
    public void RecordSuccess()
    {
        _lastSuccessfulRequest = _timeProvider.GetUtcNow();
    }

    /// <summary>
    /// Record a failed request.
    /// </summary>
    public void RecordFailure()
    {
        Interlocked.Increment(ref _failureCount);
    }

    public void Dispose()
    {
        // Unsubscribe from circuit breaker events if needed
    }
}
```

### Recovery Processor Background Service

```csharp
namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Background service that processes the deferred indexing queue when Ollama recovers.
/// </summary>
public sealed class DeferredIndexingProcessor : BackgroundService
{
    private readonly IOllamaHealthService _healthService;
    private readonly IDeferredIndexingQueue _queue;
    private readonly IDocumentIndexer _indexer;
    private readonly ILogger<DeferredIndexingProcessor> _logger;
    private readonly SemaphoreSlim _processingGate = new(1, 1);

    private const int ProcessingDelayMs = 100; // Respect rate limiter

    public DeferredIndexingProcessor(
        IOllamaHealthService healthService,
        IDeferredIndexingQueue queue,
        IDocumentIndexer indexer,
        ILogger<DeferredIndexingProcessor> logger)
    {
        _healthService = healthService;
        _queue = queue;
        _indexer = indexer;
        _logger = logger;

        _healthService.CircuitStateChanged += OnCircuitStateChanged;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Background service runs for lifetime of application
        // Processing is triggered by circuit state changes
        await Task.CompletedTask;
    }

    private async void OnCircuitStateChanged(object? sender, CircuitStateChangedEventArgs e)
    {
        // Only process when circuit closes (recovery confirmed)
        if (e.NewState != CircuitState.Closed)
        {
            return;
        }

        if (_queue.Count == 0)
        {
            return;
        }

        _logger.LogInformation(
            "Ollama recovered. Processing {Count} deferred indexing items",
            _queue.Count);

        await ProcessQueueAsync();
    }

    private async Task ProcessQueueAsync()
    {
        // Ensure only one processing run at a time
        if (!await _processingGate.WaitAsync(0))
        {
            _logger.LogDebug("Deferred queue processing already in progress");
            return;
        }

        try
        {
            var processedCount = 0;
            var failedCount = 0;

            while (_queue.TryDequeue(out var item))
            {
                // Check if Ollama is still available
                if (!_healthService.IsAvailable)
                {
                    _logger.LogWarning(
                        "Ollama became unavailable during deferred processing. Re-queuing item.");

                    // Re-queue the item we just dequeued
                    _queue.Enqueue(item with { AttemptCount = item.AttemptCount + 1 });
                    break;
                }

                try
                {
                    await ProcessItemAsync(item);
                    processedCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Failed to process deferred item: {Path}. Attempt {Attempt}",
                        item.RelativePath, item.AttemptCount + 1);

                    failedCount++;

                    // Re-queue if under retry limit
                    if (item.AttemptCount < 3)
                    {
                        _queue.Enqueue(item with { AttemptCount = item.AttemptCount + 1 });
                    }
                    else
                    {
                        _logger.LogError(
                            "Deferred item exceeded retry limit, dropping: {Path}",
                            item.RelativePath);
                    }
                }

                // Small delay to respect rate limiter
                await Task.Delay(ProcessingDelayMs);
            }

            _logger.LogInformation(
                "Deferred queue processing complete. Processed: {Processed}, Failed: {Failed}",
                processedCount, failedCount);
        }
        finally
        {
            _processingGate.Release();
        }
    }

    private async Task ProcessItemAsync(DeferredIndexItem item)
    {
        switch (item.ChangeType)
        {
            case FileChangeType.Created:
            case FileChangeType.Modified:
                await _indexer.IndexDocumentAsync(item.RelativePath);
                break;

            case FileChangeType.Deleted:
                await _indexer.RemoveDocumentAsync(item.RelativePath);
                break;

            case FileChangeType.Renamed:
                // Renamed files are handled as create (new path)
                await _indexer.IndexDocumentAsync(item.RelativePath);
                break;
        }
    }

    public override void Dispose()
    {
        _healthService.CircuitStateChanged -= OnCircuitStateChanged;
        _processingGate.Dispose();
        base.Dispose();
    }
}
```

### Error Response Helpers

Add to existing `ErrorMessages` class (from Phase 027):

```csharp
// In Services/ErrorMessages.cs

/// <summary>
/// Embedding service unavailable with circuit breaker open.
/// </summary>
public static string EmbeddingServiceCircuitOpen(int retryAfterSeconds)
    => $"Embedding service unavailable. The circuit breaker is open. Try again in {retryAfterSeconds} seconds.";

/// <summary>
/// Embedding service unavailable for semantic search.
/// </summary>
public static string SemanticSearchUnavailable()
    => "Cannot perform semantic search: embedding service unavailable.";

/// <summary>
/// Ollama not running on Apple Silicon.
/// </summary>
public static string OllamaNotRunningAppleSilicon()
    => "Ollama server not detected. On Apple Silicon, Ollama must be running natively for Metal acceleration. " +
       "Please start Ollama before using this tool.";

/// <summary>
/// File watcher queueing due to Ollama unavailability.
/// </summary>
public static string FileChangeDeferred(string path)
    => $"File change detected but Ollama unavailable. Queued for later indexing: {path}";
```

### Platform Detector

```csharp
namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Detects platform-specific characteristics.
/// </summary>
public interface IPlatformDetector
{
    /// <summary>
    /// Returns true if running on Apple Silicon (macOS ARM64).
    /// </summary>
    bool IsAppleSilicon();
}

/// <summary>
/// Platform detector implementation.
/// </summary>
public sealed class PlatformDetector : IPlatformDetector
{
    public bool IsAppleSilicon()
    {
        return OperatingSystem.IsMacOS() &&
               RuntimeInformation.OSArchitecture == Architecture.Arm64;
    }
}
```

### DI Registration

```csharp
// In Program.cs or service registration

services.Configure<DeferredIndexingOptions>(options =>
{
    options.MaxQueueSize = 1000;
    options.MaxRetryAttempts = 3;
});

services.AddSingleton<IPlatformDetector, PlatformDetector>();
services.AddSingleton<IDeferredIndexingQueue, DeferredIndexingQueue>();
services.AddSingleton<IOllamaHealthService, OllamaHealthService>();
services.AddHostedService<DeferredIndexingProcessor>();
```

---

## File Structure

```
src/CompoundDocs.McpServer/
├── Exceptions/
│   └── OllamaUnavailableException.cs
├── Models/
│   ├── CircuitState.cs
│   ├── DeferredIndexItem.cs
│   ├── FileChangeType.cs
│   └── OllamaHealthStatus.cs
└── Services/
    ├── DeferredIndexingOptions.cs
    ├── DeferredIndexingProcessor.cs
    ├── DeferredIndexingQueue.cs
    ├── IDeferredIndexingQueue.cs
    ├── IOllamaHealthService.cs
    ├── IPlatformDetector.cs
    ├── OllamaHealthService.cs
    └── PlatformDetector.cs

tests/CompoundDocs.McpServer.Tests/
└── Services/
    ├── DeferredIndexingQueueTests.cs
    ├── DeferredIndexingProcessorTests.cs
    ├── OllamaHealthServiceTests.cs
    └── PlatformDetectorTests.cs
```

---

## Dependencies

### Depends On

- **Phase 030**: Ollama Integration - Provides HttpClient configuration, resilience handler, and circuit breaker setup
- **Phase 027**: Standardized Error Responses - Provides `EmbeddingServiceException` base class and error code infrastructure
- **Phase 021**: MCP Server Project Structure - Provides project scaffold and DI container

### Blocks

- **Phase 035+**: Any phase implementing RAG query or semantic search tools (requires graceful degradation support)
- File watcher implementation (requires deferred indexing queue)

---

## Verification Steps

After completing this phase, verify:

1. **RAG query error response**: When Ollama is unavailable, RAG queries return:
   ```json
   {
     "error": true,
     "code": "EMBEDDING_SERVICE_ERROR",
     "message": "Embedding service unavailable. The circuit breaker is open. Try again in 30 seconds.",
     "details": {
       "circuit_state": "open",
       "retry_after_seconds": 30
     }
   }
   ```

2. **Semantic search error response**: When Ollama is unavailable:
   ```json
   {
     "error": true,
     "code": "EMBEDDING_SERVICE_ERROR",
     "message": "Cannot perform semantic search: embedding service unavailable.",
     "details": {}
   }
   ```

3. **Apple Silicon error response**: When Ollama not detected on Apple Silicon:
   ```json
   {
     "error": true,
     "code": "OLLAMA_NOT_RUNNING",
     "message": "Ollama server not detected. On Apple Silicon, Ollama must be running natively for Metal acceleration. Please start Ollama before using this tool.",
     "details": {
       "platform": "darwin-arm64",
       "expected_host": "http://localhost:11434"
     }
   }
   ```

4. **Deferred queue behavior**:
   ```bash
   # Simulate Ollama unavailability
   # Modify a file in compounding-docs
   # Verify queue count increases
   # Restore Ollama
   # Verify queue is processed
   ```

5. **Unit tests pass**:
   ```bash
   dotnet test --filter "FullyQualifiedName~DeferredIndexing"
   dotnet test --filter "FullyQualifiedName~OllamaHealth"
   ```

6. **Circuit state change events**: Verify events are raised when circuit transitions:
   - Closed -> Open (failures exceed threshold)
   - Open -> HalfOpen (break duration elapsed)
   - HalfOpen -> Closed (test request succeeds)
   - HalfOpen -> Open (test request fails)

---

## Testing Scenarios

### Unit Tests

```csharp
// tests/CompoundDocs.McpServer.Tests/Services/DeferredIndexingQueueTests.cs

[Fact]
public void Enqueue_WhenBelowCapacity_AddsItem()
{
    // Arrange
    var queue = CreateQueue(maxCapacity: 10);
    var item = CreateItem("test.md", FileChangeType.Modified);

    // Act
    var result = queue.Enqueue(item);

    // Assert
    Assert.True(result);
    Assert.Equal(1, queue.Count);
}

[Fact]
public void Enqueue_WhenAtCapacity_DropsOldestItem()
{
    // Arrange
    var queue = CreateQueue(maxCapacity: 2);
    var item1 = CreateItem("file1.md", FileChangeType.Created);
    var item2 = CreateItem("file2.md", FileChangeType.Created);
    var item3 = CreateItem("file3.md", FileChangeType.Created);

    // Act
    queue.Enqueue(item1);
    queue.Enqueue(item2);
    queue.Enqueue(item3); // Should drop item1

    // Assert
    Assert.Equal(2, queue.Count);
    Assert.True(queue.TryPeek(out var next));
    Assert.Equal("file2.md", next.RelativePath); // item1 was dropped
}

[Fact]
public void TryDequeue_WhenEmpty_ReturnsFalse()
{
    // Arrange
    var queue = CreateQueue(maxCapacity: 10);

    // Act
    var result = queue.TryDequeue(out var item);

    // Assert
    Assert.False(result);
    Assert.Null(item);
}
```

```csharp
// tests/CompoundDocs.McpServer.Tests/Services/OllamaHealthServiceTests.cs

[Fact]
public void GetStatus_WhenCircuitClosed_ReturnsAvailable()
{
    // Arrange
    var service = CreateHealthService();

    // Act
    var status = service.GetStatus();

    // Assert
    Assert.True(status.IsAvailable);
    Assert.Equal(CircuitState.Closed, status.CircuitState);
    Assert.Null(status.RetryAfterSeconds);
}

[Fact]
public void GetStatus_WhenCircuitOpen_ReturnsRetryAfter()
{
    // Arrange
    var service = CreateHealthService();
    service.OnCircuitStateChanged(CircuitState.Closed, CircuitState.Open);

    // Act
    var status = service.GetStatus();

    // Assert
    Assert.False(status.IsAvailable);
    Assert.Equal(CircuitState.Open, status.CircuitState);
    Assert.NotNull(status.RetryAfterSeconds);
    Assert.True(status.RetryAfterSeconds > 0);
}

[Fact]
public void CircuitStateChanged_RaisesEvent()
{
    // Arrange
    var service = CreateHealthService();
    CircuitStateChangedEventArgs? receivedArgs = null;
    service.CircuitStateChanged += (_, args) => receivedArgs = args;

    // Act
    service.OnCircuitStateChanged(CircuitState.Closed, CircuitState.Open);

    // Assert
    Assert.NotNull(receivedArgs);
    Assert.Equal(CircuitState.Closed, receivedArgs.PreviousState);
    Assert.Equal(CircuitState.Open, receivedArgs.NewState);
}
```

---

## Notes

- The deferred indexing queue is intentionally in-memory (not persisted) because startup reconciliation handles any missed changes
- Circuit breaker configuration (30s break duration, 50% failure ratio) is defined in Phase 030 and used here for status reporting
- The recovery processor uses a semaphore to prevent concurrent processing runs
- Apple Silicon detection is done once at startup for performance
- Consider adding metrics/telemetry for queue size and processing rates in future phases
- The 100ms processing delay helps avoid overwhelming Ollama immediately after recovery
