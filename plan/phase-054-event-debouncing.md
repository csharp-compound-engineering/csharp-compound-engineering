# Phase 054: File Event Debouncing

> **Status**: NOT_STARTED
> **Effort Estimate**: 4-6 hours
> **Category**: Document Processing
> **Prerequisites**: Phase 053

---

## Spec References

This phase implements debouncing patterns defined in:

- **spec/mcp-server/file-watcher.md** - [Debouncing](../spec/mcp-server/file-watcher.md#debouncing) (lines 72-78)
- **spec/mcp-server/file-watcher.md** - [Event Processing](../spec/mcp-server/file-watcher.md#event-processing) (lines 46-66)
- **spec/configuration.md** - [File Watcher Settings](../spec/configuration.md#files) (lines 50-64)

---

## Objectives

1. Implement 500ms default debounce interval for file system events
2. Implement event coalescing for rapid successive changes to the same file
3. Create timer-based debounce pattern with per-file tracking
4. Make debounce interval configurable via global settings
5. Handle multiple simultaneous file events efficiently
6. Ensure only the final state after debounce window is processed

---

## Acceptance Criteria

### Debounce Timer Implementation

- [ ] `FileEventDebouncer` class created with timer-based debouncing
- [ ] Default debounce interval of 500ms implemented
- [ ] Per-file tracking ensures each file has its own debounce window
- [ ] Timer resets when new event arrives for same file within window
- [ ] Only final event after debounce window fires for processing

### Event Coalescing

- [ ] Multiple events for same file within debounce window are coalesced
- [ ] Created -> Modified events collapse to single Created event
- [ ] Modified -> Modified events collapse to single Modified event
- [ ] Created -> Deleted events cancel out (no processing)
- [ ] Renamed events preserve source and target paths correctly
- [ ] Event coalescing logic handles all permutations of event sequences

### Configuration Integration

- [ ] Debounce interval read from `settings.json` (`file_watcher.debounce_ms`)
- [ ] Default value of 500ms used when not configured
- [ ] Configuration changes honored via IOptionsMonitor hot reload
- [ ] Minimum debounce interval enforced (e.g., 100ms floor)
- [ ] Maximum debounce interval enforced (e.g., 5000ms ceiling)

### Thread Safety

- [ ] Debouncer is thread-safe for concurrent file events
- [ ] Timer callbacks execute on thread pool threads
- [ ] Proper synchronization for shared state (pending events dictionary)
- [ ] No race conditions between timer expiry and new event arrival

### Performance

- [ ] Memory efficient for tracking many pending files
- [ ] Timers properly disposed when events are canceled or coalesced
- [ ] No memory leaks from abandoned timers
- [ ] Efficient dictionary lookups for high event volumes

---

## Implementation Notes

### Debounce Algorithm

The debounce pattern prevents excessive processing when editors save files multiple times in quick succession (auto-save, formatting, linting fixes):

```
Event arrives for file A
        |
        v
Is there a pending timer for file A?
        |
    +---+---+
    |       |
   No      Yes
    |       |
    v       v
Create    Reset timer,
new       update pending
timer     event type
    |       |
    +---+---+
        |
        v
Timer expires (500ms)
        |
        v
Process final event state
        |
        v
Remove from pending dictionary
```

### FileEventDebouncer Class

```csharp
/// <summary>
/// Debounces file system events to prevent excessive processing during rapid changes.
/// </summary>
public sealed class FileEventDebouncer : IDisposable
{
    private readonly ConcurrentDictionary<string, DebouncedFileEvent> _pendingEvents;
    private readonly IOptionsMonitor<FileWatcherOptions> _options;
    private readonly ILogger<FileEventDebouncer> _logger;
    private readonly Func<DebouncedFileEvent, CancellationToken, Task> _onDebounced;

    /// <summary>
    /// Creates a new file event debouncer.
    /// </summary>
    /// <param name="options">File watcher options (for debounce interval).</param>
    /// <param name="onDebounced">Callback invoked when debounce window expires.</param>
    /// <param name="logger">Logger instance.</param>
    public FileEventDebouncer(
        IOptionsMonitor<FileWatcherOptions> options,
        Func<DebouncedFileEvent, CancellationToken, Task> onDebounced,
        ILogger<FileEventDebouncer> logger)
    {
        _pendingEvents = new ConcurrentDictionary<string, DebouncedFileEvent>(StringComparer.OrdinalIgnoreCase);
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _onDebounced = onDebounced ?? throw new ArgumentNullException(nameof(onDebounced));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Enqueues a file event for debounced processing.
    /// </summary>
    public void Enqueue(string filePath, WatcherChangeTypes changeType, string? oldPath = null)
    {
        var normalizedPath = NormalizePath(filePath);
        var debounceMs = GetClampedDebounceInterval();

        _pendingEvents.AddOrUpdate(
            normalizedPath,
            // Add new pending event
            _ => CreateNewDebouncedEvent(normalizedPath, changeType, oldPath, debounceMs),
            // Update existing pending event
            (_, existing) => UpdateExistingEvent(existing, changeType, oldPath, debounceMs));
    }

    /// <summary>
    /// Cancels any pending event for the specified file.
    /// </summary>
    public bool Cancel(string filePath)
    {
        var normalizedPath = NormalizePath(filePath);
        if (_pendingEvents.TryRemove(normalizedPath, out var removed))
        {
            removed.Timer.Dispose();
            _logger.LogDebug("Cancelled pending event for {FilePath}", normalizedPath);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Gets the number of currently pending events.
    /// </summary>
    public int PendingCount => _pendingEvents.Count;

    public void Dispose()
    {
        foreach (var kvp in _pendingEvents)
        {
            kvp.Value.Timer.Dispose();
        }
        _pendingEvents.Clear();
    }

    private int GetClampedDebounceInterval()
    {
        var configured = _options.CurrentValue.DebounceMs;
        return Math.Clamp(configured, MinDebounceMs, MaxDebounceMs);
    }

    private const int MinDebounceMs = 100;
    private const int MaxDebounceMs = 5000;
    private const int DefaultDebounceMs = 500;

    private static string NormalizePath(string path) =>
        Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);

    private DebouncedFileEvent CreateNewDebouncedEvent(
        string path,
        WatcherChangeTypes changeType,
        string? oldPath,
        int debounceMs)
    {
        var evt = new DebouncedFileEvent
        {
            FilePath = path,
            ChangeType = changeType,
            OldPath = oldPath,
            FirstEventTime = DateTimeOffset.UtcNow,
            LastEventTime = DateTimeOffset.UtcNow,
            EventCount = 1
        };

        evt.Timer = new Timer(
            OnTimerElapsed,
            path,
            debounceMs,
            Timeout.Infinite);

        _logger.LogDebug(
            "New debounced event for {FilePath}: {ChangeType} (debounce: {DebounceMs}ms)",
            path, changeType, debounceMs);

        return evt;
    }

    private DebouncedFileEvent UpdateExistingEvent(
        DebouncedFileEvent existing,
        WatcherChangeTypes newChangeType,
        string? newOldPath,
        int debounceMs)
    {
        // Coalesce event types
        var coalescedType = CoalesceEventTypes(existing.ChangeType, newChangeType);

        // Handle special case: Created then Deleted = cancel
        if (coalescedType == WatcherChangeTypes.All)
        {
            // Signal cancellation - will be handled by timer callback
            existing.Cancelled = true;
        }
        else
        {
            existing.ChangeType = coalescedType;
        }

        existing.LastEventTime = DateTimeOffset.UtcNow;
        existing.EventCount++;

        // Update old path for rename operations
        if (!string.IsNullOrEmpty(newOldPath))
        {
            existing.OldPath = newOldPath;
        }

        // Reset the timer
        existing.Timer.Change(debounceMs, Timeout.Infinite);

        _logger.LogDebug(
            "Updated debounced event for {FilePath}: {ChangeType} (count: {Count})",
            existing.FilePath, coalescedType, existing.EventCount);

        return existing;
    }

    private static WatcherChangeTypes CoalesceEventTypes(
        WatcherChangeTypes existing,
        WatcherChangeTypes incoming)
    {
        // Event coalescing rules:
        // Created + Modified = Created (file is still new)
        // Created + Deleted = Cancel (file created and immediately deleted)
        // Modified + Modified = Modified
        // Modified + Deleted = Deleted
        // Renamed + Modified = Renamed (content changed after rename)
        // Renamed + Deleted = Deleted

        return (existing, incoming) switch
        {
            (WatcherChangeTypes.Created, WatcherChangeTypes.Changed) => WatcherChangeTypes.Created,
            (WatcherChangeTypes.Created, WatcherChangeTypes.Deleted) => WatcherChangeTypes.All, // Cancel signal
            (WatcherChangeTypes.Changed, WatcherChangeTypes.Changed) => WatcherChangeTypes.Changed,
            (WatcherChangeTypes.Changed, WatcherChangeTypes.Deleted) => WatcherChangeTypes.Deleted,
            (WatcherChangeTypes.Renamed, WatcherChangeTypes.Changed) => WatcherChangeTypes.Renamed,
            (WatcherChangeTypes.Renamed, WatcherChangeTypes.Deleted) => WatcherChangeTypes.Deleted,
            _ => incoming // Default to incoming for other combinations
        };
    }

    private async void OnTimerElapsed(object? state)
    {
        var path = (string)state!;

        if (!_pendingEvents.TryRemove(path, out var evt))
        {
            return; // Already processed or cancelled
        }

        evt.Timer.Dispose();

        if (evt.Cancelled)
        {
            _logger.LogDebug(
                "Debounced event for {FilePath} cancelled (created then deleted)",
                path);
            return;
        }

        _logger.LogInformation(
            "Processing debounced {ChangeType} event for {FilePath} (coalesced {Count} events over {Duration}ms)",
            evt.ChangeType,
            evt.FilePath,
            evt.EventCount,
            (evt.LastEventTime - evt.FirstEventTime).TotalMilliseconds);

        try
        {
            await _onDebounced(evt, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing debounced event for {FilePath}", path);
        }
    }
}
```

### DebouncedFileEvent Record

```csharp
/// <summary>
/// Represents a debounced file system event.
/// </summary>
public sealed class DebouncedFileEvent
{
    /// <summary>
    /// The full path to the file.
    /// </summary>
    public required string FilePath { get; set; }

    /// <summary>
    /// The coalesced change type.
    /// </summary>
    public WatcherChangeTypes ChangeType { get; set; }

    /// <summary>
    /// For rename operations, the original file path.
    /// </summary>
    public string? OldPath { get; set; }

    /// <summary>
    /// When the first event in this debounce window arrived.
    /// </summary>
    public DateTimeOffset FirstEventTime { get; set; }

    /// <summary>
    /// When the most recent event in this debounce window arrived.
    /// </summary>
    public DateTimeOffset LastEventTime { get; set; }

    /// <summary>
    /// Number of raw events coalesced into this debounced event.
    /// </summary>
    public int EventCount { get; set; }

    /// <summary>
    /// Timer that fires when debounce window expires.
    /// </summary>
    internal Timer Timer { get; set; } = null!;

    /// <summary>
    /// Whether this event was cancelled (e.g., created then immediately deleted).
    /// </summary>
    internal bool Cancelled { get; set; }
}
```

### FileWatcherOptions Class

```csharp
/// <summary>
/// Configuration options for the file watcher service.
/// </summary>
public class FileWatcherOptions
{
    public const string SectionName = "FileWatcher";

    /// <summary>
    /// Milliseconds to wait after a file change before processing.
    /// Default is 500ms. Minimum 100ms, maximum 5000ms.
    /// </summary>
    public int DebounceMs { get; set; } = 500;
}
```

### Integration with FileWatcherService

```csharp
public class FileWatcherService : BackgroundService
{
    private readonly FileEventDebouncer _debouncer;
    private FileSystemWatcher? _watcher;

    public FileWatcherService(
        IOptionsMonitor<FileWatcherOptions> options,
        IDocumentProcessingService documentProcessor,
        ILogger<FileWatcherService> logger)
    {
        _debouncer = new FileEventDebouncer(
            options,
            ProcessDebouncedEventAsync,
            logger);
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        // Raw events go to debouncer
        _debouncer.Enqueue(e.FullPath, e.ChangeType);
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        // Rename events include old path
        _debouncer.Enqueue(e.FullPath, e.ChangeType, e.OldFullPath);
    }

    private async Task ProcessDebouncedEventAsync(
        DebouncedFileEvent evt,
        CancellationToken ct)
    {
        switch (evt.ChangeType)
        {
            case WatcherChangeTypes.Created:
                await _documentProcessor.IndexDocumentAsync(evt.FilePath, ct);
                break;

            case WatcherChangeTypes.Changed:
                await _documentProcessor.UpdateDocumentAsync(evt.FilePath, ct);
                break;

            case WatcherChangeTypes.Deleted:
                await _documentProcessor.RemoveDocumentAsync(evt.FilePath, ct);
                break;

            case WatcherChangeTypes.Renamed:
                await _documentProcessor.RenameDocumentAsync(
                    evt.OldPath!, evt.FilePath, ct);
                break;
        }
    }

    public override void Dispose()
    {
        _debouncer.Dispose();
        _watcher?.Dispose();
        base.Dispose();
    }
}
```

### Event Coalescing Truth Table

| Existing Event | Incoming Event | Coalesced Result | Notes |
|----------------|----------------|------------------|-------|
| Created | Changed | Created | Still a new file |
| Created | Deleted | Cancel | File never existed from our perspective |
| Changed | Changed | Changed | Multiple saves |
| Changed | Deleted | Deleted | File removed |
| Renamed | Changed | Renamed | Content changed after rename |
| Renamed | Deleted | Deleted | File removed after rename |
| Deleted | Created | Created | File recreated (rare edge case) |

### Sequence Diagram: Multiple Rapid Saves

```
Time (ms)   File A Events          Debouncer State
─────────────────────────────────────────────────────
   0        Changed                Timer started (500ms)
  50        Changed                Timer reset, count=2
 100        Changed                Timer reset, count=3
 200        Changed                Timer reset, count=4
 500        -                      (waiting)
 700        Timer expires          -> Process single Changed event
```

---

## Test Cases

### Unit Tests

```csharp
[Fact]
public async Task Debouncer_CoalescesMultipleChangedEvents()
{
    // Arrange
    var processed = new List<DebouncedFileEvent>();
    var debouncer = CreateDebouncer(evt => { processed.Add(evt); return Task.CompletedTask; });

    // Act: Simulate rapid file saves
    debouncer.Enqueue("/test/file.md", WatcherChangeTypes.Changed);
    await Task.Delay(100);
    debouncer.Enqueue("/test/file.md", WatcherChangeTypes.Changed);
    await Task.Delay(100);
    debouncer.Enqueue("/test/file.md", WatcherChangeTypes.Changed);
    await Task.Delay(600); // Wait for debounce

    // Assert: Only one event processed
    Assert.Single(processed);
    Assert.Equal(3, processed[0].EventCount);
    Assert.Equal(WatcherChangeTypes.Changed, processed[0].ChangeType);
}

[Fact]
public async Task Debouncer_CancelsCreatedThenDeletedSequence()
{
    // Arrange
    var processed = new List<DebouncedFileEvent>();
    var debouncer = CreateDebouncer(evt => { processed.Add(evt); return Task.CompletedTask; });

    // Act: File created then immediately deleted
    debouncer.Enqueue("/test/temp.md", WatcherChangeTypes.Created);
    await Task.Delay(100);
    debouncer.Enqueue("/test/temp.md", WatcherChangeTypes.Deleted);
    await Task.Delay(600);

    // Assert: No event processed (cancelled out)
    Assert.Empty(processed);
}

[Fact]
public async Task Debouncer_PreservesCreatedOnChangedFollowup()
{
    // Arrange
    var processed = new List<DebouncedFileEvent>();
    var debouncer = CreateDebouncer(evt => { processed.Add(evt); return Task.CompletedTask; });

    // Act: New file with immediate edit
    debouncer.Enqueue("/test/new.md", WatcherChangeTypes.Created);
    await Task.Delay(100);
    debouncer.Enqueue("/test/new.md", WatcherChangeTypes.Changed);
    await Task.Delay(600);

    // Assert: Still treated as Created
    Assert.Single(processed);
    Assert.Equal(WatcherChangeTypes.Created, processed[0].ChangeType);
}

[Fact]
public async Task Debouncer_HandlesMultipleFilesIndependently()
{
    // Arrange
    var processed = new ConcurrentBag<DebouncedFileEvent>();
    var debouncer = CreateDebouncer(evt => { processed.Add(evt); return Task.CompletedTask; });

    // Act: Changes to different files
    debouncer.Enqueue("/test/file1.md", WatcherChangeTypes.Changed);
    debouncer.Enqueue("/test/file2.md", WatcherChangeTypes.Changed);
    debouncer.Enqueue("/test/file3.md", WatcherChangeTypes.Changed);
    await Task.Delay(600);

    // Assert: Three separate events
    Assert.Equal(3, processed.Count);
}

[Fact]
public async Task Debouncer_RespectsConfiguredInterval()
{
    // Arrange
    var options = new FileWatcherOptions { DebounceMs = 200 };
    var processed = new List<DebouncedFileEvent>();
    var debouncer = CreateDebouncer(options, evt => { processed.Add(evt); return Task.CompletedTask; });

    // Act
    debouncer.Enqueue("/test/file.md", WatcherChangeTypes.Changed);
    await Task.Delay(100); // Still within 200ms window
    Assert.Empty(processed);

    await Task.Delay(150); // Now past 200ms
    Assert.Single(processed);
}

[Fact]
public void Debouncer_ClampsIntervalToMinimum()
{
    // Arrange: Configure interval below minimum
    var options = new FileWatcherOptions { DebounceMs = 10 };
    // Assert: Interval clamped to 100ms minimum
}

[Fact]
public void Debouncer_ClampsIntervalToMaximum()
{
    // Arrange: Configure interval above maximum
    var options = new FileWatcherOptions { DebounceMs = 10000 };
    // Assert: Interval clamped to 5000ms maximum
}

[Fact]
public async Task Debouncer_HandlesRenameWithSubsequentChange()
{
    // Arrange
    var processed = new List<DebouncedFileEvent>();
    var debouncer = CreateDebouncer(evt => { processed.Add(evt); return Task.CompletedTask; });

    // Act: Rename then edit
    debouncer.Enqueue("/test/newname.md", WatcherChangeTypes.Renamed, "/test/oldname.md");
    await Task.Delay(100);
    debouncer.Enqueue("/test/newname.md", WatcherChangeTypes.Changed);
    await Task.Delay(600);

    // Assert: Treated as Renamed with preserved OldPath
    Assert.Single(processed);
    Assert.Equal(WatcherChangeTypes.Renamed, processed[0].ChangeType);
    Assert.Equal("/test/oldname.md", processed[0].OldPath);
}

[Fact]
public async Task Debouncer_IsThreadSafe()
{
    // Arrange
    var processed = new ConcurrentBag<DebouncedFileEvent>();
    var debouncer = CreateDebouncer(evt => { processed.Add(evt); return Task.CompletedTask; });

    // Act: Concurrent events from multiple threads
    var tasks = Enumerable.Range(0, 100).Select(i =>
        Task.Run(() => debouncer.Enqueue($"/test/file{i}.md", WatcherChangeTypes.Changed)));
    await Task.WhenAll(tasks);
    await Task.Delay(600);

    // Assert: All files processed exactly once
    Assert.Equal(100, processed.Count);
}

[Fact]
public void Debouncer_Cancel_RemovesPendingEvent()
{
    // Arrange
    var debouncer = CreateDebouncer(evt => Task.CompletedTask);
    debouncer.Enqueue("/test/file.md", WatcherChangeTypes.Changed);

    // Act
    var cancelled = debouncer.Cancel("/test/file.md");

    // Assert
    Assert.True(cancelled);
    Assert.Equal(0, debouncer.PendingCount);
}

[Fact]
public void Debouncer_Dispose_CleansUpAllTimers()
{
    // Arrange
    var debouncer = CreateDebouncer(evt => Task.CompletedTask);
    debouncer.Enqueue("/test/file1.md", WatcherChangeTypes.Changed);
    debouncer.Enqueue("/test/file2.md", WatcherChangeTypes.Changed);

    // Act
    debouncer.Dispose();

    // Assert: No memory leaks, pending count is 0
    Assert.Equal(0, debouncer.PendingCount);
}
```

### Integration Tests

```csharp
[Fact]
public async Task FileWatcher_DebouncesRapidEditorSaves()
{
    // Simulate VS Code auto-save behavior (multiple writes within milliseconds)
}

[Fact]
public async Task FileWatcher_ProcessesAfterDebounceWindow()
{
    // Verify document is indexed only after debounce expires
}

[Fact]
public async Task Configuration_HotReload_UpdatesDebounceInterval()
{
    // Change settings.json and verify new interval takes effect
}
```

---

## Dependencies

### Depends On

- Phase 053: File Change Detection (FileSystemWatcher setup)
- Phase 010: Project Configuration (IOptionsMonitor pattern)
- Phase 008: Global Configuration (settings.json structure)

### Blocks

- Phase 055: Document Indexing Pipeline (needs debounced events)
- File watcher integration tests

---

## Verification Steps

After completing this phase, verify:

1. **Default debounce**: Create a file and verify 500ms delay before processing
2. **Event coalescing**: Save a file multiple times rapidly, verify single processing event
3. **Created+Deleted cancellation**: Create then delete file quickly, verify no processing
4. **Configuration**: Change `debounce_ms` in settings.json, verify new interval used
5. **Multiple files**: Change several files, verify each debounced independently
6. **Memory**: Dispose debouncer, verify no timer leaks
7. **Logs**: Verify debug logs show coalesced event counts

---

## Notes

- FileSystemWatcher can fire multiple events for a single save (common with editors)
- The 500ms default balances responsiveness with avoiding duplicate work
- Per-file timers allow high throughput when many files change simultaneously
- Event coalescing reduces unnecessary embedding regeneration
- The `WatcherChangeTypes.All` value is repurposed as a cancellation signal
- ConcurrentDictionary provides thread-safe operations without explicit locking
- Timer callbacks run on thread pool threads; ensure downstream code is thread-safe
