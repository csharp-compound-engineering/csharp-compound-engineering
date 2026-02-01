using System;
using System.Threading;
using System.Threading.Tasks;

namespace CompoundDocs.McpServer.Services.FileWatcher;

/// <summary>
/// Interface for file system watcher operations.
/// Monitors document changes for automatic synchronization.
/// </summary>
public interface IFileWatcherService : IDisposable
{
    /// <summary>
    /// Gets whether the file watcher is currently active.
    /// </summary>
    bool IsWatching { get; }

    /// <summary>
    /// Gets the path currently being watched.
    /// Null if not watching.
    /// </summary>
    string? WatchedPath { get; }

    /// <summary>
    /// Starts watching for file changes in the specified project path.
    /// </summary>
    /// <param name="projectPath">The absolute path to the project root to watch.</param>
    /// <exception cref="ArgumentNullException">Thrown when projectPath is null.</exception>
    /// <exception cref="ArgumentException">Thrown when projectPath is empty or whitespace.</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown when the project path does not exist.</exception>
    /// <exception cref="InvalidOperationException">Thrown when already watching a path.</exception>
    void StartWatching(string projectPath);

    /// <summary>
    /// Stops watching for file changes.
    /// Safe to call even if not currently watching.
    /// </summary>
    void StopWatching();

    /// <summary>
    /// Performs reconciliation between disk files and the database.
    /// Scans for new, modified, and deleted files.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task containing the reconciliation result.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no project path is being watched.</exception>
    Task<ReconciliationResult> ReconcileAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Event raised when a file change is detected.
    /// </summary>
    event EventHandler<FileChangeEvent>? FileChanged;
}
