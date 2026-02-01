using System;
using System.Threading;
using System.Threading.Tasks;
using CompoundDocs.McpServer.Session;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CompoundDocs.McpServer.Services.FileWatcher;

/// <summary>
/// Background service that hosts the file watcher.
/// Started/stopped by project activation events.
/// </summary>
public sealed class FileWatcherBackgroundService : BackgroundService, IDisposable
{
    private readonly ILogger<FileWatcherBackgroundService> _logger;
    private readonly IFileWatcherService _fileWatcherService;
    private readonly FileChangeProcessor _fileChangeProcessor;
    private readonly ISessionContext _sessionContext;

    private readonly SemaphoreSlim _processingSemaphore = new(1, 1);
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileWatcherBackgroundService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="fileWatcherService">The file watcher service.</param>
    /// <param name="fileChangeProcessor">The file change processor.</param>
    /// <param name="sessionContext">The session context.</param>
    public FileWatcherBackgroundService(
        ILogger<FileWatcherBackgroundService> logger,
        IFileWatcherService fileWatcherService,
        FileChangeProcessor fileChangeProcessor,
        ISessionContext sessionContext)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _fileWatcherService = fileWatcherService ?? throw new ArgumentNullException(nameof(fileWatcherService));
        _fileChangeProcessor = fileChangeProcessor ?? throw new ArgumentNullException(nameof(fileChangeProcessor));
        _sessionContext = sessionContext ?? throw new ArgumentNullException(nameof(sessionContext));
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("FileWatcherBackgroundService starting");

        // Subscribe to file change events
        _fileWatcherService.FileChanged += OnFileChanged;

        try
        {
            // Monitor session context for project activation changes
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken).ConfigureAwait(false);

                // Check if project activation status changed
                await UpdateWatcherStateAsync(stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Expected during shutdown
            _logger.LogDebug("FileWatcherBackgroundService stopping due to cancellation");
        }
        finally
        {
            _fileWatcherService.FileChanged -= OnFileChanged;
            _fileWatcherService.StopWatching();
        }
    }

    /// <inheritdoc />
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("FileWatcherBackgroundService starting up");
        await base.StartAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("FileWatcherBackgroundService shutting down");

        // Stop watching
        _fileWatcherService.StopWatching();

        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Starts watching the specified project path.
    /// </summary>
    /// <param name="projectPath">The project path to watch.</param>
    public void StartWatching(string projectPath)
    {
        ArgumentNullException.ThrowIfNull(projectPath);

        try
        {
            // Stop existing watcher if any
            if (_fileWatcherService.IsWatching)
            {
                _logger.LogDebug("Stopping existing file watcher before starting new one");
                _fileWatcherService.StopWatching();
            }

            _fileWatcherService.StartWatching(projectPath);
            _logger.LogInformation("Started file watcher for project: {ProjectPath}", projectPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start file watcher for project: {ProjectPath}", projectPath);
            throw;
        }
    }

    /// <summary>
    /// Stops watching for file changes.
    /// </summary>
    public void StopWatching()
    {
        try
        {
            _fileWatcherService.StopWatching();
            _logger.LogInformation("Stopped file watcher");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping file watcher");
        }
    }

    /// <summary>
    /// Performs initial reconciliation for the watched project.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The reconciliation result.</returns>
    public async Task<ReconciliationResult> ReconcileAsync(CancellationToken cancellationToken = default)
    {
        if (!_fileWatcherService.IsWatching)
        {
            throw new InvalidOperationException("File watcher is not active. Start watching a project first.");
        }

        return await _fileWatcherService.ReconcileAsync(cancellationToken).ConfigureAwait(false);
    }

    private async void OnFileChanged(object? sender, FileChangeEvent e)
    {
        // Use semaphore to prevent concurrent processing
        if (!await _processingSemaphore.WaitAsync(0).ConfigureAwait(false))
        {
            _logger.LogDebug("Skipping file change event processing; another event is being processed");
            return;
        }

        try
        {
            _logger.LogInformation(
                "File change detected: {ChangeType} - {FilePath}",
                e.ChangeType,
                e.FilePath);

            var results = await _fileChangeProcessor.ProcessAsync(e).ConfigureAwait(false);

            foreach (var result in results)
            {
                if (result.Success)
                {
                    _logger.LogDebug(
                        "Successfully processed {ChangeType} for {FilePath}",
                        result.ChangeType,
                        result.FilePath);
                }
                else
                {
                    _logger.LogWarning(
                        "Failed to process {ChangeType} for {FilePath}: {Error}",
                        result.ChangeType,
                        result.FilePath,
                        result.ErrorMessage);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing file change event for {FilePath}", e.FilePath);
        }
        finally
        {
            _processingSemaphore.Release();
        }
    }

    private async Task UpdateWatcherStateAsync(CancellationToken cancellationToken)
    {
        try
        {
            var isProjectActive = _sessionContext.IsProjectActive;
            var projectPath = _sessionContext.ActiveProjectPath;

            if (isProjectActive && !string.IsNullOrEmpty(projectPath))
            {
                // Project is active - should be watching
                if (!_fileWatcherService.IsWatching || _fileWatcherService.WatchedPath != projectPath)
                {
                    _logger.LogDebug("Project activated, starting file watcher for: {ProjectPath}", projectPath);
                    StartWatching(projectPath);

                    // Perform initial reconciliation
                    try
                    {
                        var reconcileResult = await _fileWatcherService.ReconcileAsync(cancellationToken).ConfigureAwait(false);
                        if (reconcileResult.HasChanges)
                        {
                            _logger.LogInformation(
                                "Initial reconciliation found {Count} changes",
                                reconcileResult.TotalActions);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Initial reconciliation failed");
                    }
                }
            }
            else
            {
                // No active project - should not be watching
                if (_fileWatcherService.IsWatching)
                {
                    _logger.LogDebug("Project deactivated, stopping file watcher");
                    StopWatching();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating watcher state");
        }
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _processingSemaphore.Dispose();
        _fileWatcherService.Dispose();
        base.Dispose();
    }
}
