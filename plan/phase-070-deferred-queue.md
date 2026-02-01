# Phase 070: Deferred Indexing Queue

> **Status**: NOT_STARTED
> **Effort Estimate**: 6-8 hours
> **Category**: Document Processing
> **Prerequisites**: Phase 068 (Health Monitoring), Phase 034 (Embedding Service)

---

## Spec References

This phase implements deferred indexing functionality defined in:

- **spec/mcp-server/ollama-integration.md** - [Graceful Degradation - File Watcher](../spec/mcp-server/ollama-integration.md#graceful-degradation) (lines 183-208)
- **spec/mcp-server/file-watcher.md** - [Crash Recovery](../spec/mcp-server/file-watcher.md#crash-recovery) (lines 123-144)
- **spec/mcp-server/file-watcher.md** - [Error Handling - Processing Errors](../spec/mcp-server/file-watcher.md#error-handling) (lines 199-226)

---

## Objectives

1. Implement an in-memory queue for documents pending embedding generation
2. Queue documents automatically when Ollama is unavailable (circuit breaker open)
3. Process queued documents when Ollama recovers (circuit breaker closes)
4. Implement queue size limits with configurable overflow handling
5. Integrate with health monitoring service for Ollama availability detection
6. Ensure graceful handling of server restart (rely on reconciliation)

---

## Acceptance Criteria

### Queue Data Structures

- [ ] `DeferredDocument` record created with document path, content hash, and queue timestamp
- [ ] `IDeferredIndexingQueue` interface defined with enqueue, dequeue, and inspection methods
- [ ] `InMemoryDeferredIndexingQueue` implementation using `ConcurrentQueue<T>`
- [ ] Queue is singleton-scoped in DI container

### Queue Population

- [ ] File watcher enqueues documents when embedding generation fails due to Ollama unavailability
- [ ] Documents enqueued when circuit breaker is in `Open` state
- [ ] Documents enqueued when `EMBEDDING_SERVICE_ERROR` is returned
- [ ] Duplicate detection prevents same document from being queued multiple times
- [ ] Queue timestamp recorded for each enqueued document

### Queue Processing on Recovery

- [ ] `DeferredIndexingProcessor` background service monitors Ollama health
- [ ] Processing triggered when circuit breaker transitions from `Open` to `Closed`
- [ ] Processing triggered when health check detects Ollama availability after unavailability
- [ ] Documents processed in FIFO order (oldest first)
- [ ] Processing respects rate limiting to avoid overwhelming recovered Ollama
- [ ] Failed documents during processing are re-queued with exponential backoff

### Queue Size Limits

- [ ] Maximum queue size configurable (default: 1000 documents)
- [ ] Overflow strategy configurable: `DropOldest`, `DropNewest`, or `RejectNew`
- [ ] Warning logged when queue reaches 80% capacity
- [ ] Error logged when queue overflow occurs
- [ ] Queue depth exposed via metrics/health endpoint

### Persistence Considerations

- [ ] Queue is NOT persisted to disk (per spec: in-memory only)
- [ ] On server restart, queue is empty (reconciliation handles sync)
- [ ] Documentation clearly states reliance on startup reconciliation
- [ ] Queue state included in shutdown logging

---

## Implementation Notes

### 1. DeferredDocument Record

```csharp
// src/CompoundDocs.McpServer/Services/Queuing/DeferredDocument.cs
namespace CompoundDocs.McpServer.Services.Queuing;

/// <summary>
/// Represents a document that is waiting to be indexed when Ollama becomes available.
/// </summary>
/// <param name="FilePath">Absolute path to the document file.</param>
/// <param name="ContentHash">SHA256 hash of document content at time of queueing.</param>
/// <param name="QueuedAt">UTC timestamp when document was added to queue.</param>
/// <param name="RetryCount">Number of times indexing has been attempted for this document.</param>
public sealed record DeferredDocument(
    string FilePath,
    string ContentHash,
    DateTimeOffset QueuedAt,
    int RetryCount = 0)
{
    /// <summary>
    /// Creates a new instance with incremented retry count.
    /// </summary>
    public DeferredDocument WithRetry() =>
        this with { RetryCount = RetryCount + 1 };
}
```

### 2. IDeferredIndexingQueue Interface

```csharp
// src/CompoundDocs.McpServer/Services/Queuing/IDeferredIndexingQueue.cs
namespace CompoundDocs.McpServer.Services.Queuing;

/// <summary>
/// Queue for documents pending indexing due to embedding service unavailability.
/// </summary>
public interface IDeferredIndexingQueue
{
    /// <summary>
    /// Adds a document to the deferred indexing queue.
    /// </summary>
    /// <param name="document">The document to queue.</param>
    /// <returns>True if enqueued successfully, false if rejected (e.g., overflow).</returns>
    bool TryEnqueue(DeferredDocument document);

    /// <summary>
    /// Attempts to remove and return the next document from the queue.
    /// </summary>
    /// <param name="document">The dequeued document, if available.</param>
    /// <returns>True if a document was dequeued, false if queue is empty.</returns>
    bool TryDequeue(out DeferredDocument? document);

    /// <summary>
    /// Returns the document at the front of the queue without removing it.
    /// </summary>
    /// <param name="document">The peeked document, if available.</param>
    /// <returns>True if a document exists, false if queue is empty.</returns>
    bool TryPeek(out DeferredDocument? document);

    /// <summary>
    /// Checks if a document with the given path is already in the queue.
    /// </summary>
    bool Contains(string filePath);

    /// <summary>
    /// Removes a document from the queue by file path (e.g., if file was deleted).
    /// </summary>
    /// <returns>True if document was found and removed.</returns>
    bool TryRemove(string filePath);

    /// <summary>
    /// Gets the current number of documents in the queue.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Gets the maximum allowed queue size.
    /// </summary>
    int MaxSize { get; }

    /// <summary>
    /// Gets whether the queue is at or above capacity.
    /// </summary>
    bool IsFull { get; }

    /// <summary>
    /// Gets all queued documents (for diagnostics only).
    /// </summary>
    IReadOnlyList<DeferredDocument> GetSnapshot();

    /// <summary>
    /// Clears all documents from the queue.
    /// </summary>
    void Clear();
}
```

### 3. Queue Configuration Options

```csharp
// src/CompoundDocs.McpServer/Services/Queuing/DeferredIndexingOptions.cs
namespace CompoundDocs.McpServer.Services.Queuing;

/// <summary>
/// Configuration options for the deferred indexing queue.
/// </summary>
public class DeferredIndexingOptions
{
    public const string SectionName = "DeferredIndexing";

    /// <summary>
    /// Maximum number of documents that can be queued.
    /// Default: 1000
    /// </summary>
    public int MaxQueueSize { get; set; } = 1000;

    /// <summary>
    /// Strategy for handling queue overflow.
    /// Default: DropOldest
    /// </summary>
    public OverflowStrategy OverflowStrategy { get; set; } = OverflowStrategy.DropOldest;

    /// <summary>
    /// Percentage of queue capacity that triggers a warning log.
    /// Default: 80 (80%)
    /// </summary>
    public int WarningThresholdPercent { get; set; } = 80;

    /// <summary>
    /// Maximum retry attempts before dropping a document.
    /// Default: 5
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 5;

    /// <summary>
    /// Base delay for exponential backoff between retry attempts.
    /// Default: 30 seconds
    /// </summary>
    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Delay between processing batches to avoid overwhelming Ollama.
    /// Default: 1 second
    /// </summary>
    public TimeSpan ProcessingBatchDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Number of documents to process per batch during recovery.
    /// Default: 10
    /// </summary>
    public int ProcessingBatchSize { get; set; } = 10;

    /// <summary>
    /// Interval for checking Ollama availability when queue has items.
    /// Default: 10 seconds
    /// </summary>
    public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromSeconds(10);
}

/// <summary>
/// Strategy for handling queue overflow when at capacity.
/// </summary>
public enum OverflowStrategy
{
    /// <summary>
    /// Remove the oldest document to make room for new ones.
    /// </summary>
    DropOldest,

    /// <summary>
    /// Drop the newest incoming document (keep queue as-is).
    /// </summary>
    DropNewest,

    /// <summary>
    /// Reject new documents when queue is full (caller handles).
    /// </summary>
    RejectNew
}
```

### 4. InMemoryDeferredIndexingQueue Implementation

```csharp
// src/CompoundDocs.McpServer/Services/Queuing/InMemoryDeferredIndexingQueue.cs
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CompoundDocs.McpServer.Services.Queuing;

/// <summary>
/// In-memory implementation of the deferred indexing queue.
/// Thread-safe for concurrent access from file watcher and processor.
/// </summary>
public sealed class InMemoryDeferredIndexingQueue : IDeferredIndexingQueue
{
    private readonly ConcurrentQueue<DeferredDocument> _queue = new();
    private readonly ConcurrentDictionary<string, byte> _pathIndex = new(StringComparer.OrdinalIgnoreCase);
    private readonly DeferredIndexingOptions _options;
    private readonly ILogger<InMemoryDeferredIndexingQueue> _logger;
    private readonly object _overflowLock = new();
    private bool _warningLogged;

    public InMemoryDeferredIndexingQueue(
        IOptions<DeferredIndexingOptions> options,
        ILogger<InMemoryDeferredIndexingQueue> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public int Count => _queue.Count;
    public int MaxSize => _options.MaxQueueSize;
    public bool IsFull => Count >= MaxSize;

    public bool TryEnqueue(DeferredDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(document.FilePath);

        // Check for duplicates
        if (_pathIndex.ContainsKey(document.FilePath))
        {
            _logger.LogDebug(
                "Document {FilePath} already in deferred queue, skipping",
                document.FilePath);
            return true; // Already queued, consider success
        }

        lock (_overflowLock)
        {
            // Check capacity and handle overflow
            if (Count >= MaxSize)
            {
                return HandleOverflow(document);
            }

            // Check warning threshold
            var thresholdCount = (int)(MaxSize * (_options.WarningThresholdPercent / 100.0));
            if (Count >= thresholdCount && !_warningLogged)
            {
                _logger.LogWarning(
                    "Deferred indexing queue at {Percent}% capacity ({Count}/{Max})",
                    _options.WarningThresholdPercent,
                    Count,
                    MaxSize);
                _warningLogged = true;
            }

            // Enqueue the document
            _queue.Enqueue(document);
            _pathIndex.TryAdd(document.FilePath, 0);

            _logger.LogInformation(
                "Document {FilePath} added to deferred indexing queue (queue size: {Count})",
                document.FilePath,
                Count);

            return true;
        }
    }

    private bool HandleOverflow(DeferredDocument newDocument)
    {
        switch (_options.OverflowStrategy)
        {
            case OverflowStrategy.DropOldest:
                if (_queue.TryDequeue(out var dropped))
                {
                    _pathIndex.TryRemove(dropped.FilePath, out _);
                    _logger.LogWarning(
                        "Queue overflow: dropped oldest document {DroppedPath} to make room for {NewPath}",
                        dropped.FilePath,
                        newDocument.FilePath);

                    _queue.Enqueue(newDocument);
                    _pathIndex.TryAdd(newDocument.FilePath, 0);
                    return true;
                }
                return false;

            case OverflowStrategy.DropNewest:
                _logger.LogWarning(
                    "Queue overflow: rejecting new document {FilePath} (DropNewest strategy)",
                    newDocument.FilePath);
                return false;

            case OverflowStrategy.RejectNew:
            default:
                _logger.LogError(
                    "Queue overflow: cannot enqueue {FilePath}, queue is full ({Count}/{Max})",
                    newDocument.FilePath,
                    Count,
                    MaxSize);
                return false;
        }
    }

    public bool TryDequeue(out DeferredDocument? document)
    {
        if (_queue.TryDequeue(out document))
        {
            _pathIndex.TryRemove(document.FilePath, out _);

            // Reset warning flag if queue drops below threshold
            var thresholdCount = (int)(MaxSize * (_options.WarningThresholdPercent / 100.0));
            if (Count < thresholdCount)
            {
                _warningLogged = false;
            }

            return true;
        }

        document = null;
        return false;
    }

    public bool TryPeek(out DeferredDocument? document)
    {
        return _queue.TryPeek(out document);
    }

    public bool Contains(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        return _pathIndex.ContainsKey(filePath);
    }

    public bool TryRemove(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        // Note: ConcurrentQueue doesn't support removal by value.
        // We mark it as removed in the index; processor will skip it.
        if (_pathIndex.TryRemove(filePath, out _))
        {
            _logger.LogDebug(
                "Document {FilePath} marked for removal from deferred queue",
                filePath);
            return true;
        }
        return false;
    }

    public IReadOnlyList<DeferredDocument> GetSnapshot()
    {
        return _queue.ToArray();
    }

    public void Clear()
    {
        _queue.Clear();
        _pathIndex.Clear();
        _warningLogged = false;

        _logger.LogInformation("Deferred indexing queue cleared");
    }
}
```

### 5. DeferredIndexingProcessor Background Service

```csharp
// src/CompoundDocs.McpServer/Services/Queuing/DeferredIndexingProcessor.cs
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CompoundDocs.McpServer.Services.Queuing;

/// <summary>
/// Background service that processes the deferred indexing queue when Ollama becomes available.
/// </summary>
public sealed class DeferredIndexingProcessor : BackgroundService
{
    private readonly IDeferredIndexingQueue _queue;
    private readonly IOllamaHealthService _healthService;
    private readonly IDocumentIndexingService _indexingService;
    private readonly DeferredIndexingOptions _options;
    private readonly ILogger<DeferredIndexingProcessor> _logger;

    private bool _wasOllamaAvailable = true;
    private DateTimeOffset _lastProcessingAttempt = DateTimeOffset.MinValue;

    public DeferredIndexingProcessor(
        IDeferredIndexingQueue queue,
        IOllamaHealthService healthService,
        IDocumentIndexingService indexingService,
        IOptions<DeferredIndexingOptions> options,
        ILogger<DeferredIndexingProcessor> logger)
    {
        _queue = queue;
        _healthService = healthService;
        _indexingService = indexingService;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Deferred indexing processor started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessQueueIfOllamaAvailableAsync(stoppingToken);
                await Task.Delay(_options.HealthCheckInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in deferred indexing processor loop");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        LogShutdownState();
    }

    private async Task ProcessQueueIfOllamaAvailableAsync(CancellationToken ct)
    {
        // Check if queue has items
        if (_queue.Count == 0)
        {
            return;
        }

        // Check Ollama availability
        var isAvailable = await _healthService.IsAvailableAsync(ct);

        // Detect recovery (transition from unavailable to available)
        if (isAvailable && !_wasOllamaAvailable)
        {
            _logger.LogInformation(
                "Ollama recovered, starting deferred queue processing ({Count} documents)",
                _queue.Count);
        }

        _wasOllamaAvailable = isAvailable;

        if (!isAvailable)
        {
            _logger.LogDebug(
                "Ollama unavailable, deferring queue processing ({Count} documents pending)",
                _queue.Count);
            return;
        }

        // Process queue in batches
        await ProcessBatchAsync(ct);
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        var processed = 0;
        var failed = 0;
        var skipped = 0;

        while (processed < _options.ProcessingBatchSize && _queue.TryDequeue(out var document))
        {
            if (document == null)
            {
                continue;
            }

            // Skip if file no longer exists or was removed from path index
            if (!File.Exists(document.FilePath))
            {
                _logger.LogDebug(
                    "Skipping deferred document {FilePath}: file no longer exists",
                    document.FilePath);
                skipped++;
                continue;
            }

            // Check if content has changed since queueing
            var currentHash = await ComputeContentHashAsync(document.FilePath, ct);
            if (currentHash != document.ContentHash)
            {
                _logger.LogDebug(
                    "Skipping deferred document {FilePath}: content changed since queueing",
                    document.FilePath);
                skipped++;
                continue;
            }

            try
            {
                await _indexingService.IndexDocumentAsync(document.FilePath, ct);
                processed++;

                _logger.LogInformation(
                    "Successfully indexed deferred document {FilePath}",
                    document.FilePath);
            }
            catch (Exception ex) when (IsRetryableError(ex))
            {
                failed++;
                await HandleRetryAsync(document, ex);
            }
            catch (Exception ex)
            {
                failed++;
                _logger.LogError(ex,
                    "Non-retryable error indexing deferred document {FilePath}, dropping",
                    document.FilePath);
            }
        }

        if (processed > 0 || failed > 0 || skipped > 0)
        {
            _logger.LogInformation(
                "Deferred queue batch complete: {Processed} indexed, {Failed} failed, {Skipped} skipped, {Remaining} remaining",
                processed, failed, skipped, _queue.Count);
        }

        // Delay before next batch to avoid overwhelming Ollama
        if (_queue.Count > 0)
        {
            await Task.Delay(_options.ProcessingBatchDelay, ct);
        }
    }

    private async Task HandleRetryAsync(DeferredDocument document, Exception ex)
    {
        if (document.RetryCount >= _options.MaxRetryAttempts)
        {
            _logger.LogWarning(
                "Document {FilePath} exceeded max retry attempts ({Max}), dropping from queue",
                document.FilePath,
                _options.MaxRetryAttempts);
            return;
        }

        var updatedDocument = document.WithRetry();
        var backoffDelay = CalculateBackoffDelay(updatedDocument.RetryCount);

        _logger.LogWarning(ex,
            "Retryable error indexing {FilePath} (attempt {Attempt}/{Max}), re-queueing with {Delay}s delay",
            document.FilePath,
            updatedDocument.RetryCount,
            _options.MaxRetryAttempts,
            backoffDelay.TotalSeconds);

        // Re-queue with updated retry count
        // Note: Document will be processed again after other queued items
        _queue.TryEnqueue(updatedDocument);
    }

    private TimeSpan CalculateBackoffDelay(int retryCount)
    {
        // Exponential backoff: base * 2^(retry-1) with max cap
        var multiplier = Math.Pow(2, retryCount - 1);
        var delay = TimeSpan.FromTicks((long)(_options.RetryBaseDelay.Ticks * multiplier));

        // Cap at 5 minutes
        return delay > TimeSpan.FromMinutes(5) ? TimeSpan.FromMinutes(5) : delay;
    }

    private static bool IsRetryableError(Exception ex)
    {
        return ex is HttpRequestException or TaskCanceledException or TimeoutException;
    }

    private static async Task<string> ComputeContentHashAsync(string filePath, CancellationToken ct)
    {
        await using var stream = File.OpenRead(filePath);
        var hashBytes = await System.Security.Cryptography.SHA256.HashDataAsync(stream, ct);
        return Convert.ToHexString(hashBytes);
    }

    private void LogShutdownState()
    {
        if (_queue.Count > 0)
        {
            _logger.LogWarning(
                "Deferred indexing processor shutting down with {Count} documents still in queue. " +
                "These will be re-indexed on next startup via reconciliation.",
                _queue.Count);
        }
        else
        {
            _logger.LogInformation("Deferred indexing processor stopped (queue empty)");
        }
    }
}
```

### 6. File Watcher Integration

Update the file watcher to use the deferred queue:

```csharp
// Integration point in FileWatcherService
public class FileWatcherService
{
    private readonly IDeferredIndexingQueue _deferredQueue;
    private readonly IOllamaHealthService _healthService;

    // ... existing code ...

    private async Task ProcessFileChangeAsync(string filePath, CancellationToken ct)
    {
        try
        {
            // Check if Ollama is available before attempting indexing
            if (!await _healthService.IsAvailableAsync(ct))
            {
                await EnqueueForDeferredIndexingAsync(filePath, ct);
                return;
            }

            await _indexingService.IndexDocumentAsync(filePath, ct);
        }
        catch (EmbeddingServiceUnavailableException)
        {
            // Ollama became unavailable during indexing
            await EnqueueForDeferredIndexingAsync(filePath, ct);
        }
        catch (BrokenCircuitException)
        {
            // Circuit breaker is open
            await EnqueueForDeferredIndexingAsync(filePath, ct);
        }
    }

    private async Task EnqueueForDeferredIndexingAsync(string filePath, CancellationToken ct)
    {
        var contentHash = await ComputeContentHashAsync(filePath, ct);
        var deferredDoc = new DeferredDocument(
            FilePath: filePath,
            ContentHash: contentHash,
            QueuedAt: DateTimeOffset.UtcNow);

        if (_deferredQueue.TryEnqueue(deferredDoc))
        {
            _logger.LogInformation(
                "Document {FilePath} queued for deferred indexing (Ollama unavailable)",
                filePath);
        }
        else
        {
            _logger.LogError(
                "Failed to queue document {FilePath} for deferred indexing",
                filePath);
        }
    }
}
```

### 7. Service Registration

```csharp
// In ServiceCollectionExtensions.cs
public static IServiceCollection AddDeferredIndexingServices(
    this IServiceCollection services,
    IConfiguration configuration)
{
    // Bind configuration
    services.Configure<DeferredIndexingOptions>(
        configuration.GetSection(DeferredIndexingOptions.SectionName));

    // Register queue as singleton
    services.AddSingleton<IDeferredIndexingQueue, InMemoryDeferredIndexingQueue>();

    // Register background processor
    services.AddHostedService<DeferredIndexingProcessor>();

    return services;
}
```

### 8. Health/Metrics Exposure

```csharp
// Expose queue metrics for monitoring
public interface IDeferredIndexingMetrics
{
    int QueueDepth { get; }
    int MaxQueueSize { get; }
    double QueueUtilizationPercent { get; }
    DateTimeOffset? OldestDocumentQueuedAt { get; }
    int TotalEnqueued { get; }
    int TotalProcessed { get; }
    int TotalDropped { get; }
}
```

---

## Test Cases

### Unit Tests

```csharp
[Fact]
public void TryEnqueue_AddsDocumentToQueue()
{
    // Arrange
    var queue = CreateQueue();
    var doc = new DeferredDocument("/path/doc.md", "abc123", DateTimeOffset.UtcNow);

    // Act
    var result = queue.TryEnqueue(doc);

    // Assert
    Assert.True(result);
    Assert.Equal(1, queue.Count);
}

[Fact]
public void TryEnqueue_RejectsDuplicatePath()
{
    // Arrange
    var queue = CreateQueue();
    var doc1 = new DeferredDocument("/path/doc.md", "abc123", DateTimeOffset.UtcNow);
    var doc2 = new DeferredDocument("/path/doc.md", "def456", DateTimeOffset.UtcNow);

    // Act
    queue.TryEnqueue(doc1);
    var result = queue.TryEnqueue(doc2);

    // Assert
    Assert.True(result); // Returns true but doesn't add duplicate
    Assert.Equal(1, queue.Count);
}

[Fact]
public void TryEnqueue_HandlesOverflow_DropOldest()
{
    // Arrange
    var options = new DeferredIndexingOptions { MaxQueueSize = 2 };
    var queue = CreateQueue(options);
    var doc1 = new DeferredDocument("/path/doc1.md", "hash1", DateTimeOffset.UtcNow);
    var doc2 = new DeferredDocument("/path/doc2.md", "hash2", DateTimeOffset.UtcNow);
    var doc3 = new DeferredDocument("/path/doc3.md", "hash3", DateTimeOffset.UtcNow);

    // Act
    queue.TryEnqueue(doc1);
    queue.TryEnqueue(doc2);
    var result = queue.TryEnqueue(doc3);

    // Assert
    Assert.True(result);
    Assert.Equal(2, queue.Count);
    Assert.False(queue.Contains("/path/doc1.md")); // Oldest dropped
    Assert.True(queue.Contains("/path/doc3.md")); // New added
}

[Fact]
public void TryEnqueue_HandlesOverflow_RejectNew()
{
    // Arrange
    var options = new DeferredIndexingOptions
    {
        MaxQueueSize = 2,
        OverflowStrategy = OverflowStrategy.RejectNew
    };
    var queue = CreateQueue(options);

    // Act
    queue.TryEnqueue(new DeferredDocument("/doc1.md", "h1", DateTimeOffset.UtcNow));
    queue.TryEnqueue(new DeferredDocument("/doc2.md", "h2", DateTimeOffset.UtcNow));
    var result = queue.TryEnqueue(new DeferredDocument("/doc3.md", "h3", DateTimeOffset.UtcNow));

    // Assert
    Assert.False(result);
    Assert.Equal(2, queue.Count);
}

[Fact]
public void TryDequeue_ReturnsDocumentsInFifoOrder()
{
    // Arrange
    var queue = CreateQueue();
    var doc1 = new DeferredDocument("/doc1.md", "h1", DateTimeOffset.UtcNow);
    var doc2 = new DeferredDocument("/doc2.md", "h2", DateTimeOffset.UtcNow);
    queue.TryEnqueue(doc1);
    queue.TryEnqueue(doc2);

    // Act
    queue.TryDequeue(out var first);
    queue.TryDequeue(out var second);

    // Assert
    Assert.Equal("/doc1.md", first?.FilePath);
    Assert.Equal("/doc2.md", second?.FilePath);
}

[Fact]
public async Task Processor_ProcessesQueueWhenOllamaRecovers()
{
    // Arrange: Queue with documents, mock Ollama as unavailable then available
    // Act: Start processor, simulate Ollama recovery
    // Assert: Documents are indexed
}

[Fact]
public async Task Processor_RetriesFailedDocuments()
{
    // Arrange: Document that fails indexing with retryable error
    // Act: Process document
    // Assert: Document re-queued with incremented retry count
}

[Fact]
public async Task Processor_DropsDocumentAfterMaxRetries()
{
    // Arrange: Document at max retry count
    // Act: Indexing fails again
    // Assert: Document not re-queued
}
```

### Integration Tests

```csharp
[Fact]
public async Task FileWatcher_QueuesDocument_WhenOllamaUnavailable()
{
    // Arrange: File watcher with Ollama circuit breaker open
    // Act: Create a new document file
    // Assert: Document added to deferred queue
}

[Fact]
public async Task EndToEnd_QueuedDocuments_IndexedOnRecovery()
{
    // Arrange: Stop Ollama, create documents, queue them
    // Act: Start Ollama
    // Assert: All queued documents eventually indexed
}
```

---

## Dependencies

### Depends On

- **Phase 068**: Health Monitoring - `IOllamaHealthService` for availability detection
- **Phase 034**: Embedding Service - Document indexing functionality
- **Phase 030**: Resilience Patterns - Circuit breaker integration

### Blocks

- Health check endpoints (queue metrics exposure)
- Monitoring dashboard (queue depth metrics)

---

## Verification Steps

After completing this phase, verify:

1. **Queue population**: Create documents while Ollama is stopped; confirm they are queued
2. **Duplicate prevention**: Try to queue same document twice; confirm only one entry
3. **Overflow handling**: Fill queue to capacity; confirm overflow strategy works correctly
4. **Recovery processing**: Start Ollama; confirm queued documents are indexed
5. **Retry logic**: Simulate transient failures; confirm exponential backoff
6. **Max retries**: Exceed max attempts; confirm document is dropped
7. **Shutdown logging**: Stop server with items in queue; confirm warning logged
8. **Reconciliation**: Restart server; confirm previously queued items are handled by reconciliation

---

## Notes

- The queue is intentionally NOT persisted to disk per spec. Startup reconciliation handles any missed updates.
- The `ConcurrentDictionary` path index provides O(1) duplicate detection at the cost of memory.
- The `TryRemove` method marks documents for removal; they are skipped during processing rather than immediately removed from the `ConcurrentQueue`.
- Consider adding telemetry/metrics in a future phase for production monitoring of queue depth trends.
- The batch processing with delays ensures Ollama is not overwhelmed during recovery, which is especially important on resource-constrained hardware.
