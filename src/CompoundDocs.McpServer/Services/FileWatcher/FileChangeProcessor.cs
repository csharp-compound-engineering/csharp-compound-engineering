using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace CompoundDocs.McpServer.Services.FileWatcher;

/// <summary>
/// Provides simplified document indexing and deletion operations for file watcher use.
/// This interface is specifically designed for the file watcher subsystem and returns
/// simple boolean success/failure indicators.
/// </summary>
public interface IFileWatcherDocumentIndexer
{
    /// <summary>
    /// Indexes a document file.
    /// </summary>
    /// <param name="filePath">The absolute path to the document file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if indexing was successful, false otherwise.</returns>
    Task<bool> IndexDocumentAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a document from the index.
    /// </summary>
    /// <param name="filePath">The absolute path to the document file.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if deletion was successful, false otherwise.</returns>
    Task<bool> DeleteDocumentAsync(string filePath, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of processing a file change event.
/// </summary>
/// <param name="Success">Whether the processing was successful.</param>
/// <param name="FilePath">The file path that was processed.</param>
/// <param name="ChangeType">The type of change that was processed.</param>
/// <param name="ErrorMessage">Error message if processing failed.</param>
public sealed record FileChangeProcessingResult(
    bool Success,
    string FilePath,
    FileChangeType ChangeType,
    string? ErrorMessage = null);

/// <summary>
/// Processes file change events by invoking appropriate indexing operations.
/// </summary>
public sealed class FileChangeProcessor
{
    private readonly ILogger<FileChangeProcessor> _logger;
    private readonly IFileWatcherDocumentIndexer _documentIndexer;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileChangeProcessor"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="documentIndexer">The document indexer service.</param>
    public FileChangeProcessor(
        ILogger<FileChangeProcessor> logger,
        IFileWatcherDocumentIndexer documentIndexer)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _documentIndexer = documentIndexer ?? throw new ArgumentNullException(nameof(documentIndexer));
    }

    /// <summary>
    /// Processes a file change event.
    /// </summary>
    /// <param name="changeEvent">The file change event to process.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of processing results (may contain multiple for renames).</returns>
    public async Task<FileChangeProcessingResult[]> ProcessAsync(
        FileChangeEvent changeEvent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(changeEvent);

        _logger.LogDebug(
            "Processing file change: {ChangeType} for {FilePath}",
            changeEvent.ChangeType,
            changeEvent.FilePath);

        return changeEvent.ChangeType switch
        {
            FileChangeType.Created => await ProcessCreatedAsync(changeEvent, cancellationToken).ConfigureAwait(false),
            FileChangeType.Modified => await ProcessModifiedAsync(changeEvent, cancellationToken).ConfigureAwait(false),
            FileChangeType.Deleted => await ProcessDeletedAsync(changeEvent, cancellationToken).ConfigureAwait(false),
            FileChangeType.Renamed => await ProcessRenamedAsync(changeEvent, cancellationToken).ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(changeEvent), $"Unknown change type: {changeEvent.ChangeType}")
        };
    }

    /// <summary>
    /// Processes multiple file change events.
    /// </summary>
    /// <param name="changeEvents">The file change events to process.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of all processing results.</returns>
    public async Task<FileChangeProcessingResult[]> ProcessManyAsync(
        FileChangeEvent[] changeEvents,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(changeEvents);

        var results = new System.Collections.Generic.List<FileChangeProcessingResult>();

        foreach (var changeEvent in changeEvents)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var eventResults = await ProcessAsync(changeEvent, cancellationToken).ConfigureAwait(false);
            results.AddRange(eventResults);
        }

        return results.ToArray();
    }

    private async Task<FileChangeProcessingResult[]> ProcessCreatedAsync(
        FileChangeEvent changeEvent,
        CancellationToken cancellationToken)
    {
        try
        {
            var success = await _documentIndexer.IndexDocumentAsync(changeEvent.FilePath, cancellationToken)
                .ConfigureAwait(false);

            if (success)
            {
                _logger.LogInformation("Successfully indexed new document: {FilePath}", changeEvent.FilePath);
            }
            else
            {
                _logger.LogWarning("Failed to index new document: {FilePath}", changeEvent.FilePath);
            }

            return
            [
                new FileChangeProcessingResult(success, changeEvent.FilePath, FileChangeType.Created)
            ];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error indexing new document: {FilePath}", changeEvent.FilePath);
            return
            [
                new FileChangeProcessingResult(false, changeEvent.FilePath, FileChangeType.Created, ex.Message)
            ];
        }
    }

    private async Task<FileChangeProcessingResult[]> ProcessModifiedAsync(
        FileChangeEvent changeEvent,
        CancellationToken cancellationToken)
    {
        try
        {
            // For modifications, we re-index the document
            var success = await _documentIndexer.IndexDocumentAsync(changeEvent.FilePath, cancellationToken)
                .ConfigureAwait(false);

            if (success)
            {
                _logger.LogInformation("Successfully re-indexed modified document: {FilePath}", changeEvent.FilePath);
            }
            else
            {
                _logger.LogWarning("Failed to re-index modified document: {FilePath}", changeEvent.FilePath);
            }

            return
            [
                new FileChangeProcessingResult(success, changeEvent.FilePath, FileChangeType.Modified)
            ];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error re-indexing modified document: {FilePath}", changeEvent.FilePath);
            return
            [
                new FileChangeProcessingResult(false, changeEvent.FilePath, FileChangeType.Modified, ex.Message)
            ];
        }
    }

    private async Task<FileChangeProcessingResult[]> ProcessDeletedAsync(
        FileChangeEvent changeEvent,
        CancellationToken cancellationToken)
    {
        try
        {
            var success = await _documentIndexer.DeleteDocumentAsync(changeEvent.FilePath, cancellationToken)
                .ConfigureAwait(false);

            if (success)
            {
                _logger.LogInformation("Successfully removed deleted document from index: {FilePath}", changeEvent.FilePath);
            }
            else
            {
                _logger.LogWarning("Failed to remove deleted document from index: {FilePath}", changeEvent.FilePath);
            }

            return
            [
                new FileChangeProcessingResult(success, changeEvent.FilePath, FileChangeType.Deleted)
            ];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing deleted document from index: {FilePath}", changeEvent.FilePath);
            return
            [
                new FileChangeProcessingResult(false, changeEvent.FilePath, FileChangeType.Deleted, ex.Message)
            ];
        }
    }

    private async Task<FileChangeProcessingResult[]> ProcessRenamedAsync(
        FileChangeEvent changeEvent,
        CancellationToken cancellationToken)
    {
        // Handle rename as delete old + create new
        var results = new System.Collections.Generic.List<FileChangeProcessingResult>();

        // Delete old path if we have it
        if (!string.IsNullOrEmpty(changeEvent.OldPath))
        {
            try
            {
                var deleteSuccess = await _documentIndexer.DeleteDocumentAsync(changeEvent.OldPath, cancellationToken)
                    .ConfigureAwait(false);

                results.Add(new FileChangeProcessingResult(
                    deleteSuccess,
                    changeEvent.OldPath,
                    FileChangeType.Deleted,
                    deleteSuccess ? null : "Failed to delete old path during rename"));

                if (deleteSuccess)
                {
                    _logger.LogDebug("Deleted old path during rename: {OldPath}", changeEvent.OldPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting old path during rename: {OldPath}", changeEvent.OldPath);
                results.Add(new FileChangeProcessingResult(false, changeEvent.OldPath, FileChangeType.Deleted, ex.Message));
            }
        }

        // Index new path
        try
        {
            var indexSuccess = await _documentIndexer.IndexDocumentAsync(changeEvent.FilePath, cancellationToken)
                .ConfigureAwait(false);

            results.Add(new FileChangeProcessingResult(
                indexSuccess,
                changeEvent.FilePath,
                FileChangeType.Created,
                indexSuccess ? null : "Failed to index new path during rename"));

            if (indexSuccess)
            {
                _logger.LogInformation(
                    "Successfully processed rename: {OldPath} -> {NewPath}",
                    changeEvent.OldPath,
                    changeEvent.FilePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error indexing new path during rename: {FilePath}", changeEvent.FilePath);
            results.Add(new FileChangeProcessingResult(false, changeEvent.FilePath, FileChangeType.Created, ex.Message));
        }

        return results.ToArray();
    }
}

/// <summary>
/// Stub implementation of <see cref="IFileWatcherDocumentIndexer"/> for testing without database dependencies.
/// For production use, <see cref="DatabaseDocumentIndexer"/> is registered via DI.
/// </summary>
public sealed class StubDocumentIndexer : IFileWatcherDocumentIndexer
{
    private readonly ILogger<StubDocumentIndexer> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="StubDocumentIndexer"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    public StubDocumentIndexer(ILogger<StubDocumentIndexer> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task<bool> IndexDocumentAsync(string filePath, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Stub: Simulating index of document at {FilePath}", filePath);
        // Stub always returns success for testing
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task<bool> DeleteDocumentAsync(string filePath, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Stub: Simulating deletion of document at {FilePath}", filePath);
        // Stub always returns success for testing
        return Task.FromResult(true);
    }
}
