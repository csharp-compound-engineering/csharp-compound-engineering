# Phase 053: File Watcher Service Structure

> **Status**: PLANNED
> **Category**: Document Processing
> **Estimated Effort**: L
> **Prerequisites**: Phase 023 (Server Lifecycle), Phase 048 (Document Processing Pipeline)

---

## Spec References

- [mcp-server/file-watcher.md](../spec/mcp-server/file-watcher.md) - Complete file watcher specification
- [structure/mcp-server.md](../structure/mcp-server.md) - File watcher summary
- [research/dotnet-file-watcher-embeddings-research.md](../research/dotnet-file-watcher-embeddings-research.md) - FileSystemWatcher patterns and debouncing
- [research/hosted-services-background-tasks.md](../research/hosted-services-background-tasks.md) - IHostedService patterns

---

## Objectives

1. Implement `FileWatcherService` as an `IHostedService` for managed lifecycle
2. Configure `System.IO.FileSystemWatcher` for recursive directory monitoring
3. Establish watch path configuration for `./csharp-compounding-docs/` directory
4. Integrate with server lifecycle (start on project activation, stop on deactivation)
5. Create the structural foundation for event processing (debouncing implemented in Phase 054)

---

## Acceptance Criteria

- [ ] `FileWatcherService` implements `IHostedService` interface
- [ ] `System.IO.FileSystemWatcher` configured with correct `NotifyFilter` settings
- [ ] `IncludeSubdirectories = true` enabled for recursive watching
- [ ] `InternalBufferSize` increased to 65536 bytes (64KB) for reliability
- [ ] File filter set to `*.md` for markdown files only
- [ ] Service starts watching when project is activated via `IProjectActivationHandler`
- [ ] Service stops watching when project is deactivated (new project activated)
- [ ] Watch path configurable via `IProjectConfiguration`
- [ ] `IFileWatcherService` interface created for testability and dependency injection
- [ ] Events routed to `IFileChangeHandler` for downstream processing
- [ ] Unit tests verify watcher configuration and lifecycle behavior
- [ ] Error event handler logs buffer overflows with appropriate warnings

---

## Implementation Notes

### 1. IFileWatcherService Interface

Create a testable interface for the file watcher service:

```csharp
// src/CompoundDocs.McpServer/Services/IFileWatcherService.cs
namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Service responsible for monitoring the compounding docs directory
/// and triggering document processing when files change.
/// </summary>
public interface IFileWatcherService
{
    /// <summary>
    /// Gets whether the watcher is currently active.
    /// </summary>
    bool IsWatching { get; }

    /// <summary>
    /// Gets the currently watched path, or null if not watching.
    /// </summary>
    string? WatchedPath { get; }

    /// <summary>
    /// Starts watching the specified directory for file changes.
    /// </summary>
    /// <param name="path">The absolute path to the compounding docs directory.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StartWatchingAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops watching the current directory.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StopWatchingAsync(CancellationToken cancellationToken = default);
}
```

### 2. IFileChangeHandler Interface

Interface for components that process file change events:

```csharp
// src/CompoundDocs.McpServer/Services/IFileChangeHandler.cs
namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Handles file change events from the file watcher service.
/// Implementations are responsible for debouncing and processing changes.
/// </summary>
public interface IFileChangeHandler
{
    /// <summary>
    /// Called when a file is created in the watched directory.
    /// </summary>
    Task HandleFileCreatedAsync(string fullPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Called when a file is modified in the watched directory.
    /// </summary>
    Task HandleFileChangedAsync(string fullPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Called when a file is deleted from the watched directory.
    /// </summary>
    Task HandleFileDeletedAsync(string fullPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Called when a file is renamed in the watched directory.
    /// </summary>
    Task HandleFileRenamedAsync(string oldPath, string newPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Called when a buffer overflow occurs and events may have been lost.
    /// </summary>
    Task HandleBufferOverflowAsync(CancellationToken cancellationToken = default);
}
```

### 3. FileWatcherOptions Configuration

```csharp
// src/CompoundDocs.McpServer/Configuration/FileWatcherOptions.cs
namespace CompoundDocs.McpServer.Configuration;

/// <summary>
/// Configuration options for the file watcher service.
/// </summary>
public sealed class FileWatcherOptions
{
    public const string SectionName = "FileWatcher";

    /// <summary>
    /// Default name of the compounding docs directory.
    /// Default: "csharp-compounding-docs"
    /// </summary>
    public string CompoundingDocsDirectoryName { get; set; } = "csharp-compounding-docs";

    /// <summary>
    /// File filter pattern for watched files.
    /// Default: "*.md"
    /// </summary>
    public string FileFilter { get; set; } = "*.md";

    /// <summary>
    /// Internal buffer size for FileSystemWatcher in bytes.
    /// Must be a multiple of 4KB, maximum 64KB.
    /// Default: 65536 (64KB)
    /// </summary>
    public int InternalBufferSize { get; set; } = 65536;

    /// <summary>
    /// Whether to watch subdirectories recursively.
    /// Default: true
    /// </summary>
    public bool IncludeSubdirectories { get; set; } = true;

    /// <summary>
    /// Timeout for stopping the watcher gracefully.
    /// Default: 5 seconds
    /// </summary>
    public TimeSpan StopTimeout { get; set; } = TimeSpan.FromSeconds(5);
}
```

### 4. FileWatcherService Implementation

```csharp
// src/CompoundDocs.McpServer/Services/FileWatcherService.cs
using System.IO;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using CompoundDocs.McpServer.Configuration;

namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Hosted service that monitors the compounding docs directory for file changes.
/// Implements IHostedService for proper lifecycle management within the .NET Generic Host.
/// </summary>
public sealed class FileWatcherService : IFileWatcherService, IHostedService, IDisposable
{
    private readonly IFileChangeHandler _changeHandler;
    private readonly ILogger<FileWatcherService> _logger;
    private readonly FileWatcherOptions _options;
    private readonly object _lock = new();

    private FileSystemWatcher? _watcher;
    private string? _watchedPath;
    private bool _isDisposed;

    public FileWatcherService(
        IFileChangeHandler changeHandler,
        IOptions<FileWatcherOptions> options,
        ILogger<FileWatcherService> logger)
    {
        _changeHandler = changeHandler ?? throw new ArgumentNullException(nameof(changeHandler));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public bool IsWatching
    {
        get
        {
            lock (_lock)
            {
                return _watcher?.EnableRaisingEvents ?? false;
            }
        }
    }

    /// <inheritdoc />
    public string? WatchedPath
    {
        get
        {
            lock (_lock)
            {
                return _watchedPath;
            }
        }
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("FileWatcherService starting. Waiting for project activation.");
        // Service starts but doesn't watch until a project is activated
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("FileWatcherService stopping...");
        await StopWatchingAsync(cancellationToken);
        _logger.LogInformation("FileWatcherService stopped.");
    }

    /// <inheritdoc />
    public Task StartWatchingAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        lock (_lock)
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(FileWatcherService));
            }

            // If already watching a different path, stop first
            if (_watcher != null)
            {
                _logger.LogInformation(
                    "Stopping current watcher on {CurrentPath} to switch to {NewPath}",
                    _watchedPath,
                    path);

                DisposeWatcher();
            }

            if (!Directory.Exists(path))
            {
                _logger.LogWarning(
                    "Watch path does not exist: {Path}. Creating directory.",
                    path);
                Directory.CreateDirectory(path);
            }

            _watcher = CreateWatcher(path);
            _watchedPath = path;
            _watcher.EnableRaisingEvents = true;

            _logger.LogInformation(
                "Started watching directory: {Path} (Recursive: {Recursive}, Filter: {Filter})",
                path,
                _options.IncludeSubdirectories,
                _options.FileFilter);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopWatchingAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_watcher == null)
            {
                return Task.CompletedTask;
            }

            _logger.LogInformation("Stopping file watcher for: {Path}", _watchedPath);
            DisposeWatcher();
            _watchedPath = null;
        }

        return Task.CompletedTask;
    }

    private FileSystemWatcher CreateWatcher(string path)
    {
        var watcher = new FileSystemWatcher(path)
        {
            Filter = _options.FileFilter,
            IncludeSubdirectories = _options.IncludeSubdirectories,
            InternalBufferSize = _options.InternalBufferSize,
            NotifyFilter = NotifyFilters.FileName
                         | NotifyFilters.LastWrite
                         | NotifyFilters.Size
                         | NotifyFilters.DirectoryName
        };

        // Subscribe to file system events
        watcher.Created += OnCreated;
        watcher.Changed += OnChanged;
        watcher.Deleted += OnDeleted;
        watcher.Renamed += OnRenamed;
        watcher.Error += OnError;

        return watcher;
    }

    private void OnCreated(object sender, FileSystemEventArgs e)
    {
        if (IsDirectory(e.FullPath))
        {
            _logger.LogDebug("Directory created event ignored: {Path}", e.FullPath);
            return;
        }

        _logger.LogDebug("File created: {Path}", e.FullPath);

        // Fire and forget with error handling - actual processing happens in handler
        _ = SafeInvokeAsync(() => _changeHandler.HandleFileCreatedAsync(e.FullPath));
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        if (e.ChangeType != WatcherChangeTypes.Changed)
        {
            return;
        }

        if (IsDirectory(e.FullPath))
        {
            _logger.LogDebug("Directory changed event ignored: {Path}", e.FullPath);
            return;
        }

        _logger.LogDebug("File changed: {Path}", e.FullPath);

        _ = SafeInvokeAsync(() => _changeHandler.HandleFileChangedAsync(e.FullPath));
    }

    private void OnDeleted(object sender, FileSystemEventArgs e)
    {
        // Cannot check if directory since it no longer exists
        _logger.LogDebug("File/directory deleted: {Path}", e.FullPath);

        _ = SafeInvokeAsync(() => _changeHandler.HandleFileDeletedAsync(e.FullPath));
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        if (IsDirectory(e.FullPath))
        {
            _logger.LogDebug("Directory renamed event ignored: {OldPath} -> {NewPath}",
                e.OldFullPath, e.FullPath);
            return;
        }

        _logger.LogDebug("File renamed: {OldPath} -> {NewPath}", e.OldFullPath, e.FullPath);

        _ = SafeInvokeAsync(() => _changeHandler.HandleFileRenamedAsync(e.OldFullPath, e.FullPath));
    }

    private void OnError(object sender, ErrorEventArgs e)
    {
        var exception = e.GetException();

        if (exception is InternalBufferOverflowException)
        {
            _logger.LogWarning(
                "FileSystemWatcher buffer overflow! Some file events may have been lost. " +
                "Consider triggering a full reconciliation.");

            _ = SafeInvokeAsync(() => _changeHandler.HandleBufferOverflowAsync());
        }
        else
        {
            _logger.LogError(exception, "FileSystemWatcher error occurred");
        }
    }

    private async Task SafeInvokeAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error invoking file change handler");
        }
    }

    private static bool IsDirectory(string path)
    {
        try
        {
            return Directory.Exists(path);
        }
        catch
        {
            return false;
        }
    }

    private void DisposeWatcher()
    {
        if (_watcher == null) return;

        _watcher.EnableRaisingEvents = false;
        _watcher.Created -= OnCreated;
        _watcher.Changed -= OnChanged;
        _watcher.Deleted -= OnDeleted;
        _watcher.Renamed -= OnRenamed;
        _watcher.Error -= OnError;
        _watcher.Dispose();
        _watcher = null;
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_isDisposed) return;

            DisposeWatcher();
            _isDisposed = true;
        }
    }
}
```

### 5. Project Activation Integration

Integration with project activation to start/stop watching:

```csharp
// src/CompoundDocs.McpServer/Services/FileWatcherProjectActivationHandler.cs
namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Handles file watcher lifecycle in response to project activation/deactivation.
/// </summary>
public sealed class FileWatcherProjectActivationHandler : IProjectActivationHandler
{
    private readonly IFileWatcherService _fileWatcher;
    private readonly IOptions<FileWatcherOptions> _options;
    private readonly ILogger<FileWatcherProjectActivationHandler> _logger;

    public FileWatcherProjectActivationHandler(
        IFileWatcherService fileWatcher,
        IOptions<FileWatcherOptions> options,
        ILogger<FileWatcherProjectActivationHandler> logger)
    {
        _fileWatcher = fileWatcher;
        _options = options;
        _logger = logger;
    }

    public int Order => 100; // Run after core project activation

    public async Task OnProjectActivatedAsync(
        ProjectContext context,
        CancellationToken cancellationToken = default)
    {
        var docsPath = Path.Combine(
            context.RootPath,
            _options.Value.CompoundingDocsDirectoryName);

        _logger.LogInformation(
            "Project activated: {ProjectName}. Starting file watcher for: {DocsPath}",
            context.ProjectName,
            docsPath);

        await _fileWatcher.StartWatchingAsync(docsPath, cancellationToken);
    }

    public async Task OnProjectDeactivatingAsync(
        ProjectContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Project deactivating: {ProjectName}. Stopping file watcher.",
            context.ProjectName);

        await _fileWatcher.StopWatchingAsync(cancellationToken);
    }
}

/// <summary>
/// Handler interface for project activation events.
/// </summary>
public interface IProjectActivationHandler
{
    /// <summary>
    /// Order of execution (lower runs first).
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Called when a project is activated.
    /// </summary>
    Task OnProjectActivatedAsync(ProjectContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Called when a project is about to be deactivated.
    /// </summary>
    Task OnProjectDeactivatingAsync(ProjectContext context, CancellationToken cancellationToken = default);
}

/// <summary>
/// Context information for an activated project.
/// </summary>
public record ProjectContext(
    string ProjectName,
    string RootPath,
    string BranchName);
```

### 6. Null File Change Handler (Placeholder)

A placeholder implementation until the debouncing/processing is implemented in Phase 054:

```csharp
// src/CompoundDocs.McpServer/Services/NullFileChangeHandler.cs
namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Placeholder file change handler that logs events.
/// Will be replaced with debounced handler in Phase 054.
/// </summary>
public sealed class NullFileChangeHandler : IFileChangeHandler
{
    private readonly ILogger<NullFileChangeHandler> _logger;

    public NullFileChangeHandler(ILogger<NullFileChangeHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleFileCreatedAsync(string fullPath, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("File created (pending processing): {Path}", fullPath);
        return Task.CompletedTask;
    }

    public Task HandleFileChangedAsync(string fullPath, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("File changed (pending processing): {Path}", fullPath);
        return Task.CompletedTask;
    }

    public Task HandleFileDeletedAsync(string fullPath, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("File deleted (pending processing): {Path}", fullPath);
        return Task.CompletedTask;
    }

    public Task HandleFileRenamedAsync(string oldPath, string newPath, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("File renamed (pending processing): {OldPath} -> {NewPath}", oldPath, newPath);
        return Task.CompletedTask;
    }

    public Task HandleBufferOverflowAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("Buffer overflow detected. Full reconciliation needed (pending implementation).");
        return Task.CompletedTask;
    }
}
```

### 7. Service Registration

```csharp
// In ServiceCollectionExtensions.cs
public static IServiceCollection AddFileWatcherServices(
    this IServiceCollection services,
    IConfiguration configuration)
{
    // Configuration
    services.Configure<FileWatcherOptions>(
        configuration.GetSection(FileWatcherOptions.SectionName));

    // File change handler (placeholder until Phase 054)
    services.AddSingleton<IFileChangeHandler, NullFileChangeHandler>();

    // File watcher service
    services.AddSingleton<FileWatcherService>();
    services.AddSingleton<IFileWatcherService>(sp =>
        sp.GetRequiredService<FileWatcherService>());

    // Register as hosted service for lifecycle management
    services.AddHostedService(sp =>
        sp.GetRequiredService<FileWatcherService>());

    // Project activation handler
    services.AddSingleton<IProjectActivationHandler, FileWatcherProjectActivationHandler>();

    return services;
}
```

### 8. Configuration Example

```json
// appsettings.json
{
  "FileWatcher": {
    "CompoundingDocsDirectoryName": "csharp-compounding-docs",
    "FileFilter": "*.md",
    "InternalBufferSize": 65536,
    "IncludeSubdirectories": true,
    "StopTimeout": "00:00:05"
  }
}
```

---

## Dependencies

### Depends On

- **Phase 023**: MCP Server Lifecycle Management - Health tracking and shutdown coordination
- **Phase 048**: Document Processing Pipeline - Processing infrastructure (if implemented)

### Blocks

- **Phase 054**: Debounced Event Processing - Implements actual debouncing logic
- **Phase 055**: Startup Reconciliation - Full sync on project activation
- **Phase 056**: Document Indexing Integration - Connects file events to embedding pipeline

---

## Testing Verification

### Unit Tests

```csharp
// tests/CompoundDocs.Tests/Services/FileWatcherServiceTests.cs
public class FileWatcherServiceTests
{
    private readonly Mock<IFileChangeHandler> _handlerMock;
    private readonly Mock<ILogger<FileWatcherService>> _loggerMock;
    private readonly FileWatcherService _service;
    private readonly string _testDir;

    public FileWatcherServiceTests()
    {
        _handlerMock = new Mock<IFileChangeHandler>();
        _loggerMock = new Mock<ILogger<FileWatcherService>>();
        var options = Options.Create(new FileWatcherOptions());

        _service = new FileWatcherService(_handlerMock.Object, options, _loggerMock.Object);
        _testDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDir);
    }

    [Fact]
    public async Task StartWatchingAsync_SetsIsWatchingTrue()
    {
        // Act
        await _service.StartWatchingAsync(_testDir);

        // Assert
        Assert.True(_service.IsWatching);
        Assert.Equal(_testDir, _service.WatchedPath);
    }

    [Fact]
    public async Task StopWatchingAsync_SetsIsWatchingFalse()
    {
        // Arrange
        await _service.StartWatchingAsync(_testDir);

        // Act
        await _service.StopWatchingAsync();

        // Assert
        Assert.False(_service.IsWatching);
        Assert.Null(_service.WatchedPath);
    }

    [Fact]
    public async Task StartWatchingAsync_CreatesDirectoryIfNotExists()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_testDir, "non-existent");

        // Act
        await _service.StartWatchingAsync(nonExistentPath);

        // Assert
        Assert.True(Directory.Exists(nonExistentPath));
    }

    [Fact]
    public async Task StartWatchingAsync_StopsExistingWatcherFirst()
    {
        // Arrange
        var secondDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(secondDir);

        await _service.StartWatchingAsync(_testDir);

        // Act
        await _service.StartWatchingAsync(secondDir);

        // Assert
        Assert.Equal(secondDir, _service.WatchedPath);
    }

    [Fact]
    public async Task FileCreated_InvokesChangeHandler()
    {
        // Arrange
        var tcs = new TaskCompletionSource();
        _handlerMock
            .Setup(h => h.HandleFileCreatedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                tcs.SetResult();
                return Task.CompletedTask;
            });

        await _service.StartWatchingAsync(_testDir);

        // Act
        var filePath = Path.Combine(_testDir, "test.md");
        await File.WriteAllTextAsync(filePath, "# Test");

        // Assert
        var completed = await Task.WhenAny(tcs.Task, Task.Delay(5000));
        Assert.Equal(tcs.Task, completed);

        _handlerMock.Verify(
            h => h.HandleFileCreatedAsync(filePath, It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task Dispose_StopsWatcherAndCleansUp()
    {
        // Arrange
        await _service.StartWatchingAsync(_testDir);

        // Act
        _service.Dispose();

        // Assert
        Assert.False(_service.IsWatching);
    }

    [Fact]
    public async Task StartWatchingAsync_ThrowsWhenDisposed()
    {
        // Arrange
        _service.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => _service.StartWatchingAsync(_testDir));
    }
}
```

### Integration Tests

```csharp
// tests/CompoundDocs.IntegrationTests/Services/FileWatcherIntegrationTests.cs
[Trait("Category", "Integration")]
public class FileWatcherIntegrationTests : IDisposable
{
    private readonly string _testDir;
    private readonly FileWatcherService _service;
    private readonly List<string> _createdFiles = new();
    private readonly List<string> _changedFiles = new();
    private readonly List<string> _deletedFiles = new();

    public FileWatcherIntegrationTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"filewatcher-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);

        var handler = new TestFileChangeHandler(
            path => _createdFiles.Add(path),
            path => _changedFiles.Add(path),
            path => _deletedFiles.Add(path));

        var options = Options.Create(new FileWatcherOptions { FileFilter = "*.md" });
        var logger = NullLogger<FileWatcherService>.Instance;

        _service = new FileWatcherService(handler, options, logger);
    }

    [Fact]
    public async Task WatchesRecursiveSubdirectories()
    {
        // Arrange
        var subDir = Path.Combine(_testDir, "sub1", "sub2");
        Directory.CreateDirectory(subDir);

        await _service.StartWatchingAsync(_testDir);

        // Act
        var filePath = Path.Combine(subDir, "nested.md");
        await File.WriteAllTextAsync(filePath, "# Nested file");
        await Task.Delay(500); // Allow event to propagate

        // Assert
        Assert.Contains(_createdFiles, f => f == filePath);
    }

    [Fact]
    public async Task FiltersNonMarkdownFiles()
    {
        // Arrange
        await _service.StartWatchingAsync(_testDir);

        // Act
        var txtPath = Path.Combine(_testDir, "test.txt");
        await File.WriteAllTextAsync(txtPath, "Not markdown");
        await Task.Delay(500);

        // Assert
        Assert.DoesNotContain(_createdFiles, f => f == txtPath);
    }

    public void Dispose()
    {
        _service.Dispose();
        try { Directory.Delete(_testDir, recursive: true); } catch { }
    }

    private class TestFileChangeHandler : IFileChangeHandler
    {
        private readonly Action<string> _onCreate;
        private readonly Action<string> _onChange;
        private readonly Action<string> _onDelete;

        public TestFileChangeHandler(
            Action<string> onCreate,
            Action<string> onChange,
            Action<string> onDelete)
        {
            _onCreate = onCreate;
            _onChange = onChange;
            _onDelete = onDelete;
        }

        public Task HandleFileCreatedAsync(string fullPath, CancellationToken ct)
        {
            _onCreate(fullPath);
            return Task.CompletedTask;
        }

        public Task HandleFileChangedAsync(string fullPath, CancellationToken ct)
        {
            _onChange(fullPath);
            return Task.CompletedTask;
        }

        public Task HandleFileDeletedAsync(string fullPath, CancellationToken ct)
        {
            _onDelete(fullPath);
            return Task.CompletedTask;
        }

        public Task HandleFileRenamedAsync(string oldPath, string newPath, CancellationToken ct)
            => Task.CompletedTask;

        public Task HandleBufferOverflowAsync(CancellationToken ct)
            => Task.CompletedTask;
    }
}
```

---

## Files Created/Modified

| File | Action | Description |
|------|--------|-------------|
| `src/CompoundDocs.McpServer/Services/IFileWatcherService.cs` | Create | File watcher interface |
| `src/CompoundDocs.McpServer/Services/IFileChangeHandler.cs` | Create | Change handler interface |
| `src/CompoundDocs.McpServer/Services/FileWatcherService.cs` | Create | Main watcher implementation |
| `src/CompoundDocs.McpServer/Services/NullFileChangeHandler.cs` | Create | Placeholder handler |
| `src/CompoundDocs.McpServer/Services/FileWatcherProjectActivationHandler.cs` | Create | Project activation integration |
| `src/CompoundDocs.McpServer/Services/IProjectActivationHandler.cs` | Create | Activation handler interface |
| `src/CompoundDocs.McpServer/Configuration/FileWatcherOptions.cs` | Create | Configuration options |
| `src/CompoundDocs.McpServer/Extensions/ServiceCollectionExtensions.cs` | Modify | Add file watcher registration |
| `tests/CompoundDocs.Tests/Services/FileWatcherServiceTests.cs` | Create | Unit tests |
| `tests/CompoundDocs.IntegrationTests/Services/FileWatcherIntegrationTests.cs` | Create | Integration tests |

---

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| Buffer overflow on rapid changes | Increased buffer to 64KB, handler logs overflow for reconciliation |
| Platform-specific behavior differences | Documented in research; integration tests on all platforms |
| Event callbacks block watcher | Fire-and-forget with SafeInvokeAsync wrapping |
| Directory doesn't exist on startup | Auto-create directory when starting watch |
| Race condition on start/stop | Thread-safe locking around watcher operations |
| Memory leak from unsubscribed events | Explicit unsubscribe in DisposeWatcher method |
| Watcher used after disposal | ObjectDisposedException thrown with clear message |

---

## Cross-Platform Considerations

| Platform | Behavior | Notes |
|----------|----------|-------|
| **Windows** | Most reliable, ReadDirectoryChangesW API | Best event accuracy |
| **Linux** | inotify backend | May hit system limits (`fs.inotify.max_user_watches`) |
| **macOS** | FSEvents backend | Slower startup, potential extra events |

Refer to [research/dotnet-file-watcher-embeddings-research.md](../research/dotnet-file-watcher-embeddings-research.md) for detailed platform-specific guidance and workarounds.
