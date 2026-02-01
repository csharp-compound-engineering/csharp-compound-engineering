# Phase 040: Concurrency Model (Last-Write-Wins)

> **Status**: NOT_STARTED
> **Effort Estimate**: 4-6 hours
> **Category**: MCP Server Core
> **Prerequisites**: Phase 021 (MCP Server Skeleton)

---

## Spec References

This phase implements the concurrency control strategy defined in:

- **spec/mcp-server.md** - Concurrency Control section
- **spec/mcp-server/file-watcher.md** - Concurrency Control details and rationale

---

## Objectives

1. Implement last-write-wins concurrency strategy across all document operations
2. Establish the file system as the single source of truth
3. Ensure no OS-level file locking is used for document access
4. Define conflict resolution behavior for concurrent modifications
5. Implement thread-safe access patterns for shared resources
6. Create synchronization primitives for background service coordination

---

## Acceptance Criteria

### Last-Write-Wins Strategy

- [ ] No optimistic locking (version checks or ETags) on document operations
- [ ] File system content always takes precedence over database state
- [ ] Concurrent writes to the same document result in last writer's content being persisted
- [ ] File watcher reconciliation corrects any drift between disk and database
- [ ] Document chunks are always regenerated from current file content (never merged)

### No OS-Level File Locking

- [ ] `FileStream` operations use `FileShare.ReadWrite` to allow concurrent access
- [ ] No `FileStream.Lock()` or `FileStream.Unlock()` calls
- [ ] No exclusive file handles held beyond immediate read/write operations
- [ ] External processes (editors, IDEs) can freely access files while MCP server runs
- [ ] File reading uses defensive patterns for files modified mid-read

### Thread Safety for Shared Resources

- [ ] `ConcurrentDictionary<string, T>` used for in-memory document state tracking
- [ ] `Channel<T>` used for file watcher event queue (producer-consumer pattern)
- [ ] `SemaphoreSlim` used for throttling concurrent embedding operations
- [ ] `ReaderWriterLockSlim` used for link graph access (read-heavy workload)
- [ ] All service interfaces document their thread safety guarantees

### Background Service Coordination

- [ ] File watcher service and reconciliation service coordinate via shared state
- [ ] Cancellation tokens properly propagated through all async operations
- [ ] Graceful shutdown ensures in-flight operations complete before termination
- [ ] `IHostApplicationLifetime` used for coordinated startup/shutdown

### Conflict Resolution Behavior

- [ ] Documented behavior for simultaneous file changes from multiple sources
- [ ] Debounce window consolidates rapid changes (500ms default)
- [ ] Content hash comparison detects actual changes vs. timestamp-only updates
- [ ] Chunk regeneration is atomic (all-or-nothing per document)

---

## Implementation Notes

### File Access Pattern

All file operations should use non-exclusive access to allow external tools:

```csharp
/// <summary>
/// Reads file content using shared access mode.
/// Allows external processes to read/write the file simultaneously.
/// </summary>
public async Task<string> ReadFileContentAsync(string filePath, CancellationToken ct)
{
    // Use FileShare.ReadWrite to avoid blocking external editors
    await using var stream = new FileStream(
        filePath,
        FileMode.Open,
        FileAccess.Read,
        FileShare.ReadWrite,  // Critical: allows concurrent access
        bufferSize: 4096,
        useAsync: true);

    using var reader = new StreamReader(stream, Encoding.UTF8);
    return await reader.ReadToEndAsync(ct);
}
```

### Defensive Reading for Modified Files

Handle scenarios where a file is modified during read:

```csharp
/// <summary>
/// Reads file with retry logic for concurrent modification scenarios.
/// </summary>
public async Task<FileReadResult> ReadFileDefensivelyAsync(
    string filePath,
    CancellationToken ct)
{
    const int maxRetries = 3;
    const int retryDelayMs = 50;

    for (int attempt = 0; attempt < maxRetries; attempt++)
    {
        try
        {
            var content = await ReadFileContentAsync(filePath, ct);
            var hash = ComputeContentHash(content);

            return new FileReadResult(content, hash, Success: true);
        }
        catch (IOException ex) when (attempt < maxRetries - 1)
        {
            // File may be in use by another process - brief retry
            _logger.LogDebug(
                ex,
                "File read attempt {Attempt} failed for {Path}, retrying",
                attempt + 1,
                filePath);

            await Task.Delay(retryDelayMs, ct);
        }
    }

    return new FileReadResult(string.Empty, string.Empty, Success: false);
}

public record FileReadResult(string Content, string ContentHash, bool Success);
```

### Event Queue with Channel

Use `System.Threading.Channels` for the file watcher event queue:

```csharp
/// <summary>
/// Thread-safe event queue for file system changes.
/// Bounded channel provides back-pressure when processing can't keep up.
/// </summary>
public class FileEventQueue : IDisposable
{
    private readonly Channel<FileChangeEvent> _channel;
    private readonly ILogger<FileEventQueue> _logger;

    public FileEventQueue(ILogger<FileEventQueue> logger)
    {
        _logger = logger;

        // Bounded channel with 1000 capacity
        // If full, oldest events are dropped (acceptable due to reconciliation)
        _channel = Channel.CreateBounded<FileChangeEvent>(
            new BoundedChannelOptions(1000)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,   // Only reconciliation service reads
                SingleWriter = false   // FileSystemWatcher may fire from multiple threads
            });
    }

    public ValueTask EnqueueAsync(FileChangeEvent evt, CancellationToken ct)
    {
        return _channel.Writer.WriteAsync(evt, ct);
    }

    public IAsyncEnumerable<FileChangeEvent> ReadAllAsync(CancellationToken ct)
    {
        return _channel.Reader.ReadAllAsync(ct);
    }

    public void Complete() => _channel.Writer.Complete();

    public void Dispose() => Complete();
}

public record FileChangeEvent(
    string FilePath,
    FileChangeType ChangeType,
    DateTime Timestamp);

public enum FileChangeType
{
    Created,
    Modified,
    Deleted,
    Renamed
}
```

### Debounce Implementation

Consolidate rapid file changes within the debounce window:

```csharp
/// <summary>
/// Debounces file change events to prevent excessive processing.
/// Only the final event within the debounce window is processed.
/// </summary>
public class FileChangeDebouncer : IDisposable
{
    private readonly ConcurrentDictionary<string, DebouncedChange> _pendingChanges = new();
    private readonly TimeSpan _debounceInterval;
    private readonly Func<FileChangeEvent, CancellationToken, Task> _processAction;
    private readonly ILogger<FileChangeDebouncer> _logger;
    private readonly CancellationTokenSource _cts = new();

    public FileChangeDebouncer(
        TimeSpan debounceInterval,
        Func<FileChangeEvent, CancellationToken, Task> processAction,
        ILogger<FileChangeDebouncer> logger)
    {
        _debounceInterval = debounceInterval;
        _processAction = processAction;
        _logger = logger;
    }

    public void OnFileChanged(FileChangeEvent evt)
    {
        var change = _pendingChanges.AddOrUpdate(
            evt.FilePath,
            path => new DebouncedChange(evt, CreateDebounceTimer(path)),
            (path, existing) =>
            {
                // Cancel existing timer and create new one
                existing.Timer.Dispose();
                return new DebouncedChange(evt, CreateDebounceTimer(path));
            });

        _logger.LogDebug(
            "Debouncing {ChangeType} for {Path}",
            evt.ChangeType,
            evt.FilePath);
    }

    private Timer CreateDebounceTimer(string filePath)
    {
        return new Timer(
            async _ => await OnDebounceElapsedAsync(filePath),
            state: null,
            dueTime: _debounceInterval,
            period: Timeout.InfiniteTimeSpan);
    }

    private async Task OnDebounceElapsedAsync(string filePath)
    {
        if (_pendingChanges.TryRemove(filePath, out var change))
        {
            try
            {
                await _processAction(change.Event, _cts.Token);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error processing debounced change for {Path}", filePath);
            }
            finally
            {
                change.Timer.Dispose();
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        foreach (var change in _pendingChanges.Values)
        {
            change.Timer.Dispose();
        }
        _pendingChanges.Clear();
        _cts.Dispose();
    }

    private record DebouncedChange(FileChangeEvent Event, Timer Timer);
}
```

### Embedding Operation Throttling

Limit concurrent embedding operations to avoid overwhelming Ollama:

```csharp
/// <summary>
/// Throttles concurrent embedding operations using a semaphore.
/// </summary>
public class ThrottledEmbeddingService : IEmbeddingService
{
    private readonly IEmbeddingService _inner;
    private readonly SemaphoreSlim _throttle;
    private readonly ILogger<ThrottledEmbeddingService> _logger;

    public ThrottledEmbeddingService(
        IEmbeddingService inner,
        int maxConcurrentEmbeddings,
        ILogger<ThrottledEmbeddingService> logger)
    {
        _inner = inner;
        _throttle = new SemaphoreSlim(maxConcurrentEmbeddings);
        _logger = logger;
    }

    public async Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(
        string content,
        CancellationToken ct = default)
    {
        await _throttle.WaitAsync(ct);
        try
        {
            _logger.LogDebug(
                "Generating embedding, {Available}/{Total} slots available",
                _throttle.CurrentCount,
                _throttle.CurrentCount + 1);

            return await _inner.GenerateEmbeddingAsync(content, ct);
        }
        finally
        {
            _throttle.Release();
        }
    }
}
```

### Link Graph Thread Safety

Use `ReaderWriterLockSlim` for the in-memory link graph (read-heavy workload):

```csharp
/// <summary>
/// Thread-safe wrapper for the document link graph.
/// Optimized for read-heavy access patterns.
/// </summary>
public class ThreadSafeLinkGraph : IDisposable
{
    private readonly AdjacencyGraph<string, Edge<string>> _graph = new();
    private readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.NoRecursion);

    public void AddDocument(string documentPath)
    {
        _lock.EnterWriteLock();
        try
        {
            _graph.AddVertex(documentPath);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void AddLink(string sourceDocument, string targetDocument)
    {
        _lock.EnterWriteLock();
        try
        {
            _graph.AddVerticesAndEdge(new Edge<string>(sourceDocument, targetDocument));
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void RemoveDocument(string documentPath)
    {
        _lock.EnterWriteLock();
        try
        {
            _graph.RemoveVertex(documentPath);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public IReadOnlyList<string> GetLinkedDocuments(string documentPath, int maxDepth)
    {
        _lock.EnterReadLock();
        try
        {
            var visited = new HashSet<string>();
            var result = new List<string>();
            TraverseLinks(documentPath, maxDepth, 0, visited, result);
            return result;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public bool HasCircularReference(string documentPath)
    {
        _lock.EnterReadLock();
        try
        {
            // Use DFS to detect cycles involving this document
            return DetectCycle(documentPath);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    private void TraverseLinks(
        string current,
        int maxDepth,
        int currentDepth,
        HashSet<string> visited,
        List<string> result)
    {
        if (currentDepth > maxDepth || !visited.Add(current))
            return;

        if (_graph.TryGetOutEdges(current, out var edges))
        {
            foreach (var edge in edges)
            {
                result.Add(edge.Target);
                TraverseLinks(edge.Target, maxDepth, currentDepth + 1, visited, result);
            }
        }
    }

    private bool DetectCycle(string startVertex)
    {
        // Standard DFS cycle detection
        var visiting = new HashSet<string>();
        var visited = new HashSet<string>();

        return DfsCycleCheck(startVertex, visiting, visited);
    }

    private bool DfsCycleCheck(
        string vertex,
        HashSet<string> visiting,
        HashSet<string> visited)
    {
        if (visiting.Contains(vertex))
            return true;  // Cycle detected

        if (visited.Contains(vertex))
            return false;  // Already fully explored

        visiting.Add(vertex);

        if (_graph.TryGetOutEdges(vertex, out var edges))
        {
            foreach (var edge in edges)
            {
                if (DfsCycleCheck(edge.Target, visiting, visited))
                    return true;
            }
        }

        visiting.Remove(vertex);
        visited.Add(vertex);
        return false;
    }

    public void Dispose()
    {
        _lock.Dispose();
    }
}
```

### Document State Tracking

Track in-memory state for active documents:

```csharp
/// <summary>
/// Thread-safe tracking of document processing state.
/// </summary>
public class DocumentStateTracker
{
    private readonly ConcurrentDictionary<string, DocumentState> _states = new();
    private readonly ILogger<DocumentStateTracker> _logger;

    public DocumentStateTracker(ILogger<DocumentStateTracker> logger)
    {
        _logger = logger;
    }

    public DocumentState GetOrCreateState(string documentPath)
    {
        return _states.GetOrAdd(documentPath, path => new DocumentState
        {
            Path = path,
            Status = ProcessingStatus.Pending,
            LastModified = DateTime.UtcNow
        });
    }

    public bool TryMarkProcessing(string documentPath)
    {
        if (_states.TryGetValue(documentPath, out var state))
        {
            var expected = ProcessingStatus.Pending;
            return Interlocked.CompareExchange(
                ref state.StatusInt,
                (int)ProcessingStatus.Processing,
                (int)expected) == (int)expected;
        }
        return false;
    }

    public void MarkComplete(string documentPath, string contentHash)
    {
        if (_states.TryGetValue(documentPath, out var state))
        {
            state.ContentHash = contentHash;
            state.LastProcessed = DateTime.UtcNow;
            Interlocked.Exchange(ref state.StatusInt, (int)ProcessingStatus.Complete);
        }
    }

    public void MarkError(string documentPath, string errorMessage)
    {
        if (_states.TryGetValue(documentPath, out var state))
        {
            state.LastError = errorMessage;
            state.LastErrorTime = DateTime.UtcNow;
            Interlocked.Exchange(ref state.StatusInt, (int)ProcessingStatus.Error);
        }
    }

    public void Remove(string documentPath)
    {
        _states.TryRemove(documentPath, out _);
    }

    public IReadOnlyList<DocumentState> GetPendingDocuments()
    {
        return _states.Values
            .Where(s => s.Status == ProcessingStatus.Pending)
            .ToList();
    }
}

public class DocumentState
{
    public required string Path { get; init; }
    internal int StatusInt;
    public ProcessingStatus Status
    {
        get => (ProcessingStatus)StatusInt;
        set => StatusInt = (int)value;
    }
    public string? ContentHash { get; set; }
    public DateTime LastModified { get; set; }
    public DateTime? LastProcessed { get; set; }
    public string? LastError { get; set; }
    public DateTime? LastErrorTime { get; set; }
}

public enum ProcessingStatus
{
    Pending,
    Processing,
    Complete,
    Error
}
```

### Graceful Shutdown Coordination

Coordinate service shutdown using `IHostApplicationLifetime`:

```csharp
/// <summary>
/// Coordinates graceful shutdown of background services.
/// </summary>
public class ShutdownCoordinator : IHostedService
{
    private readonly IHostApplicationLifetime _lifetime;
    private readonly FileEventQueue _eventQueue;
    private readonly DocumentStateTracker _stateTracker;
    private readonly ILogger<ShutdownCoordinator> _logger;

    public ShutdownCoordinator(
        IHostApplicationLifetime lifetime,
        FileEventQueue eventQueue,
        DocumentStateTracker stateTracker,
        ILogger<ShutdownCoordinator> logger)
    {
        _lifetime = lifetime;
        _eventQueue = eventQueue;
        _stateTracker = stateTracker;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken ct)
    {
        _lifetime.ApplicationStopping.Register(OnApplicationStopping);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct)
    {
        return Task.CompletedTask;
    }

    private void OnApplicationStopping()
    {
        _logger.LogInformation("Application stopping, completing event queue");

        // Signal no more events will be produced
        _eventQueue.Complete();

        // Log any documents still being processed
        var pending = _stateTracker.GetPendingDocuments();
        if (pending.Count > 0)
        {
            _logger.LogWarning(
                "{Count} documents were pending when shutdown initiated",
                pending.Count);
        }
    }
}
```

### Service Registration

Register concurrency-related services in DI:

```csharp
public static class ConcurrencyServiceExtensions
{
    public static IServiceCollection AddConcurrencyServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = configuration
            .GetSection(FileWatcherOptions.SectionName)
            .Get<FileWatcherOptions>() ?? new FileWatcherOptions();

        // Event queue - singleton, shared across services
        services.AddSingleton<FileEventQueue>();

        // State tracker - singleton, shared across services
        services.AddSingleton<DocumentStateTracker>();

        // Link graph - singleton, shared across services
        services.AddSingleton<ThreadSafeLinkGraph>();

        // Debouncer - configured with options
        services.AddSingleton<FileChangeDebouncer>(sp =>
        {
            var queue = sp.GetRequiredService<FileEventQueue>();
            var logger = sp.GetRequiredService<ILogger<FileChangeDebouncer>>();

            return new FileChangeDebouncer(
                TimeSpan.FromMilliseconds(options.DebounceMilliseconds),
                async (evt, ct) => await queue.EnqueueAsync(evt, ct),
                logger);
        });

        // Throttled embedding service decorator
        services.Decorate<IEmbeddingService>((inner, sp) =>
        {
            var logger = sp.GetRequiredService<ILogger<ThrottledEmbeddingService>>();
            return new ThrottledEmbeddingService(
                inner,
                maxConcurrentEmbeddings: 3,  // Configurable
                logger);
        });

        // Shutdown coordinator
        services.AddHostedService<ShutdownCoordinator>();

        return services;
    }
}
```

---

## Thread Safety Documentation

Document thread safety guarantees for all service interfaces:

```csharp
/// <summary>
/// Generates embeddings from text content.
/// </summary>
/// <remarks>
/// Thread Safety: This interface must be implemented in a thread-safe manner.
/// Multiple concurrent calls to GenerateEmbeddingAsync are expected.
/// Implementations should handle internal connection pooling and throttling.
/// </remarks>
public interface IEmbeddingService
{
    Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(
        string content,
        CancellationToken ct = default);
}

/// <summary>
/// Repository for document CRUD operations.
/// </summary>
/// <remarks>
/// Thread Safety: Implementations must be thread-safe for concurrent operations
/// on different documents. Operations on the same document follow last-write-wins
/// semantics without locking.
/// </remarks>
public interface IDocumentRepository
{
    // ... methods
}

/// <summary>
/// Watches file system for document changes.
/// </summary>
/// <remarks>
/// Thread Safety: FileSystemWatcher events may fire from multiple threads
/// concurrently. Implementations must handle this via thread-safe queuing
/// and debouncing mechanisms.
/// </remarks>
public interface IFileWatcherService
{
    // ... methods
}
```

---

## Dependencies

### Depends On
- Phase 021: MCP Server Skeleton (host builder, service registration patterns)

### Blocks
- Phase 041+: Any phase requiring file watching or document synchronization
- File watcher service implementation
- Reconciliation service implementation
- All MCP tool implementations that modify documents

---

## Verification Steps

After completing this phase, verify:

1. **File access is non-exclusive**: External editors can modify files while MCP server runs
2. **Debouncing works**: Rapid file saves result in single processing operation
3. **Thread safety**: No race conditions under concurrent load
4. **Graceful shutdown**: In-flight operations complete, no data loss
5. **Last-write-wins behavior**: Concurrent modifications resolve to final disk state

### Manual Verification

```bash
# Test 1: Non-exclusive file access
# Start MCP server, then open a tracked document in VS Code
# Edit and save multiple times - should not show file-in-use errors

# Test 2: Debouncing
# Rapidly save a file 10 times in quick succession
# Check logs - should see single "Processing change" after debounce

# Test 3: Concurrent modifications
# Create a script that modifies same file from multiple processes
# Final database state should match final file content
```

### Unit Test Verification

```csharp
[Fact]
public async Task Debouncer_ConsolidatesRapidChanges()
{
    // Arrange
    var processedEvents = new List<FileChangeEvent>();
    var debouncer = new FileChangeDebouncer(
        TimeSpan.FromMilliseconds(100),
        (evt, ct) => { processedEvents.Add(evt); return Task.CompletedTask; },
        NullLogger<FileChangeDebouncer>.Instance);

    // Act - fire 5 events in rapid succession
    for (int i = 0; i < 5; i++)
    {
        debouncer.OnFileChanged(new FileChangeEvent(
            "/test/file.md",
            FileChangeType.Modified,
            DateTime.UtcNow));
        await Task.Delay(20);  // Less than debounce interval
    }

    // Wait for debounce to complete
    await Task.Delay(200);

    // Assert - only one event processed
    Assert.Single(processedEvents);
}

[Fact]
public async Task FileEventQueue_HandlesMultipleProducers()
{
    // Arrange
    var queue = new FileEventQueue(NullLogger<FileEventQueue>.Instance);
    var producerCount = 10;
    var eventsPerProducer = 100;
    var receivedEvents = new ConcurrentBag<FileChangeEvent>();

    // Act - multiple producers, single consumer
    var producerTasks = Enumerable.Range(0, producerCount)
        .Select(async producerId =>
        {
            for (int i = 0; i < eventsPerProducer; i++)
            {
                await queue.EnqueueAsync(
                    new FileChangeEvent($"/test/{producerId}/{i}.md", FileChangeType.Created, DateTime.UtcNow),
                    CancellationToken.None);
            }
        });

    var consumerTask = Task.Run(async () =>
    {
        await foreach (var evt in queue.ReadAllAsync(CancellationToken.None))
        {
            receivedEvents.Add(evt);
            if (receivedEvents.Count >= producerCount * eventsPerProducer)
                break;
        }
    });

    await Task.WhenAll(producerTasks);
    queue.Complete();
    await consumerTask;

    // Assert
    Assert.Equal(producerCount * eventsPerProducer, receivedEvents.Count);
}

[Fact]
public void ThreadSafeLinkGraph_HandlesConccurrentAccess()
{
    // Arrange
    var graph = new ThreadSafeLinkGraph();
    var documentCount = 100;

    // Act - concurrent writes and reads
    Parallel.For(0, documentCount, i =>
    {
        var docPath = $"/docs/doc{i}.md";
        graph.AddDocument(docPath);

        if (i > 0)
        {
            graph.AddLink($"/docs/doc{i-1}.md", docPath);
        }

        // Concurrent read while writing
        var linked = graph.GetLinkedDocuments("/docs/doc0.md", 5);
    });

    // Assert - no exceptions, graph is consistent
    var links = graph.GetLinkedDocuments("/docs/doc0.md", documentCount);
    Assert.True(links.Count > 0);
}
```

---

## Notes

- Last-write-wins is explicitly chosen over optimistic locking due to chunking complexity and single-user typical usage patterns
- The file system is always authoritative; the database is a derived index that can be rebuilt
- Reconciliation on activation catches any drift, making persistent change queues unnecessary
- Channel-based event queues with bounded capacity provide back-pressure while tolerating event loss (reconciliation recovers)
- Future enhancement: If multi-user concurrent editing becomes common, optimistic locking could be added at the file level (not chunk level) using ContentHash
