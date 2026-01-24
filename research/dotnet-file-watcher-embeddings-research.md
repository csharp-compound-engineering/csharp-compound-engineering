# .NET FileSystemWatcher for RAG Embedding Synchronization

A comprehensive research report on using .NET's FileSystemWatcher to monitor document directories and trigger embedding operations for RAG (Retrieval-Augmented Generation) systems.

---

## Table of Contents

1. [FileSystemWatcher Overview](#1-filesystemwatcher-overview)
2. [Recursive Directory Watching](#2-recursive-directory-watching)
3. [Event Handling](#3-event-handling)
4. [Common Issues and Solutions](#4-common-issues-and-solutions)
5. [Debouncing Implementation](#5-debouncing-implementation)
6. [Background Service Pattern](#6-background-service-pattern)
7. [Processing Queue Architecture](#7-processing-queue-architecture)
8. [Embedding Operations Mapping](#8-embedding-operations-mapping)
9. [File Reading Considerations](#9-file-reading-considerations)
10. [Error Handling and Resilience](#10-error-handling-and-resilience)
11. [Initial Synchronization](#11-initial-synchronization)
12. [Complete Code Examples](#12-complete-code-examples)
13. [Alternative: Polling Approach](#13-alternative-polling-approach)
14. [Performance and Scalability](#14-performance-and-scalability)
15. [Sources](#15-sources)

---

## 1. FileSystemWatcher Overview

### What is FileSystemWatcher?

`FileSystemWatcher` is a class in the `System.IO` namespace that listens to file system change notifications and raises events when a directory or file changes. It enables real-time monitoring of file system changes without polling.

**Namespace:** `System.IO`
**Available in:** .NET Framework 1.1+, .NET Core, .NET 5+

### Supported Platforms

| Platform | Backend API | Notes |
|----------|-------------|-------|
| **Windows** | ReadDirectoryChanges API | Full support, most reliable |
| **Linux** | inotify | Some behavioral differences, system limits apply |
| **macOS** | FSEvents | Performance issues on startup, some behavioral differences |

### Key Properties

| Property | Type | Description |
|----------|------|-------------|
| `Path` | string | Directory to watch |
| `Filter` | string | File filter pattern (e.g., "*.txt", "*.*") |
| `Filters` | Collection | Multiple filter patterns |
| `NotifyFilter` | NotifyFilters | Types of changes to watch |
| `IncludeSubdirectories` | bool | Watch subdirectories recursively |
| `EnableRaisingEvents` | bool | Enable/disable event raising |
| `InternalBufferSize` | int | Internal buffer size (default: 8KB, max: 64KB for network) |

### NotifyFilters Enumeration

```csharp
NotifyFilters.Attributes       // File attributes
NotifyFilters.CreationTime     // Creation time
NotifyFilters.DirectoryName    // Directory name changes
NotifyFilters.FileName         // File name changes
NotifyFilters.LastAccess       // Last access time
NotifyFilters.LastWrite        // Last write time
NotifyFilters.Security         // Security settings
NotifyFilters.Size             // File size
```

### Events

| Event | EventArgs | When Raised |
|-------|-----------|-------------|
| `Created` | FileSystemEventArgs | File or directory created |
| `Changed` | FileSystemEventArgs | File or directory modified |
| `Deleted` | FileSystemEventArgs | File or directory deleted |
| `Renamed` | RenamedEventArgs | File or directory renamed |
| `Error` | ErrorEventArgs | Watcher encounters an error or buffer overflow |

### Limitations and Known Issues

1. **Buffer overflow**: If many changes occur quickly, events may be lost
2. **Duplicate events**: A single file operation can trigger multiple events
3. **Platform differences**: Behavior varies between Windows, Linux, and macOS
4. **File locking**: Events fire before files are fully written
5. **Network drives**: Maximum buffer size is 64KB
6. **Long file names**: May be reported in 8.3 format on Windows

---

## 2. Recursive Directory Watching

### Enabling Recursive Watching

```csharp
var watcher = new FileSystemWatcher
{
    Path = @"/path/to/documents",
    IncludeSubdirectories = true,  // Enable recursive watching
    EnableRaisingEvents = true
};
```

### Performance Implications

- **Memory usage**: Each watched directory consumes resources
- **Buffer pressure**: More directories = more events = higher buffer usage
- **Linux limits**: `fs.inotify.max_user_watches` limits the number of watches
- **macOS startup**: Creating FileSystemWatcher can be slow due to `Sync()` calls

### Large Directory Considerations

1. Increase `InternalBufferSize` (up to 64KB)
2. Use targeted `NotifyFilter` settings
3. Consider watching fewer subdirectories
4. Implement your own event buffering
5. Use polling for very large directory structures

### Filter Patterns

```csharp
// Single filter
watcher.Filter = "*.md";

// Multiple filters (.NET 5+)
watcher.Filters.Add("*.txt");
watcher.Filters.Add("*.md");
watcher.Filters.Add("*.pdf");
watcher.Filters.Add("*.docx");

// All files
watcher.Filter = "*.*";  // or ""
```

---

## 3. Event Handling

### Basic Event Setup

```csharp
var watcher = new FileSystemWatcher(@"/path/to/watch");

watcher.Created += OnCreated;
watcher.Changed += OnChanged;
watcher.Deleted += OnDeleted;
watcher.Renamed += OnRenamed;
watcher.Error += OnError;

watcher.NotifyFilter = NotifyFilters.FileName
                     | NotifyFilters.LastWrite
                     | NotifyFilters.Size;

watcher.IncludeSubdirectories = true;
watcher.EnableRaisingEvents = true;
```

### Created Event - New File Added

```csharp
private void OnCreated(object sender, FileSystemEventArgs e)
{
    Console.WriteLine($"Created: {e.FullPath}");
    // Action: Generate embedding, upsert to vector store
}
```

### Changed Event - File Modified

```csharp
private void OnChanged(object sender, FileSystemEventArgs e)
{
    // Filter to only handle actual content changes
    if (e.ChangeType != WatcherChangeTypes.Changed)
        return;

    Console.WriteLine($"Changed: {e.FullPath}");
    // Action: Regenerate embedding, update in vector store
}
```

### Deleted Event - File Removed

```csharp
private void OnDeleted(object sender, FileSystemEventArgs e)
{
    Console.WriteLine($"Deleted: {e.FullPath}");
    // Action: Remove from vector store by file path/ID
}
```

### Renamed Event - File Renamed

```csharp
private void OnRenamed(object sender, RenamedEventArgs e)
{
    Console.WriteLine($"Renamed: {e.OldFullPath} -> {e.FullPath}");
    // Action: Update metadata in vector store or delete + create
}
```

### Error Event - Handle Failures

```csharp
private void OnError(object sender, ErrorEventArgs e)
{
    var exception = e.GetException();

    if (exception is InternalBufferOverflowException)
    {
        // Buffer overflow - consider full resync
        Console.WriteLine("Buffer overflow! Some events may have been lost.");
    }
    else
    {
        Console.WriteLine($"Error: {exception.Message}");
    }
}
```

### Event Arguments Properties

**FileSystemEventArgs:**
- `Name`: File name
- `FullPath`: Complete file path
- `ChangeType`: Type of change (Created, Changed, Deleted)

**RenamedEventArgs** (extends FileSystemEventArgs):
- `OldName`: Previous file name
- `OldFullPath`: Previous complete path

---

## 4. Common Issues and Solutions

### Issue 1: Duplicate Events (Changed Fires Multiple Times)

Many applications update files in stages, causing multiple Changed events for a single save operation.

**Solutions:**
1. Timer-based debouncing
2. Track last processed timestamp per file
3. Use MemoryCache to deduplicate
4. Queue events and process after quiet period

### Issue 2: Buffer Overflow

The internal buffer can overflow when many changes occur quickly.

**Solutions:**
```csharp
// Increase buffer size (multiples of 4KB, max 64KB)
watcher.InternalBufferSize = 65536;

// Filter to reduce events
watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite;

// Handle Error event to detect overflow
watcher.Error += (s, e) => {
    if (e.GetException() is InternalBufferOverflowException)
    {
        // Trigger full resync
    }
};
```

### Issue 3: File Locked During Write

Events fire immediately when a file operation begins, but the file may still be locked.

**Solution - Retry Pattern:**
```csharp
private async Task<bool> WaitForFileReady(string path, int maxRetries = 10, int delayMs = 100)
{
    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None);
            return true;
        }
        catch (IOException)
        {
            await Task.Delay(delayMs);
        }
    }
    return false;
}
```

### Issue 4: Platform-Specific Behaviors

**Linux Issues:**
- `NotifyFilter` not fully respected (events may fire for filtered-out changes)
- Symlinks not properly watched
- System limits: `fs.inotify.max_user_watches`, `fs.inotify.max_user_instances`
- "No space left on device" error when limits exceeded

**macOS Issues:**
- Slow startup due to `Sync()` calls
- FSEvents may generate extra events
- Some event ordering issues

**Recommendations:**
- Test thoroughly on target platform
- Implement defensive event handling
- Consider polling as fallback for problematic scenarios

### Issue 5: NotifyFilters Configuration

```csharp
// Minimal filters for document monitoring
watcher.NotifyFilter = NotifyFilters.FileName    // File renames/moves
                     | NotifyFilters.LastWrite   // Content changes
                     | NotifyFilters.Size;       // Size changes (backup)

// Avoid unless needed (generates many events)
// NotifyFilters.LastAccess - fires on file reads
// NotifyFilters.Attributes - fires on attribute changes
```

---

## 5. Debouncing Implementation

### Why Debouncing is Necessary

1. Single file saves trigger multiple Changed events
2. Batch file operations flood the system
3. Editors use atomic saves (temp file -> rename)
4. Prevents redundant embedding regeneration

### Solution 1: Timer-Based Debouncing

```csharp
public class DebouncedFileWatcher : IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _debounceTokens = new();
    private readonly TimeSpan _debounceInterval = TimeSpan.FromMilliseconds(500);
    private readonly Action<string, WatcherChangeTypes> _onDebounced;

    public DebouncedFileWatcher(string path, Action<string, WatcherChangeTypes> onDebounced)
    {
        _onDebounced = onDebounced;
        _watcher = new FileSystemWatcher(path)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite
        };

        _watcher.Changed += OnFileEvent;
        _watcher.Created += OnFileEvent;
        _watcher.Deleted += OnFileEvent;
        _watcher.Renamed += OnRenamedEvent;
    }

    private void OnFileEvent(object sender, FileSystemEventArgs e)
    {
        DebounceEvent(e.FullPath, e.ChangeType);
    }

    private void OnRenamedEvent(object sender, RenamedEventArgs e)
    {
        // Handle old path as delete, new path as create
        DebounceEvent(e.OldFullPath, WatcherChangeTypes.Deleted);
        DebounceEvent(e.FullPath, WatcherChangeTypes.Created);
    }

    private void DebounceEvent(string path, WatcherChangeTypes changeType)
    {
        // Cancel existing debounce for this path
        if (_debounceTokens.TryRemove(path, out var existingCts))
        {
            existingCts.Cancel();
            existingCts.Dispose();
        }

        var cts = new CancellationTokenSource();
        _debounceTokens[path] = cts;

        Task.Delay(_debounceInterval, cts.Token).ContinueWith(t =>
        {
            if (!t.IsCanceled)
            {
                _debounceTokens.TryRemove(path, out _);
                _onDebounced(path, changeType);
            }
        }, TaskScheduler.Default);
    }

    public void Start() => _watcher.EnableRaisingEvents = true;
    public void Stop() => _watcher.EnableRaisingEvents = false;

    public void Dispose()
    {
        _watcher.Dispose();
        foreach (var cts in _debounceTokens.Values)
        {
            cts.Cancel();
            cts.Dispose();
        }
    }
}
```

### Solution 2: Using System.Reactive (Rx.NET)

```csharp
using System.Reactive.Linq;

public class RxFileWatcher : IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private readonly IDisposable _subscription;

    public RxFileWatcher(string path, Action<IList<FileSystemEventArgs>> onBatch)
    {
        _watcher = new FileSystemWatcher(path)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite
        };

        // Convert events to observables
        var created = Observable.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(
            h => _watcher.Created += h,
            h => _watcher.Created -= h)
            .Select(e => e.EventArgs);

        var changed = Observable.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(
            h => _watcher.Changed += h,
            h => _watcher.Changed -= h)
            .Select(e => e.EventArgs);

        var deleted = Observable.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(
            h => _watcher.Deleted += h,
            h => _watcher.Deleted -= h)
            .Select(e => e.EventArgs);

        var renamed = Observable.FromEventPattern<RenamedEventHandler, RenamedEventArgs>(
            h => _watcher.Renamed += h,
            h => _watcher.Renamed -= h)
            .Select(e => e.EventArgs as FileSystemEventArgs);

        // Merge and apply quiescent buffering
        _subscription = Observable.Merge(created, changed, deleted, renamed)
            .Quiescent(TimeSpan.FromSeconds(1))  // Wait for 1s of inactivity
            .Select(events => events
                .DistinctBy(e => (e.ChangeType, e.FullPath))
                .ToList())
            .Subscribe(onBatch);
    }

    public void Start() => _watcher.EnableRaisingEvents = true;
    public void Stop() => _watcher.EnableRaisingEvents = false;

    public void Dispose()
    {
        _subscription.Dispose();
        _watcher.Dispose();
    }
}

// Quiescent extension method (buffers until period of inactivity)
public static class ObservableExtensions
{
    public static IObservable<IList<T>> Quiescent<T>(
        this IObservable<T> source,
        TimeSpan minimumInactivityPeriod,
        IScheduler scheduler = null)
    {
        scheduler ??= Scheduler.Default;

        return source
            .Buffer(source.Throttle(minimumInactivityPeriod, scheduler))
            .Where(buffer => buffer.Count > 0);
    }
}
```

### Solution 3: Using Channels for Event Queuing

See Section 7 for Channel-based queuing with built-in deduplication.

### Recommended Debounce Intervals

| Scenario | Interval |
|----------|----------|
| Interactive editing | 300-500ms |
| Batch file operations | 1-2 seconds |
| Large file uploads | 2-5 seconds |
| Background sync | 5-10 seconds |

---

## 6. Background Service Pattern

### Implementing as BackgroundService

```csharp
public class DocumentWatcherService : BackgroundService, IDisposable
{
    private readonly ILogger<DocumentWatcherService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly DocumentWatcherOptions _options;
    private FileSystemWatcher? _watcher;
    private readonly Channel<FileChangeEvent> _eventChannel;

    public DocumentWatcherService(
        ILogger<DocumentWatcherService> logger,
        IServiceProvider serviceProvider,
        IOptions<DocumentWatcherOptions> options)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _eventChannel = Channel.CreateBounded<FileChangeEvent>(
            new BoundedChannelOptions(1000)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Document watcher starting for path: {Path}", _options.WatchPath);

        // Perform initial sync
        await PerformInitialSyncAsync(stoppingToken);

        // Start file watcher
        StartFileWatcher();

        // Process events from channel
        await ProcessEventsAsync(stoppingToken);
    }

    private void StartFileWatcher()
    {
        _watcher = new FileSystemWatcher(_options.WatchPath)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
            InternalBufferSize = 65536  // 64KB
        };

        foreach (var filter in _options.FileFilters)
        {
            _watcher.Filters.Add(filter);
        }

        _watcher.Created += (s, e) => EnqueueEvent(e.FullPath, ChangeType.Created);
        _watcher.Changed += (s, e) => EnqueueEvent(e.FullPath, ChangeType.Modified);
        _watcher.Deleted += (s, e) => EnqueueEvent(e.FullPath, ChangeType.Deleted);
        _watcher.Renamed += (s, e) => {
            EnqueueEvent(e.OldFullPath, ChangeType.Deleted);
            EnqueueEvent(e.FullPath, ChangeType.Created);
        };
        _watcher.Error += OnWatcherError;

        _watcher.EnableRaisingEvents = true;
        _logger.LogInformation("File watcher started");
    }

    private void EnqueueEvent(string path, ChangeType changeType)
    {
        var evt = new FileChangeEvent(path, changeType, DateTime.UtcNow);
        if (!_eventChannel.Writer.TryWrite(evt))
        {
            _logger.LogWarning("Event channel full, dropping event for {Path}", path);
        }
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        var ex = e.GetException();
        if (ex is InternalBufferOverflowException)
        {
            _logger.LogWarning("FileSystemWatcher buffer overflow. Scheduling resync.");
            // Trigger a full resync
            EnqueueEvent("*", ChangeType.Resync);
        }
        else
        {
            _logger.LogError(ex, "FileSystemWatcher error");
        }
    }

    private async Task ProcessEventsAsync(CancellationToken stoppingToken)
    {
        var debounceBuffer = new Dictionary<string, FileChangeEvent>();
        var debounceTimer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));

        var readerTask = Task.Run(async () =>
        {
            await foreach (var evt in _eventChannel.Reader.ReadAllAsync(stoppingToken))
            {
                lock (debounceBuffer)
                {
                    debounceBuffer[evt.Path] = evt;
                }
            }
        }, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await debounceTimer.WaitForNextTickAsync(stoppingToken);

            List<FileChangeEvent> eventsToProcess;
            lock (debounceBuffer)
            {
                eventsToProcess = debounceBuffer.Values.ToList();
                debounceBuffer.Clear();
            }

            foreach (var evt in eventsToProcess)
            {
                await ProcessEventAsync(evt, stoppingToken);
            }
        }
    }

    private async Task ProcessEventAsync(FileChangeEvent evt, CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var embeddingService = scope.ServiceProvider.GetRequiredService<IDocumentEmbeddingService>();

        try
        {
            switch (evt.ChangeType)
            {
                case ChangeType.Created:
                case ChangeType.Modified:
                    await embeddingService.UpsertDocumentAsync(evt.Path, stoppingToken);
                    break;
                case ChangeType.Deleted:
                    await embeddingService.DeleteDocumentAsync(evt.Path, stoppingToken);
                    break;
                case ChangeType.Resync:
                    await PerformInitialSyncAsync(stoppingToken);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing {ChangeType} for {Path}", evt.ChangeType, evt.Path);
            // Consider adding to dead letter queue
        }
    }

    private async Task PerformInitialSyncAsync(CancellationToken stoppingToken)
    {
        // See Section 11 for implementation
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Document watcher stopping...");

        _watcher?.Dispose();
        _watcher = null;

        _eventChannel.Writer.Complete();

        await base.StopAsync(cancellationToken);
    }

    public override void Dispose()
    {
        _watcher?.Dispose();
        base.Dispose();
    }
}

public record FileChangeEvent(string Path, ChangeType ChangeType, DateTime Timestamp);

public enum ChangeType
{
    Created,
    Modified,
    Deleted,
    Resync
}

public class DocumentWatcherOptions
{
    public string WatchPath { get; set; } = string.Empty;
    public List<string> FileFilters { get; set; } = new() { "*.txt", "*.md", "*.pdf", "*.docx" };
}
```

### Service Registration

```csharp
// Program.cs or Startup.cs
services.Configure<DocumentWatcherOptions>(configuration.GetSection("DocumentWatcher"));
services.AddHostedService<DocumentWatcherService>();
services.AddScoped<IDocumentEmbeddingService, DocumentEmbeddingService>();
```

### Graceful Shutdown Handling

1. `CancellationToken` is triggered when `IHostedService.StopAsync` is called
2. Override `StopAsync` to clean up resources
3. Implement `IDisposable` for resource cleanup even if `StopAsync` isn't called
4. Default shutdown timeout is 5 seconds (configurable via `HostOptions.ShutdownTimeout`)

```csharp
// Extend shutdown timeout if needed
services.Configure<HostOptions>(options =>
{
    options.ShutdownTimeout = TimeSpan.FromSeconds(30);
});
```

---

## 7. Processing Queue Architecture

### Using System.Threading.Channels

```csharp
public class FileChangeProcessor : IDisposable
{
    private readonly Channel<FileChangeEvent> _channel;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _processorTask;
    private readonly IDocumentEmbeddingService _embeddingService;
    private readonly ILogger<FileChangeProcessor> _logger;

    public FileChangeProcessor(
        IDocumentEmbeddingService embeddingService,
        ILogger<FileChangeProcessor> logger)
    {
        _embeddingService = embeddingService;
        _logger = logger;

        _channel = Channel.CreateBounded<FileChangeEvent>(
            new BoundedChannelOptions(10_000)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = false,  // Multiple consumers
                SingleWriter = false,  // Multiple producers (event handlers)
                AllowSynchronousContinuations = false
            });

        // Start multiple consumers
        _processorTask = Task.WhenAll(
            Enumerable.Range(0, 3).Select(_ => ProcessAsync(_cts.Token))
        );
    }

    public bool Enqueue(FileChangeEvent evt)
    {
        return _channel.Writer.TryWrite(evt);
    }

    private async Task ProcessAsync(CancellationToken cancellationToken)
    {
        await foreach (var evt in _channel.Reader.ReadAllAsync(cancellationToken))
        {
            try
            {
                await ProcessEventAsync(evt, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing event for {Path}", evt.Path);
            }
        }
    }

    private async Task ProcessEventAsync(FileChangeEvent evt, CancellationToken ct)
    {
        // Wait for file to be ready
        if (evt.ChangeType != ChangeType.Deleted)
        {
            if (!await WaitForFileReadyAsync(evt.Path, ct))
            {
                _logger.LogWarning("File not ready after retries: {Path}", evt.Path);
                return;
            }
        }

        switch (evt.ChangeType)
        {
            case ChangeType.Created:
            case ChangeType.Modified:
                await _embeddingService.UpsertDocumentAsync(evt.Path, ct);
                _logger.LogInformation("Processed {ChangeType}: {Path}", evt.ChangeType, evt.Path);
                break;

            case ChangeType.Deleted:
                await _embeddingService.DeleteDocumentAsync(evt.Path, ct);
                _logger.LogInformation("Deleted embedding for: {Path}", evt.Path);
                break;
        }
    }

    private async Task<bool> WaitForFileReadyAsync(string path, CancellationToken ct)
    {
        for (int i = 0; i < 10; i++)
        {
            try
            {
                using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                return true;
            }
            catch (IOException)
            {
                await Task.Delay(100, ct);
            }
            catch (FileNotFoundException)
            {
                return false;  // File was deleted
            }
        }
        return false;
    }

    public async ValueTask DisposeAsync()
    {
        _channel.Writer.Complete();
        _cts.Cancel();
        await _processorTask;
        _cts.Dispose();
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().Wait();
    }
}
```

### Avoiding Duplicate Processing

```csharp
public class DeduplicatingProcessor
{
    private readonly ConcurrentDictionary<string, DateTime> _lastProcessed = new();
    private readonly TimeSpan _deduplicationWindow = TimeSpan.FromSeconds(2);

    public bool ShouldProcess(FileChangeEvent evt)
    {
        var now = DateTime.UtcNow;

        if (_lastProcessed.TryGetValue(evt.Path, out var lastTime))
        {
            if (now - lastTime < _deduplicationWindow)
            {
                return false;  // Skip duplicate
            }
        }

        _lastProcessed[evt.Path] = now;
        return true;
    }

    public void CleanupOldEntries()
    {
        var threshold = DateTime.UtcNow - TimeSpan.FromMinutes(5);
        foreach (var kvp in _lastProcessed)
        {
            if (kvp.Value < threshold)
            {
                _lastProcessed.TryRemove(kvp.Key, out _);
            }
        }
    }
}
```

### Prioritization Strategy

```csharp
public class PrioritizedFileChangeProcessor
{
    // Priority: Deletes > Creates > Updates
    private readonly Channel<FileChangeEvent> _highPriority;   // Deletes
    private readonly Channel<FileChangeEvent> _mediumPriority; // Creates
    private readonly Channel<FileChangeEvent> _lowPriority;    // Updates

    public void Enqueue(FileChangeEvent evt)
    {
        var channel = evt.ChangeType switch
        {
            ChangeType.Deleted => _highPriority,
            ChangeType.Created => _mediumPriority,
            _ => _lowPriority
        };
        channel.Writer.TryWrite(evt);
    }

    private async Task ProcessAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            // Process in priority order
            if (_highPriority.Reader.TryRead(out var evt) ||
                _mediumPriority.Reader.TryRead(out evt) ||
                _lowPriority.Reader.TryRead(out evt))
            {
                await ProcessEventAsync(evt, ct);
            }
            else
            {
                await Task.Delay(50, ct);
            }
        }
    }
}
```

### Batch Processing for Efficiency

```csharp
public class BatchProcessor
{
    private readonly Channel<FileChangeEvent> _channel;
    private readonly int _batchSize = 10;
    private readonly TimeSpan _batchTimeout = TimeSpan.FromSeconds(5);

    public async Task ProcessBatchesAsync(CancellationToken ct)
    {
        var batch = new List<FileChangeEvent>();
        var batchTimer = new PeriodicTimer(_batchTimeout);

        while (!ct.IsCancellationRequested)
        {
            // Try to fill batch
            while (batch.Count < _batchSize &&
                   _channel.Reader.TryRead(out var evt))
            {
                batch.Add(evt);
            }

            if (batch.Count > 0)
            {
                await ProcessBatchAsync(batch, ct);
                batch.Clear();
            }

            await batchTimer.WaitForNextTickAsync(ct);
        }
    }

    private async Task ProcessBatchAsync(List<FileChangeEvent> batch, CancellationToken ct)
    {
        // Group by change type for efficient processing
        var creates = batch.Where(e => e.ChangeType == ChangeType.Created).ToList();
        var updates = batch.Where(e => e.ChangeType == ChangeType.Modified).ToList();
        var deletes = batch.Where(e => e.ChangeType == ChangeType.Deleted).ToList();

        // Process deletes first (quick operations)
        foreach (var evt in deletes)
        {
            await ProcessDeleteAsync(evt, ct);
        }

        // Batch embed creates and updates
        var toEmbed = creates.Concat(updates).ToList();
        if (toEmbed.Any())
        {
            await BatchEmbedAndUpsertAsync(toEmbed, ct);
        }
    }
}
```

---

## 8. Embedding Operations Mapping

### Event to Operation Mapping

| File Event | Embedding Operation | Vector Store Action |
|------------|--------------------|--------------------|
| Created | Generate embedding from file content | Upsert (insert new record) |
| Changed | Regenerate embedding | Upsert (update existing record) |
| Deleted | None | Delete by file path/document ID |
| Renamed | Generate for new path, delete old | Delete old + Upsert new |

### Implementation with Semantic Kernel

```csharp
public interface IDocumentEmbeddingService
{
    Task UpsertDocumentAsync(string filePath, CancellationToken ct = default);
    Task DeleteDocumentAsync(string filePath, CancellationToken ct = default);
    Task SyncDirectoryAsync(string directoryPath, CancellationToken ct = default);
}

public class DocumentEmbeddingService : IDocumentEmbeddingService
{
    private readonly ITextEmbeddingGenerationService _embeddingService;
    private readonly IVectorStoreRecordCollection<string, DocumentChunk> _collection;
    private readonly IDocumentParser _documentParser;
    private readonly ILogger<DocumentEmbeddingService> _logger;

    public DocumentEmbeddingService(
        ITextEmbeddingGenerationService embeddingService,
        IVectorStore vectorStore,
        IDocumentParser documentParser,
        ILogger<DocumentEmbeddingService> logger)
    {
        _embeddingService = embeddingService;
        _collection = vectorStore.GetCollection<string, DocumentChunk>("documents");
        _documentParser = documentParser;
        _logger = logger;
    }

    public async Task UpsertDocumentAsync(string filePath, CancellationToken ct = default)
    {
        // Delete existing chunks for this document
        await DeleteDocumentAsync(filePath, ct);

        // Parse document into chunks
        var chunks = await _documentParser.ParseAsync(filePath, ct);

        // Generate embeddings and upsert
        foreach (var chunk in chunks)
        {
            var embedding = await _embeddingService.GenerateEmbeddingAsync(chunk.Text, ct);

            var record = new DocumentChunk
            {
                Id = $"{filePath}:{chunk.Index}",
                FilePath = filePath,
                ChunkIndex = chunk.Index,
                Text = chunk.Text,
                Embedding = embedding,
                LastModified = File.GetLastWriteTimeUtc(filePath)
            };

            await _collection.UpsertAsync(record, ct);
        }

        _logger.LogInformation("Upserted {Count} chunks for {FilePath}", chunks.Count, filePath);
    }

    public async Task DeleteDocumentAsync(string filePath, CancellationToken ct = default)
    {
        // Find all chunks for this document
        var filter = new VectorSearchFilter()
            .EqualTo(nameof(DocumentChunk.FilePath), filePath);

        var existingChunks = await _collection
            .SearchAsync(ReadOnlyMemory<float>.Empty, top: 1000, filter, ct)
            .ToListAsync(ct);

        foreach (var chunk in existingChunks)
        {
            await _collection.DeleteAsync(chunk.Record.Id, ct);
        }

        _logger.LogInformation("Deleted {Count} chunks for {FilePath}", existingChunks.Count, filePath);
    }
}

public class DocumentChunk
{
    [VectorStoreKey]
    public string Id { get; set; } = string.Empty;

    [VectorStoreData]
    public string FilePath { get; set; } = string.Empty;

    [VectorStoreData]
    public int ChunkIndex { get; set; }

    [VectorStoreData]
    public string Text { get; set; } = string.Empty;

    [VectorStoreData]
    public DateTime LastModified { get; set; }

    [VectorStoreVector(1536)]
    public ReadOnlyMemory<float> Embedding { get; set; }
}
```

### Handling Renames

```csharp
public async Task HandleRenameAsync(string oldPath, string newPath, CancellationToken ct)
{
    // Option 1: Update metadata only (if content unchanged)
    var chunks = await GetChunksByFilePathAsync(oldPath, ct);
    foreach (var chunk in chunks)
    {
        chunk.FilePath = newPath;
        chunk.Id = $"{newPath}:{chunk.ChunkIndex}";
        await _collection.UpsertAsync(chunk, ct);
    }
    await DeleteByOldIdsAsync(oldPath, ct);

    // Option 2: Delete and recreate (simpler, works always)
    // await DeleteDocumentAsync(oldPath, ct);
    // await UpsertDocumentAsync(newPath, ct);
}
```

---

## 9. File Reading Considerations

### Waiting for File to be Fully Written

```csharp
public class FileReadyChecker
{
    public async Task<Stream?> WaitForFileReadyAsync(
        string path,
        int maxRetries = 20,
        int delayMs = 250,
        CancellationToken ct = default)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                // Try to open with exclusive access
                var stream = new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 4096,
                    useAsync: true);

                return stream;
            }
            catch (IOException) when (i < maxRetries - 1)
            {
                await Task.Delay(delayMs, ct);
            }
            catch (FileNotFoundException)
            {
                return null; // File was deleted
            }
        }

        throw new TimeoutException($"File still locked after {maxRetries} retries: {path}");
    }
}
```

### Reading Different File Types

```csharp
public interface IDocumentParser
{
    Task<List<TextChunk>> ParseAsync(string filePath, CancellationToken ct);
}

public class MultiFormatDocumentParser : IDocumentParser
{
    private readonly Dictionary<string, IFileTypeParser> _parsers;

    public MultiFormatDocumentParser()
    {
        _parsers = new Dictionary<string, IFileTypeParser>(StringComparer.OrdinalIgnoreCase)
        {
            { ".txt", new PlainTextParser() },
            { ".md", new MarkdownParser() },
            { ".pdf", new PdfParser() },      // Using PdfPig
            { ".docx", new DocxParser() },    // Using OpenXML
            { ".html", new HtmlParser() },
        };
    }

    public async Task<List<TextChunk>> ParseAsync(string filePath, CancellationToken ct)
    {
        var extension = Path.GetExtension(filePath);

        if (!_parsers.TryGetValue(extension, out var parser))
        {
            throw new NotSupportedException($"File type not supported: {extension}");
        }

        return await parser.ParseAsync(filePath, ct);
    }
}

// Plain text parser
public class PlainTextParser : IFileTypeParser
{
    private readonly int _chunkSize = 1000;
    private readonly int _chunkOverlap = 200;

    public async Task<List<TextChunk>> ParseAsync(string filePath, CancellationToken ct)
    {
        var content = await File.ReadAllTextAsync(filePath, Encoding.UTF8, ct);
        return ChunkText(content);
    }

    private List<TextChunk> ChunkText(string content)
    {
        var chunks = new List<TextChunk>();
        var sentences = content.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);

        var currentChunk = new StringBuilder();
        var chunkIndex = 0;

        foreach (var sentence in sentences)
        {
            if (currentChunk.Length + sentence.Length > _chunkSize && currentChunk.Length > 0)
            {
                chunks.Add(new TextChunk(chunkIndex++, currentChunk.ToString().Trim()));

                // Keep overlap
                var words = currentChunk.ToString().Split(' ');
                currentChunk.Clear();
                var overlapWords = words.TakeLast(_chunkOverlap / 10);
                currentChunk.Append(string.Join(" ", overlapWords));
            }

            currentChunk.Append(sentence.Trim()).Append(". ");
        }

        if (currentChunk.Length > 0)
        {
            chunks.Add(new TextChunk(chunkIndex, currentChunk.ToString().Trim()));
        }

        return chunks;
    }
}

// PDF parser using PdfPig
public class PdfParser : IFileTypeParser
{
    public async Task<List<TextChunk>> ParseAsync(string filePath, CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            var chunks = new List<TextChunk>();

            using var document = PdfDocument.Open(filePath);

            for (int i = 0; i < document.NumberOfPages; i++)
            {
                var page = document.GetPage(i + 1);
                var text = string.Join(" ", page.GetWords().Select(w => w.Text));

                if (!string.IsNullOrWhiteSpace(text))
                {
                    chunks.Add(new TextChunk(i, text));
                }
            }

            return chunks;
        }, ct);
    }
}

// DOCX parser using OpenXML
public class DocxParser : IFileTypeParser
{
    public async Task<List<TextChunk>> ParseAsync(string filePath, CancellationToken ct)
    {
        return await Task.Run(() =>
        {
            var chunks = new List<TextChunk>();

            using var document = WordprocessingDocument.Open(filePath, false);
            var body = document.MainDocumentPart?.Document.Body;

            if (body != null)
            {
                var paragraphs = body.Elements<Paragraph>();
                int index = 0;

                foreach (var para in paragraphs)
                {
                    var text = para.InnerText;
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        chunks.Add(new TextChunk(index++, text));
                    }
                }
            }

            return chunks;
        }, ct);
    }
}

public record TextChunk(int Index, string Text);
```

### Encoding Detection

```csharp
public class EncodingDetector
{
    public Encoding DetectEncoding(string filePath)
    {
        // Read BOM if present
        var bom = new byte[4];
        using (var file = new FileStream(filePath, FileMode.Open, FileAccess.Read))
        {
            file.Read(bom, 0, 4);
        }

        // Check for BOM
        if (bom[0] == 0xef && bom[1] == 0xbb && bom[2] == 0xbf)
            return Encoding.UTF8;
        if (bom[0] == 0xff && bom[1] == 0xfe)
            return Encoding.Unicode; // UTF-16 LE
        if (bom[0] == 0xfe && bom[1] == 0xff)
            return Encoding.BigEndianUnicode; // UTF-16 BE
        if (bom[0] == 0xff && bom[1] == 0xfe && bom[2] == 0 && bom[3] == 0)
            return Encoding.UTF32;

        // Default to UTF-8 without BOM
        return new UTF8Encoding(false);
    }
}
```

### Large File Handling

```csharp
public class LargeFileHandler
{
    private const long MaxFileSize = 50 * 1024 * 1024; // 50MB

    public async IAsyncEnumerable<TextChunk> ParseLargeFileAsync(
        string filePath,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var fileInfo = new FileInfo(filePath);

        if (fileInfo.Length > MaxFileSize)
        {
            throw new InvalidOperationException($"File too large: {fileInfo.Length} bytes");
        }

        using var reader = new StreamReader(filePath);
        var buffer = new StringBuilder();
        int chunkIndex = 0;
        string? line;

        while ((line = await reader.ReadLineAsync(ct)) != null)
        {
            buffer.AppendLine(line);

            if (buffer.Length >= 1000) // Chunk size
            {
                yield return new TextChunk(chunkIndex++, buffer.ToString());
                buffer.Clear();
            }
        }

        if (buffer.Length > 0)
        {
            yield return new TextChunk(chunkIndex, buffer.ToString());
        }
    }
}
```

---

## 10. Error Handling and Resilience

### Handling Processing Failures

```csharp
public class ResilientDocumentProcessor
{
    private readonly IDocumentEmbeddingService _embeddingService;
    private readonly Channel<FailedEvent> _deadLetterQueue;
    private readonly ILogger<ResilientDocumentProcessor> _logger;

    public async Task ProcessWithRetryAsync(FileChangeEvent evt, CancellationToken ct)
    {
        var policy = Policy
            .Handle<HttpRequestException>()
            .Or<TimeoutException>()
            .Or<IOException>()
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                onRetry: (ex, delay, attempt, ctx) =>
                {
                    _logger.LogWarning(ex,
                        "Retry {Attempt} for {Path} after {Delay}s",
                        attempt, evt.Path, delay.TotalSeconds);
                });

        try
        {
            await policy.ExecuteAsync(async () =>
            {
                await ProcessEventAsync(evt, ct);
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process {Path} after all retries", evt.Path);
            await _deadLetterQueue.Writer.WriteAsync(
                new FailedEvent(evt, ex, DateTime.UtcNow), ct);
        }
    }

    private async Task ProcessEventAsync(FileChangeEvent evt, CancellationToken ct)
    {
        switch (evt.ChangeType)
        {
            case ChangeType.Created:
            case ChangeType.Modified:
                await _embeddingService.UpsertDocumentAsync(evt.Path, ct);
                break;
            case ChangeType.Deleted:
                await _embeddingService.DeleteDocumentAsync(evt.Path, ct);
                break;
        }
    }
}

public record FailedEvent(FileChangeEvent Event, Exception Exception, DateTime FailedAt);
```

### Dead Letter Queue Processing

```csharp
public class DeadLetterQueueProcessor : BackgroundService
{
    private readonly Channel<FailedEvent> _dlq;
    private readonly IDocumentEmbeddingService _embeddingService;
    private readonly ILogger<DeadLetterQueueProcessor> _logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await RetryFailedEventsAsync(stoppingToken);
        }
    }

    private async Task RetryFailedEventsAsync(CancellationToken ct)
    {
        var failedEvents = new List<FailedEvent>();

        while (_dlq.Reader.TryRead(out var evt))
        {
            failedEvents.Add(evt);
        }

        foreach (var failed in failedEvents)
        {
            // Only retry if not too old
            if (DateTime.UtcNow - failed.FailedAt > TimeSpan.FromHours(24))
            {
                _logger.LogWarning("Dropping stale event for {Path}", failed.Event.Path);
                continue;
            }

            try
            {
                await ProcessEventAsync(failed.Event, ct);
                _logger.LogInformation("Successfully reprocessed {Path}", failed.Event.Path);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Retry failed for {Path}, re-queuing", failed.Event.Path);
                await _dlq.Writer.WriteAsync(failed with { FailedAt = DateTime.UtcNow }, ct);
            }
        }
    }
}
```

### Logging and Monitoring

```csharp
public class MetricsCollector
{
    private long _eventsReceived;
    private long _eventsProcessed;
    private long _eventsFailed;
    private long _bufferOverflows;

    public void IncrementReceived() => Interlocked.Increment(ref _eventsReceived);
    public void IncrementProcessed() => Interlocked.Increment(ref _eventsProcessed);
    public void IncrementFailed() => Interlocked.Increment(ref _eventsFailed);
    public void IncrementBufferOverflow() => Interlocked.Increment(ref _bufferOverflows);

    public ProcessingMetrics GetMetrics() => new(
        _eventsReceived,
        _eventsProcessed,
        _eventsFailed,
        _bufferOverflows);
}

public record ProcessingMetrics(
    long EventsReceived,
    long EventsProcessed,
    long EventsFailed,
    long BufferOverflows);
```

---

## 11. Initial Synchronization

### Scanning Directory on Startup

```csharp
public class InitialSyncService
{
    private readonly IDocumentEmbeddingService _embeddingService;
    private readonly IVectorStoreRecordCollection<string, DocumentChunk> _collection;
    private readonly ILogger<InitialSyncService> _logger;
    private readonly string[] _supportedExtensions;

    public async Task SyncAsync(string directoryPath, CancellationToken ct = default)
    {
        _logger.LogInformation("Starting initial sync for {Path}", directoryPath);

        // Get current state from vector store
        var storedDocuments = await GetStoredDocumentsAsync(ct);

        // Get current state from file system
        var fileSystemDocuments = GetFileSystemState(directoryPath);

        // Compare and sync
        var toAdd = fileSystemDocuments
            .Where(f => !storedDocuments.ContainsKey(f.Key) ||
                        storedDocuments[f.Key] < f.Value.LastWriteTimeUtc)
            .Select(f => f.Key)
            .ToList();

        var toRemove = storedDocuments.Keys
            .Where(path => !fileSystemDocuments.ContainsKey(path))
            .ToList();

        _logger.LogInformation(
            "Sync: {AddCount} to add/update, {RemoveCount} to remove",
            toAdd.Count, toRemove.Count);

        // Process removals
        foreach (var path in toRemove)
        {
            await _embeddingService.DeleteDocumentAsync(path, ct);
        }

        // Process additions/updates
        foreach (var path in toAdd)
        {
            try
            {
                await _embeddingService.UpsertDocumentAsync(path, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync {Path}", path);
            }
        }

        _logger.LogInformation("Initial sync completed");
    }

    private Dictionary<string, FileInfo> GetFileSystemState(string directoryPath)
    {
        var files = new Dictionary<string, FileInfo>();

        foreach (var extension in _supportedExtensions)
        {
            foreach (var file in Directory.EnumerateFiles(
                directoryPath, $"*{extension}", SearchOption.AllDirectories))
            {
                files[file] = new FileInfo(file);
            }
        }

        return files;
    }

    private async Task<Dictionary<string, DateTime>> GetStoredDocumentsAsync(CancellationToken ct)
    {
        // Query vector store for all unique file paths and their last modified dates
        var documents = new Dictionary<string, DateTime>();

        // This depends on your vector store implementation
        // Example using a search or iteration
        var results = await _collection.GetAsync(
            filter: null,
            top: 100000,
            cancellationToken: ct);

        await foreach (var doc in results)
        {
            if (!documents.ContainsKey(doc.FilePath) ||
                documents[doc.FilePath] < doc.LastModified)
            {
                documents[doc.FilePath] = doc.LastModified;
            }
        }

        return documents;
    }
}
```

### Hash-Based Change Detection

```csharp
public class HashBasedSyncService
{
    public async Task<string> ComputeFileHashAsync(string filePath, CancellationToken ct)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hash = await sha256.ComputeHashAsync(stream, ct);
        return Convert.ToHexString(hash);
    }

    public async Task SyncWithHashAsync(string directoryPath, CancellationToken ct)
    {
        var storedHashes = await GetStoredHashesAsync(ct);

        foreach (var file in Directory.EnumerateFiles(
            directoryPath, "*.*", SearchOption.AllDirectories))
        {
            var currentHash = await ComputeFileHashAsync(file, ct);

            if (!storedHashes.TryGetValue(file, out var storedHash) ||
                storedHash != currentHash)
            {
                // File is new or changed
                await ProcessFileAsync(file, currentHash, ct);
            }
        }
    }
}
```

### Full Resync vs Incremental Sync

```csharp
public enum SyncMode
{
    /// <summary>
    /// Only process changes since last sync
    /// </summary>
    Incremental,

    /// <summary>
    /// Delete all embeddings and reprocess everything
    /// </summary>
    Full
}

public class SyncOrchestrator
{
    public async Task SyncAsync(string path, SyncMode mode, CancellationToken ct)
    {
        if (mode == SyncMode.Full)
        {
            _logger.LogInformation("Performing full resync...");

            // Delete all existing embeddings
            await _collection.DeleteCollectionAsync(ct);
            await _collection.EnsureCollectionExistsAsync(ct);

            // Reprocess all files
            foreach (var file in EnumerateSupportedFiles(path))
            {
                await _embeddingService.UpsertDocumentAsync(file, ct);
            }
        }
        else
        {
            // Incremental sync
            await _syncService.SyncAsync(path, ct);
        }
    }
}
```

---

## 12. Complete Code Examples

### Full FileSystemWatcher Setup with Recursive Watching

```csharp
public class CompleteFileWatcher : IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private readonly ILogger<CompleteFileWatcher> _logger;

    public CompleteFileWatcher(string path, ILogger<CompleteFileWatcher> logger)
    {
        _logger = logger;

        _watcher = new FileSystemWatcher(path)
        {
            // Watch all subdirectories
            IncludeSubdirectories = true,

            // Set buffer size to maximum
            InternalBufferSize = 65536,

            // Watch for file name changes and content changes
            NotifyFilter = NotifyFilters.FileName
                         | NotifyFilters.DirectoryName
                         | NotifyFilters.LastWrite
                         | NotifyFilters.Size,
        };

        // Add file type filters
        _watcher.Filters.Add("*.txt");
        _watcher.Filters.Add("*.md");
        _watcher.Filters.Add("*.pdf");
        _watcher.Filters.Add("*.docx");

        // Subscribe to events
        _watcher.Created += OnCreated;
        _watcher.Changed += OnChanged;
        _watcher.Deleted += OnDeleted;
        _watcher.Renamed += OnRenamed;
        _watcher.Error += OnError;
    }

    public void Start()
    {
        _watcher.EnableRaisingEvents = true;
        _logger.LogInformation("FileSystemWatcher started for {Path}", _watcher.Path);
    }

    public void Stop()
    {
        _watcher.EnableRaisingEvents = false;
        _logger.LogInformation("FileSystemWatcher stopped");
    }

    private void OnCreated(object sender, FileSystemEventArgs e)
    {
        _logger.LogDebug("Created: {Path}", e.FullPath);
        // Queue for processing
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        if (e.ChangeType != WatcherChangeTypes.Changed)
            return;

        _logger.LogDebug("Changed: {Path}", e.FullPath);
        // Queue for processing
    }

    private void OnDeleted(object sender, FileSystemEventArgs e)
    {
        _logger.LogDebug("Deleted: {Path}", e.FullPath);
        // Queue for processing
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        _logger.LogDebug("Renamed: {OldPath} -> {NewPath}", e.OldFullPath, e.FullPath);
        // Queue delete of old path and create of new path
    }

    private void OnError(object sender, ErrorEventArgs e)
    {
        var ex = e.GetException();

        if (ex is InternalBufferOverflowException)
        {
            _logger.LogWarning("Buffer overflow detected. Some events may have been lost.");
            // Trigger resync
        }
        else
        {
            _logger.LogError(ex, "FileSystemWatcher error");
        }
    }

    public void Dispose()
    {
        _watcher.EnableRaisingEvents = false;
        _watcher.Dispose();
    }
}
```

### Complete BackgroundService Implementation

```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Threading.Channels;

public class DocumentSyncBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DocumentSyncBackgroundService> _logger;
    private readonly DocumentSyncOptions _options;
    private readonly Channel<FileChangeEvent> _eventChannel;
    private readonly ConcurrentDictionary<string, DateTime> _debounceTracker;

    private FileSystemWatcher? _watcher;

    public DocumentSyncBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<DocumentSyncBackgroundService> logger,
        IOptions<DocumentSyncOptions> options)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options.Value;
        _debounceTracker = new ConcurrentDictionary<string, DateTime>();

        _eventChannel = Channel.CreateBounded<FileChangeEvent>(
            new BoundedChannelOptions(_options.ChannelCapacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Document sync service starting...");

        try
        {
            // Initial sync
            await PerformInitialSyncAsync(stoppingToken);

            // Start file watcher
            InitializeWatcher();

            // Start debounce cleanup timer
            var cleanupTask = RunDebounceCleanupAsync(stoppingToken);

            // Process events
            await ProcessEventsAsync(stoppingToken);

            await cleanupTask;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("Document sync service shutting down...");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in document sync service");
            throw;
        }
    }

    private void InitializeWatcher()
    {
        _watcher = new FileSystemWatcher(_options.WatchPath)
        {
            IncludeSubdirectories = true,
            InternalBufferSize = _options.BufferSize,
            NotifyFilter = NotifyFilters.FileName
                         | NotifyFilters.DirectoryName
                         | NotifyFilters.LastWrite
        };

        foreach (var filter in _options.FileFilters)
        {
            _watcher.Filters.Add(filter);
        }

        _watcher.Created += (s, e) => EnqueueWithDebounce(e.FullPath, ChangeType.Created);
        _watcher.Changed += (s, e) => EnqueueWithDebounce(e.FullPath, ChangeType.Modified);
        _watcher.Deleted += (s, e) => EnqueueWithDebounce(e.FullPath, ChangeType.Deleted);
        _watcher.Renamed += (s, e) =>
        {
            EnqueueWithDebounce(e.OldFullPath, ChangeType.Deleted);
            EnqueueWithDebounce(e.FullPath, ChangeType.Created);
        };
        _watcher.Error += (s, e) =>
        {
            if (e.GetException() is InternalBufferOverflowException)
            {
                _logger.LogWarning("Buffer overflow - scheduling resync");
                _eventChannel.Writer.TryWrite(
                    new FileChangeEvent("*", ChangeType.Resync, DateTime.UtcNow));
            }
        };

        _watcher.EnableRaisingEvents = true;
        _logger.LogInformation("File watcher started for {Path}", _options.WatchPath);
    }

    private void EnqueueWithDebounce(string path, ChangeType changeType)
    {
        var now = DateTime.UtcNow;

        // Check if we recently processed this path
        if (_debounceTracker.TryGetValue(path, out var lastTime) &&
            now - lastTime < _options.DebounceInterval)
        {
            // Update the timestamp but don't enqueue
            _debounceTracker[path] = now;
            return;
        }

        _debounceTracker[path] = now;

        if (!_eventChannel.Writer.TryWrite(new FileChangeEvent(path, changeType, now)))
        {
            _logger.LogWarning("Channel full, dropping event for {Path}", path);
        }
    }

    private async Task ProcessEventsAsync(CancellationToken ct)
    {
        await foreach (var evt in _eventChannel.Reader.ReadAllAsync(ct))
        {
            using var scope = _serviceProvider.CreateScope();
            var embeddingService = scope.ServiceProvider
                .GetRequiredService<IDocumentEmbeddingService>();

            try
            {
                switch (evt.ChangeType)
                {
                    case ChangeType.Created:
                    case ChangeType.Modified:
                        if (await WaitForFileReadyAsync(evt.Path, ct))
                        {
                            await embeddingService.UpsertDocumentAsync(evt.Path, ct);
                            _logger.LogInformation(
                                "Processed {ChangeType} for {Path}",
                                evt.ChangeType, evt.Path);
                        }
                        break;

                    case ChangeType.Deleted:
                        await embeddingService.DeleteDocumentAsync(evt.Path, ct);
                        _logger.LogInformation("Deleted embeddings for {Path}", evt.Path);
                        break;

                    case ChangeType.Resync:
                        await PerformInitialSyncAsync(ct);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing {ChangeType} for {Path}",
                    evt.ChangeType, evt.Path);
            }
        }
    }

    private async Task<bool> WaitForFileReadyAsync(string path, CancellationToken ct)
    {
        for (int i = 0; i < 20; i++)
        {
            try
            {
                using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                return true;
            }
            catch (IOException)
            {
                await Task.Delay(250, ct);
            }
            catch (FileNotFoundException)
            {
                return false;
            }
        }

        _logger.LogWarning("File still locked after retries: {Path}", path);
        return false;
    }

    private async Task PerformInitialSyncAsync(CancellationToken ct)
    {
        _logger.LogInformation("Starting initial sync...");

        using var scope = _serviceProvider.CreateScope();
        var syncService = scope.ServiceProvider.GetRequiredService<IInitialSyncService>();

        await syncService.SyncAsync(_options.WatchPath, ct);

        _logger.LogInformation("Initial sync completed");
    }

    private async Task RunDebounceCleanupAsync(CancellationToken ct)
    {
        var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));

        while (await timer.WaitForNextTickAsync(ct))
        {
            var threshold = DateTime.UtcNow - TimeSpan.FromMinutes(5);

            foreach (var kvp in _debounceTracker)
            {
                if (kvp.Value < threshold)
                {
                    _debounceTracker.TryRemove(kvp.Key, out _);
                }
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping document sync service...");

        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
            _watcher = null;
        }

        _eventChannel.Writer.Complete();

        await base.StopAsync(cancellationToken);
    }
}

public class DocumentSyncOptions
{
    public string WatchPath { get; set; } = string.Empty;
    public List<string> FileFilters { get; set; } = new() { "*.txt", "*.md", "*.pdf", "*.docx" };
    public int BufferSize { get; set; } = 65536;
    public int ChannelCapacity { get; set; } = 10000;
    public TimeSpan DebounceInterval { get; set; } = TimeSpan.FromMilliseconds(500);
}
```

### Integration with Semantic Kernel Embedding Service

```csharp
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.Extensions.VectorData;

public class SemanticKernelEmbeddingService : IDocumentEmbeddingService
{
    private readonly ITextEmbeddingGenerationService _embeddingService;
    private readonly IVectorStoreRecordCollection<string, DocumentRecord> _collection;
    private readonly IDocumentParser _parser;
    private readonly ILogger<SemanticKernelEmbeddingService> _logger;

    public SemanticKernelEmbeddingService(
        ITextEmbeddingGenerationService embeddingService,
        IVectorStore vectorStore,
        IDocumentParser parser,
        ILogger<SemanticKernelEmbeddingService> logger)
    {
        _embeddingService = embeddingService;
        _parser = parser;
        _logger = logger;

        _collection = vectorStore.GetCollection<string, DocumentRecord>("documents");
    }

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await _collection.EnsureCollectionExistsAsync(ct);
    }

    public async Task UpsertDocumentAsync(string filePath, CancellationToken ct = default)
    {
        // Delete existing chunks
        await DeleteDocumentAsync(filePath, ct);

        // Parse document
        var chunks = await _parser.ParseAsync(filePath, ct);
        var fileInfo = new FileInfo(filePath);

        // Process each chunk
        foreach (var chunk in chunks)
        {
            // Generate embedding
            var embedding = await _embeddingService.GenerateEmbeddingAsync(chunk.Text, ct);

            // Create record
            var record = new DocumentRecord
            {
                Id = GenerateChunkId(filePath, chunk.Index),
                FilePath = filePath,
                FileName = fileInfo.Name,
                ChunkIndex = chunk.Index,
                Content = chunk.Text,
                ContentEmbedding = embedding,
                LastModified = fileInfo.LastWriteTimeUtc,
                FileSize = fileInfo.Length
            };

            // Upsert to vector store
            await _collection.UpsertAsync(record, ct);
        }

        _logger.LogDebug("Upserted {Count} chunks for {Path}", chunks.Count, filePath);
    }

    public async Task DeleteDocumentAsync(string filePath, CancellationToken ct = default)
    {
        // Find existing chunks for this document
        // Implementation depends on vector store capabilities
        // This is a simplified version

        var keysToDelete = new List<string>();

        // Iterate through potential chunk IDs
        for (int i = 0; i < 1000; i++) // Reasonable max chunks per doc
        {
            var key = GenerateChunkId(filePath, i);
            try
            {
                var existing = await _collection.GetAsync(key, ct);
                if (existing != null)
                {
                    keysToDelete.Add(key);
                }
                else
                {
                    break; // No more chunks
                }
            }
            catch
            {
                break;
            }
        }

        foreach (var key in keysToDelete)
        {
            await _collection.DeleteAsync(key, ct);
        }

        if (keysToDelete.Count > 0)
        {
            _logger.LogDebug("Deleted {Count} chunks for {Path}", keysToDelete.Count, filePath);
        }
    }

    private static string GenerateChunkId(string filePath, int chunkIndex)
    {
        var hash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(filePath)))[..16];
        return $"{hash}:{chunkIndex}";
    }
}

public class DocumentRecord
{
    [VectorStoreKey]
    public string Id { get; set; } = string.Empty;

    [VectorStoreData(IsFilterable = true)]
    public string FilePath { get; set; } = string.Empty;

    [VectorStoreData]
    public string FileName { get; set; } = string.Empty;

    [VectorStoreData]
    public int ChunkIndex { get; set; }

    [VectorStoreData]
    public string Content { get; set; } = string.Empty;

    [VectorStoreData]
    public DateTime LastModified { get; set; }

    [VectorStoreData]
    public long FileSize { get; set; }

    [VectorStoreVector(1536)] // OpenAI ada-002 dimensions
    public ReadOnlyMemory<float> ContentEmbedding { get; set; }
}
```

### Service Registration

```csharp
// Program.cs
var builder = Host.CreateApplicationBuilder(args);

// Configuration
builder.Services.Configure<DocumentSyncOptions>(
    builder.Configuration.GetSection("DocumentSync"));

// Semantic Kernel
builder.Services.AddSingleton(sp =>
{
    var kernelBuilder = Kernel.CreateBuilder();

    kernelBuilder.AddAzureOpenAITextEmbeddingGeneration(
        deploymentName: builder.Configuration["AzureOpenAI:EmbeddingDeployment"]!,
        endpoint: builder.Configuration["AzureOpenAI:Endpoint"]!,
        apiKey: builder.Configuration["AzureOpenAI:ApiKey"]!);

    return kernelBuilder.Build();
});

builder.Services.AddSingleton(sp =>
    sp.GetRequiredService<Kernel>().GetRequiredService<ITextEmbeddingGenerationService>());

// Vector Store (PostgreSQL with pgvector)
builder.Services.AddSingleton<IVectorStore>(sp =>
{
    var connectionString = builder.Configuration.GetConnectionString("VectorStore");
    return new PostgresVectorStore(connectionString);
});

// Document parsing
builder.Services.AddSingleton<IDocumentParser, MultiFormatDocumentParser>();

// Embedding service
builder.Services.AddScoped<IDocumentEmbeddingService, SemanticKernelEmbeddingService>();

// Initial sync service
builder.Services.AddScoped<IInitialSyncService, InitialSyncService>();

// Background service
builder.Services.AddHostedService<DocumentSyncBackgroundService>();

var host = builder.Build();

// Initialize vector store collection
using (var scope = host.Services.CreateScope())
{
    var embeddingService = scope.ServiceProvider.GetRequiredService<IDocumentEmbeddingService>();
    await ((SemanticKernelEmbeddingService)embeddingService).InitializeAsync();
}

await host.RunAsync();
```

---

## 13. Alternative: Polling Approach

### When to Use Polling vs FileSystemWatcher

| Scenario | Recommended Approach |
|----------|---------------------|
| Local directories | FileSystemWatcher |
| Network/mounted drives | Polling |
| Very large directories (100k+ files) | Polling |
| Cross-platform consistency required | Polling |
| Low-latency requirements | FileSystemWatcher |
| Infrequent changes | Polling |
| Many simultaneous changes | Hybrid |

### Implementing Polling-Based File Monitoring

```csharp
public class PollingFileWatcher : BackgroundService
{
    private readonly string _watchPath;
    private readonly TimeSpan _pollInterval;
    private readonly IDocumentEmbeddingService _embeddingService;
    private readonly ILogger<PollingFileWatcher> _logger;
    private readonly HashSet<string> _supportedExtensions;

    private Dictionary<string, FileState> _lastKnownState = new();

    public PollingFileWatcher(
        IOptions<PollingOptions> options,
        IDocumentEmbeddingService embeddingService,
        ILogger<PollingFileWatcher> logger)
    {
        _watchPath = options.Value.WatchPath;
        _pollInterval = options.Value.PollInterval;
        _embeddingService = embeddingService;
        _logger = logger;
        _supportedExtensions = options.Value.FileExtensions.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Polling file watcher starting for {Path}", _watchPath);

        // Initial scan
        _lastKnownState = ScanDirectory();
        await ProcessChangesAsync(
            _lastKnownState.Keys.ToList(),
            new List<string>(),
            _lastKnownState.Keys.ToList(),
            stoppingToken);

        // Polling loop
        var timer = new PeriodicTimer(_pollInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await PollForChangesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during polling cycle");
            }
        }
    }

    private async Task PollForChangesAsync(CancellationToken ct)
    {
        var currentState = ScanDirectory();

        var added = new List<string>();
        var modified = new List<string>();
        var deleted = new List<string>();

        // Find added and modified files
        foreach (var (path, state) in currentState)
        {
            if (!_lastKnownState.TryGetValue(path, out var lastState))
            {
                added.Add(path);
            }
            else if (state.LastWriteTime != lastState.LastWriteTime ||
                     state.Size != lastState.Size)
            {
                modified.Add(path);
            }
        }

        // Find deleted files
        foreach (var path in _lastKnownState.Keys)
        {
            if (!currentState.ContainsKey(path))
            {
                deleted.Add(path);
            }
        }

        // Update state
        _lastKnownState = currentState;

        // Process changes
        if (added.Count > 0 || modified.Count > 0 || deleted.Count > 0)
        {
            _logger.LogInformation(
                "Detected changes: {Added} added, {Modified} modified, {Deleted} deleted",
                added.Count, modified.Count, deleted.Count);

            await ProcessChangesAsync(added, deleted, modified, ct);
        }
    }

    private Dictionary<string, FileState> ScanDirectory()
    {
        var state = new Dictionary<string, FileState>();

        try
        {
            foreach (var file in Directory.EnumerateFiles(
                _watchPath, "*.*", SearchOption.AllDirectories))
            {
                var extension = Path.GetExtension(file);
                if (!_supportedExtensions.Contains(extension))
                    continue;

                try
                {
                    var info = new FileInfo(file);
                    state[file] = new FileState(info.LastWriteTimeUtc, info.Length);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not get file info for {Path}", file);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning directory {Path}", _watchPath);
        }

        return state;
    }

    private async Task ProcessChangesAsync(
        List<string> added,
        List<string> deleted,
        List<string> modified,
        CancellationToken ct)
    {
        // Process deletes first
        foreach (var path in deleted)
        {
            await _embeddingService.DeleteDocumentAsync(path, ct);
        }

        // Process adds and modifications
        foreach (var path in added.Concat(modified))
        {
            try
            {
                await _embeddingService.UpsertDocumentAsync(path, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process {Path}", path);
            }
        }
    }

    private record FileState(DateTime LastWriteTime, long Size);
}

public class PollingOptions
{
    public string WatchPath { get; set; } = string.Empty;
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(30);
    public List<string> FileExtensions { get; set; } = new() { ".txt", ".md", ".pdf", ".docx" };
}
```

### Using PhysicalFileProvider (Built-in .NET)

```csharp
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

public class PhysicalFileProviderWatcher : BackgroundService
{
    private readonly PhysicalFileProvider _fileProvider;
    private readonly ILogger<PhysicalFileProviderWatcher> _logger;

    public PhysicalFileProviderWatcher(
        IOptions<WatcherOptions> options,
        ILogger<PhysicalFileProviderWatcher> logger)
    {
        _logger = logger;

        _fileProvider = new PhysicalFileProvider(options.Value.WatchPath)
        {
            // Enable polling mode (works on all platforms)
            UsePollingFileWatcher = true,
            UseActivePolling = true
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var patterns = new[] { "**/*.txt", "**/*.md", "**/*.pdf" };

        foreach (var pattern in patterns)
        {
            WatchPattern(pattern, stoppingToken);
        }

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private void WatchPattern(string pattern, CancellationToken ct)
    {
        IChangeToken? token = null;

        void OnChange()
        {
            if (ct.IsCancellationRequested) return;

            _logger.LogInformation("Change detected for pattern: {Pattern}", pattern);
            // Note: PhysicalFileProvider doesn't tell you which specific file changed
            // You would need to track state yourself

            token = _fileProvider.Watch(pattern);
            token.RegisterChangeCallback(_ => OnChange(), null);
        }

        token = _fileProvider.Watch(pattern);
        token.RegisterChangeCallback(_ => OnChange(), null);
    }
}
```

### Hybrid Approach

```csharp
public class HybridFileWatcher : BackgroundService
{
    private readonly FileSystemWatcher? _fsWatcher;
    private readonly PollingFileWatcher _pollingWatcher;
    private readonly bool _usePolling;

    public HybridFileWatcher(
        IOptions<HybridOptions> options,
        IServiceProvider serviceProvider)
    {
        // Decide based on path type
        _usePolling = IsNetworkPath(options.Value.WatchPath) ||
                      options.Value.ForcePolling;

        if (!_usePolling)
        {
            try
            {
                _fsWatcher = new FileSystemWatcher(options.Value.WatchPath);
                // ... configure FileSystemWatcher
            }
            catch
            {
                // Fallback to polling if FSW fails
                _usePolling = true;
            }
        }

        if (_usePolling)
        {
            _pollingWatcher = serviceProvider.GetRequiredService<PollingFileWatcher>();
        }
    }

    private static bool IsNetworkPath(string path)
    {
        return path.StartsWith(@"\\") ||
               path.StartsWith("//") ||
               (path.Length >= 2 && path[1] == ':' &&
                new DriveInfo(path[0].ToString()).DriveType == DriveType.Network);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_usePolling)
        {
            await _pollingWatcher.StartAsync(stoppingToken);
        }
        else
        {
            _fsWatcher!.EnableRaisingEvents = true;
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
    }
}
```

### Pros and Cons

| Approach | Pros | Cons |
|----------|------|------|
| **FileSystemWatcher** | Low latency, low CPU when idle, efficient | Platform differences, buffer overflow, reliability issues |
| **Polling** | Reliable, consistent across platforms, works with network drives | Higher CPU usage, latency between changes, more I/O |
| **Hybrid** | Best of both worlds, fallback capability | More complex, harder to maintain |

---

## 14. Performance and Scalability

### Watching Thousands of Files

**Recommendations:**

1. **Limit scope**: Watch specific subdirectories rather than entire drives
2. **Use filters**: Only watch file types you care about
3. **Increase buffer**: Set `InternalBufferSize` to 64KB
4. **Handle overflow**: Always implement Error event handler
5. **Consider polling**: For directories with 100k+ files

```csharp
// Optimized for large directories
var watcher = new FileSystemWatcher(path)
{
    InternalBufferSize = 65536,
    IncludeSubdirectories = true,
    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite, // Minimal filters
};

// Only specific file types
watcher.Filters.Add("*.md");
watcher.Filters.Add("*.txt");
```

### Memory Considerations

- Each FileSystemWatcher buffer uses non-paged memory
- Default buffer (8KB) holds ~500 short file name events
- Maximum buffer (64KB) holds ~4000 short file name events
- Long file names consume more buffer space

### CPU Impact

- FileSystemWatcher: Low CPU when idle, spikes during event bursts
- Polling: Consistent CPU usage proportional to file count
- Processing: Embedding generation is the main CPU consumer

### Throttling Processing Rate

```csharp
public class ThrottledProcessor
{
    private readonly SemaphoreSlim _semaphore;
    private readonly int _maxConcurrent;

    public ThrottledProcessor(int maxConcurrentOperations = 5)
    {
        _maxConcurrent = maxConcurrentOperations;
        _semaphore = new SemaphoreSlim(maxConcurrentOperations);
    }

    public async Task ProcessAsync(IEnumerable<string> files, CancellationToken ct)
    {
        var tasks = files.Select(async file =>
        {
            await _semaphore.WaitAsync(ct);
            try
            {
                await ProcessFileAsync(file, ct);
            }
            finally
            {
                _semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
    }

    // Rate limiting for API calls
    public async Task ProcessWithRateLimitAsync(
        IEnumerable<string> files,
        int requestsPerMinute,
        CancellationToken ct)
    {
        var delay = TimeSpan.FromMinutes(1.0 / requestsPerMinute);

        foreach (var file in files)
        {
            await ProcessFileAsync(file, ct);
            await Task.Delay(delay, ct);
        }
    }
}
```

---

## 15. Sources

### Microsoft Documentation

- [FileSystemWatcher Class - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/api/system.io.filesystemwatcher?view=net-10.0)
- [System.IO.FileSystemWatcher - Runtime Libraries](https://learn.microsoft.com/en-us/dotnet/fundamentals/runtime-libraries/system-io-filesystemwatcher)
- [FileSystemWatcher.IncludeSubdirectories Property](https://learn.microsoft.com/en-us/dotnet/api/system.io.filesystemwatcher.includesubdirectories?view=net-8.0)
- [Channels in .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/channels)
- [Background tasks with hosted services in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-9.0)
- [Semantic Kernel Vector Store Data Ingestion](https://learn.microsoft.com/en-us/semantic-kernel/concepts/vector-store-connectors/how-to/vector-store-data-ingestion)
- [Semantic Kernel Embedding Generation](https://learn.microsoft.com/en-us/semantic-kernel/concepts/vector-store-connectors/embedding-generation)
- [InternalBufferOverflowException Class](https://learn.microsoft.com/en-us/dotnet/api/system.io.internalbufferoverflowexception?view=net-8.0)
- [FileSystemWatcher.InternalBufferSize Property](https://learn.microsoft.com/en-us/dotnet/api/system.io.filesystemwatcher.internalbuffersize?view=net-7.0)

### Community Resources and Tutorials

- [FileSystemWatcher in C# - Code Maze](https://code-maze.com/csharp-filesystemwatcher/)
- [C# FileSystemWatcher - ZetCode](https://www.zetcode.com/csharp/system-io-filesystemwatcher/)
- [How to Build a File Watcher Service in C# - IT Trip](https://en.ittrip.xyz/c-sharp/csharp-file-watcher)
- [An Introduction to System.Threading.Channels - .NET Blog](https://devblogs.microsoft.com/dotnet/an-introduction-to-system-threading-channels/)
- [How to implement Producer/Consumer with System.Threading.Channels - David Guida](https://www.davidguida.net/how-to-implement-producer-consumer-with-system-threading-channels/)
- [Using Channel as an async queue - Makolyte](https://makolyte.com/event-driven-dotnet-concurrent-producer-consumer-using-a-channel-as-a-non-blocking-async-queue/)
- [Understanding Background Services in .NET 8 - DEV Community](https://dev.to/moh_moh701/understanding-background-services-in-net-8-ihostedservice-and-backgroundservice-2eoh)
- [Using .NET Background Worker Service With FileSystemWatcher - CodeProject](https://www.codeproject.com/Articles/5344573/Using-Net-Background-Worker-Service-With-FileSyste)
- [Extending the shutdown timeout for graceful IHostedService shutdown - Andrew Lock](https://andrewlock.net/extending-the-shutdown-timeout-setting-to-ensure-graceful-ihostedservice-shutdown/)

### Debouncing and Duplicate Events

- [FileSystemWatcher generates duplicate events - Microsoft Learn Archive](https://learn.microsoft.com/en-us/archive/blogs/ahamza/filesystemwatcher-generates-duplicate-events-how-to-workaround)
- [A Robust Solution for FileSystemWatcher Firing Events Multiple Times - CodeProject](https://www.codeproject.com/Articles/1220093/A-Robust-Solution-for-FileSystemWatcher-Firing-Eve)
- [FileSystemWatcher is a Bit Broken - Failing Fast](https://failingfast.io/a-robust-solution-for-filesystemwatcher-firing-events-multiple-times/)
- [FileSystemWatcher vs Locked Files - CodeProject](https://www.codeproject.com/Articles/1219927/FileSystemWatcher-vs-Locked-Files)

### Reactive Extensions (Rx.NET)

- [Observe File System Changes with Reactive Extensions for .NET - Endjin](https://endjin.com/blog/2024/05/observe-file-system-changes-with-rx-dotnet)
- [Rx-FileSystemWatcher - GitHub](https://github.com/g0t4/Rx-FileSystemWatcher)
- [Creating Observable Sequences - Introduction to Rx.NET](https://introtorx.com/chapters/creating-observable-sequences)
- [Observable.Throttle Method - Microsoft Learn](https://learn.microsoft.com/en-us/previous-versions/dotnet/reactive-extensions/hh229400(v=vs.103))

### Platform-Specific Issues

- [FileSystemWatcher on Linux - GitHub Discussion](https://github.com/dotnet/runtime/discussions/69700)
- [FileSystemWatcher NotifyFilter Not Respected on Linux - GitHub Issue](https://github.com/dotnet/runtime/issues/113220)
- [FileSystemWatcher start performance issues on macOS - GitHub Issue](https://github.com/dotnet/runtime/issues/77793)
- [FileSystemWatcher does not raise events when target directory is symlink - GitHub Issue](https://github.com/dotnet/runtime/issues/25078)
- [fsnotify/fsnotify - Cross-platform filesystem notifications for Go](https://github.com/fsnotify/fsnotify)

### Polling Alternatives

- [FileSystem Watcher: Consider polling API - GitHub Issue](https://github.com/dotnet/runtime/issues/17111)
- [FileSystemWatcherAlts - GitHub](https://github.com/theXappy/FileSystemWatcherAlts)
- [PhysicalFileProvider.UsePollingFileWatcher Property - Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/api/microsoft.extensions.fileproviders.physicalfileprovider.usepollingfilewatcher?view=net-9.0-pp)
- [MiniFSWatcher - GitHub](https://github.com/CenterDevice/MiniFSWatcher)

### File Processing and Document Parsing

- [PdfPig - Read and extract text from PDFs in C#](https://uglytoad.github.io/PdfPig/)
- [GroupDocs.Parser - Extract Text from Markdown Files](https://blog.groupdocs.com/parser/extract-text-from-markdown-files-using-csharp/)
- [SemanticChunker.NET - GitHub](https://github.com/GregorBiswanger/SemanticChunker.NET)

### Semantic Kernel and Vector Stores

- [What are Semantic Kernel Vector Stores?](https://learn.microsoft.com/en-us/semantic-kernel/concepts/vector-store-connectors/)
- [Semantic Kernel Vector Store code samples](https://learn.microsoft.com/en-us/semantic-kernel/concepts/vector-store-connectors/code-samples)
- [Add embedding generation services to Semantic Kernel](https://learn.microsoft.com/en-us/semantic-kernel/concepts/ai-services/embedding-generation/)

---

## Summary

This research provides a comprehensive guide for implementing a file watching system in .NET to trigger embedding operations for RAG systems. Key takeaways:

1. **FileSystemWatcher** is the primary tool for real-time file monitoring in .NET, but requires careful handling of duplicate events, buffer overflows, and platform differences.

2. **Debouncing** is essential to prevent redundant processing when files are saved multiple times in quick succession.

3. **BackgroundService** provides a clean pattern for hosting file watching functionality with proper lifecycle management.

4. **System.Threading.Channels** offers an efficient producer-consumer pattern for queuing and processing file change events.

5. **Initial synchronization** on startup ensures the vector store stays in sync even when the service was down.

6. **Error handling** with retries and dead letter queues ensures resilience in production environments.

7. **Polling** may be preferred for network drives, very large directories, or when cross-platform consistency is critical.

8. **Semantic Kernel** provides excellent abstractions for embedding generation and vector store operations.

For production use, consider combining FileSystemWatcher with periodic full syncs to handle any missed events, and always test thoroughly on your target deployment platform.
