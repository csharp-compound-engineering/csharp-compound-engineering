using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CompoundDocs.Common.Configuration;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Logging;

namespace CompoundDocs.McpServer.Services.FileWatcher;

/// <summary>
/// File system watcher service that monitors document changes for automatic synchronization.
/// Uses debouncing to prevent excessive events and supports include/exclude patterns.
/// </summary>
public sealed class FileWatcherService : IFileWatcherService
{
    private readonly ILogger<FileWatcherService> _logger;
    private readonly FileReconciliationService _reconciliationService;
    private readonly FileWatcherSettings _settings;

    private FileSystemWatcher? _watcher;
    private Timer? _debounceTimer;
    private readonly ConcurrentDictionary<string, FileChangeEvent> _pendingChanges = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _syncLock = new();
    private bool _disposed;
    private string? _watchedPath;

    /// <inheritdoc />
    public bool IsWatching => _watcher != null && _watcher.EnableRaisingEvents;

    /// <inheritdoc />
    public string? WatchedPath => _watchedPath;

    /// <inheritdoc />
    public event EventHandler<FileChangeEvent>? FileChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileWatcherService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="reconciliationService">The file reconciliation service.</param>
    /// <param name="settings">The file watcher settings.</param>
    public FileWatcherService(
        ILogger<FileWatcherService> logger,
        FileReconciliationService reconciliationService,
        FileWatcherSettings settings)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _reconciliationService = reconciliationService ?? throw new ArgumentNullException(nameof(reconciliationService));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <inheritdoc />
    public void StartWatching(string projectPath)
    {
        ArgumentNullException.ThrowIfNull(projectPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);

        if (!Directory.Exists(projectPath))
        {
            throw new DirectoryNotFoundException($"Project path does not exist: {projectPath}");
        }

        lock (_syncLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (IsWatching)
            {
                throw new InvalidOperationException($"Already watching path: {_watchedPath}. Call StopWatching first.");
            }

            _watchedPath = Path.GetFullPath(projectPath);

            _watcher = new FileSystemWatcher(_watchedPath)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName
                             | NotifyFilters.DirectoryName
                             | NotifyFilters.LastWrite
                             | NotifyFilters.Size,
                Filter = "*.*" // We filter more specifically in the event handlers
            };

            // Subscribe to file system events
            _watcher.Created += OnFileCreated;
            _watcher.Changed += OnFileChanged;
            _watcher.Deleted += OnFileDeleted;
            _watcher.Renamed += OnFileRenamed;
            _watcher.Error += OnWatcherError;

            // Start watching
            _watcher.EnableRaisingEvents = true;

            _logger.LogInformation("Started watching for file changes in {Path}", _watchedPath);
        }
    }

    /// <inheritdoc />
    public void StopWatching()
    {
        lock (_syncLock)
        {
            if (_watcher == null)
            {
                return;
            }

            _watcher.EnableRaisingEvents = false;
            _watcher.Created -= OnFileCreated;
            _watcher.Changed -= OnFileChanged;
            _watcher.Deleted -= OnFileDeleted;
            _watcher.Renamed -= OnFileRenamed;
            _watcher.Error -= OnWatcherError;
            _watcher.Dispose();
            _watcher = null;

            _debounceTimer?.Dispose();
            _debounceTimer = null;

            var path = _watchedPath;
            _watchedPath = null;

            // Clear any pending changes
            _pendingChanges.Clear();

            _logger.LogInformation("Stopped watching for file changes in {Path}", path);
        }
    }

    /// <inheritdoc />
    public Task<ReconciliationResult> ReconcileAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrEmpty(_watchedPath))
        {
            throw new InvalidOperationException("No project path is being watched. Call StartWatching first.");
        }

        return _reconciliationService.ReconcileAsync(_watchedPath, cancellationToken);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        lock (_syncLock)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            StopWatching();
        }
    }

    private void OnFileCreated(object sender, FileSystemEventArgs e)
    {
        if (!ShouldProcessFile(e.FullPath))
        {
            return;
        }

        _logger.LogDebug("File created: {Path}; ChangeType={ChangeType}; Timestamp={Timestamp}",
            e.FullPath, FileChangeType.Created, DateTimeOffset.UtcNow);
        QueueChange(new FileChangeEvent(e.FullPath, FileChangeType.Created));
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        if (!ShouldProcessFile(e.FullPath))
        {
            return;
        }

        _logger.LogDebug("File modified: {Path}; ChangeType={ChangeType}; Timestamp={Timestamp}",
            e.FullPath, FileChangeType.Modified, DateTimeOffset.UtcNow);
        QueueChange(new FileChangeEvent(e.FullPath, FileChangeType.Modified));
    }

    private void OnFileDeleted(object sender, FileSystemEventArgs e)
    {
        if (!ShouldProcessFile(e.FullPath))
        {
            return;
        }

        _logger.LogDebug("File deleted: {Path}; ChangeType={ChangeType}; Timestamp={Timestamp}",
            e.FullPath, FileChangeType.Deleted, DateTimeOffset.UtcNow);
        QueueChange(new FileChangeEvent(e.FullPath, FileChangeType.Deleted));
    }

    private void OnFileRenamed(object sender, RenamedEventArgs e)
    {
        var shouldProcessOld = ShouldProcessFile(e.OldFullPath);
        var shouldProcessNew = ShouldProcessFile(e.FullPath);

        if (shouldProcessOld && shouldProcessNew)
        {
            // Both paths match patterns - treat as rename
            _logger.LogDebug("File renamed: {OldPath} -> {NewPath}; ChangeType={ChangeType}; Timestamp={Timestamp}",
                e.OldFullPath, e.FullPath, FileChangeType.Renamed, DateTimeOffset.UtcNow);
            QueueChange(new FileChangeEvent(e.FullPath, FileChangeType.Renamed, e.OldFullPath));
        }
        else if (shouldProcessOld)
        {
            // Old path matched but new doesn't - treat as delete
            _logger.LogDebug("File renamed out of scope: {OldPath} -> {NewPath}; ChangeType={ChangeType}; Timestamp={Timestamp}",
                e.OldFullPath, e.FullPath, FileChangeType.Deleted, DateTimeOffset.UtcNow);
            QueueChange(new FileChangeEvent(e.OldFullPath, FileChangeType.Deleted));
        }
        else if (shouldProcessNew)
        {
            // New path matches but old didn't - treat as create
            _logger.LogDebug("File renamed into scope: {OldPath} -> {NewPath}; ChangeType={ChangeType}; Timestamp={Timestamp}",
                e.OldFullPath, e.FullPath, FileChangeType.Created, DateTimeOffset.UtcNow);
            QueueChange(new FileChangeEvent(e.FullPath, FileChangeType.Created));
        }
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        var ex = e.GetException();
        _logger.LogError(ex, "File system watcher error occurred");

        // Attempt to restart the watcher if it failed
        if (_watchedPath != null && !_disposed)
        {
            _logger.LogInformation("Attempting to restart file watcher after error");
            try
            {
                var path = _watchedPath;
                StopWatching();
                StartWatching(path);
            }
            catch (Exception restartEx)
            {
                _logger.LogError(restartEx, "Failed to restart file watcher");
            }
        }
    }

    private void QueueChange(FileChangeEvent changeEvent)
    {
        // Use the file path as the key to coalesce multiple events for the same file
        _pendingChanges.AddOrUpdate(
            changeEvent.FilePath,
            changeEvent,
            (_, existing) => CoalesceEvents(existing, changeEvent));

        // Reset the debounce timer
        lock (_syncLock)
        {
            _debounceTimer?.Dispose();
            _debounceTimer = new Timer(
                ProcessPendingChanges,
                null,
                _settings.DebounceMs,
                Timeout.Infinite);
        }
    }

    private static FileChangeEvent CoalesceEvents(FileChangeEvent existing, FileChangeEvent incoming)
    {
        // Coalescing logic:
        // - If either is a delete, prefer delete
        // - If created then modified, keep as created
        // - If modified then modified, keep as modified
        // - Rename overrides all

        if (incoming.ChangeType == FileChangeType.Renamed)
        {
            return incoming;
        }

        if (incoming.ChangeType == FileChangeType.Deleted)
        {
            return incoming;
        }

        if (existing.ChangeType == FileChangeType.Created)
        {
            // Keep as created even if modified after
            return existing with { Timestamp = incoming.Timestamp };
        }

        return incoming;
    }

    private void ProcessPendingChanges(object? state)
    {
        if (_disposed)
        {
            return;
        }

        // Take all pending changes atomically
        var changes = _pendingChanges.ToArray();
        _pendingChanges.Clear();

        if (changes.Length > 0)
        {
            _logger.LogInformation("Processing {Count} pending file changes; WatchedPath={WatchedPath}",
                changes.Length, _watchedPath);
        }

        foreach (var (_, changeEvent) in changes)
        {
            try
            {
                _logger.LogDebug(
                    "Raising file change event: {ChangeType} for {Path}; EventTime={EventTime}",
                    changeEvent.ChangeType,
                    changeEvent.FilePath,
                    changeEvent.Timestamp);

                FileChanged?.Invoke(this, changeEvent);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing file change event for {Path}; ChangeType={ChangeType}",
                    changeEvent.FilePath, changeEvent.ChangeType);
            }
        }
    }

    private bool ShouldProcessFile(string filePath)
    {
        if (string.IsNullOrEmpty(_watchedPath))
        {
            return false;
        }

        // Get relative path from watched root
        var relativePath = Path.GetRelativePath(_watchedPath, filePath);

        // Normalize path separators for glob matching
        relativePath = relativePath.Replace(Path.DirectorySeparatorChar, '/');

        // Check exclude patterns first (they take precedence)
        if (_settings.ExcludePatterns.Count > 0)
        {
            var excludeMatcher = new Matcher(StringComparison.OrdinalIgnoreCase);
            foreach (var pattern in _settings.ExcludePatterns)
            {
                excludeMatcher.AddInclude(pattern);
            }

            if (excludeMatcher.Match(relativePath).HasMatches)
            {
                _logger.LogTrace("File excluded by pattern: {Path}", filePath);
                return false;
            }
        }

        // Check include patterns
        if (_settings.IncludePatterns.Count > 0)
        {
            var includeMatcher = new Matcher(StringComparison.OrdinalIgnoreCase);
            foreach (var pattern in _settings.IncludePatterns)
            {
                includeMatcher.AddInclude(pattern);
            }

            if (!includeMatcher.Match(relativePath).HasMatches)
            {
                _logger.LogTrace("File not matched by include patterns: {Path}", filePath);
                return false;
            }
        }

        return true;
    }
}
