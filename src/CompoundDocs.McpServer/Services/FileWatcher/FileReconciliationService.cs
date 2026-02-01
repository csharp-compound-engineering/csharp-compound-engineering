using System;
using System.Collections.Generic;
using System.IO;
using CompoundDocs.Common.Configuration;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Microsoft.Extensions.Logging;

namespace CompoundDocs.McpServer.Services.FileWatcher;

/// <summary>
/// Provides a service interface for accessing document records in the database.
/// This interface should be implemented by the data layer.
/// </summary>
public interface IDocumentRecordProvider
{
    /// <summary>
    /// Gets all document records for a project.
    /// </summary>
    /// <param name="projectPath">The project path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A dictionary of file paths to their last modified timestamps.</returns>
    Task<Dictionary<string, DateTimeOffset>> GetDocumentRecordsAsync(
        string projectPath,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a document record from the database.
/// </summary>
/// <param name="FilePath">The absolute file path.</param>
/// <param name="LastModified">The last modified timestamp in the database.</param>
/// <param name="ContentHash">Optional content hash for change detection.</param>
public sealed record DocumentRecord(
    string FilePath,
    DateTimeOffset LastModified,
    string? ContentHash = null);

/// <summary>
/// Service for reconciling file system state with database state.
/// Detects files that have been added, modified, or deleted.
/// </summary>
public sealed class FileReconciliationService
{
    private readonly ILogger<FileReconciliationService> _logger;
    private readonly IDocumentRecordProvider _documentRecordProvider;
    private readonly FileWatcherSettings _settings;

    /// <summary>
    /// Initializes a new instance of the <see cref="FileReconciliationService"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="documentRecordProvider">The document record provider.</param>
    /// <param name="settings">The file watcher settings.</param>
    public FileReconciliationService(
        ILogger<FileReconciliationService> logger,
        IDocumentRecordProvider documentRecordProvider,
        FileWatcherSettings settings)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _documentRecordProvider = documentRecordProvider ?? throw new ArgumentNullException(nameof(documentRecordProvider));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <summary>
    /// Performs reconciliation between disk files and database records.
    /// </summary>
    /// <param name="projectPath">The project path to reconcile.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A reconciliation result containing actions needed.</returns>
    public async Task<ReconciliationResult> ReconcileAsync(
        string projectPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(projectPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectPath);

        if (!Directory.Exists(projectPath))
        {
            throw new DirectoryNotFoundException($"Project path does not exist: {projectPath}");
        }

        projectPath = Path.GetFullPath(projectPath);

        _logger.LogInformation("Starting reconciliation for project: {ProjectPath}", projectPath);

        // Get files from disk
        var filesOnDisk = GetFilesOnDisk(projectPath);
        _logger.LogDebug("Found {Count} files on disk matching patterns", filesOnDisk.Count);

        // Get records from database
        var recordsInDatabase = await _documentRecordProvider.GetDocumentRecordsAsync(projectPath, cancellationToken)
            .ConfigureAwait(false);
        _logger.LogDebug("Found {Count} records in database", recordsInDatabase.Count);

        // Detect changes
        var newFiles = new List<ReconciliationItem>();
        var modifiedFiles = new List<ReconciliationItem>();
        var deletedFiles = new List<ReconciliationItem>();

        // Check each file on disk against database
        foreach (var diskFile in filesOnDisk)
        {
            var filePath = diskFile.Key;
            var diskModified = diskFile.Value;

            if (recordsInDatabase.TryGetValue(filePath, out var dbModified))
            {
                // File exists in both - check if modified
                // Allow for small time differences due to filesystem precision
                if (Math.Abs((diskModified - dbModified).TotalSeconds) > 1)
                {
                    modifiedFiles.Add(new ReconciliationItem(
                        filePath,
                        ReconciliationAction.Reindex,
                        $"File modified on disk (disk: {diskModified:O}, db: {dbModified:O})"));
                }
            }
            else
            {
                // File exists on disk but not in database - new file
                newFiles.Add(new ReconciliationItem(
                    filePath,
                    ReconciliationAction.Index,
                    "New file found on disk"));
            }
        }

        // Check for files in database but not on disk
        foreach (var dbRecord in recordsInDatabase)
        {
            if (!filesOnDisk.ContainsKey(dbRecord.Key))
            {
                deletedFiles.Add(new ReconciliationItem(
                    dbRecord.Key,
                    ReconciliationAction.Remove,
                    "File deleted from disk"));
            }
        }

        var result = new ReconciliationResult
        {
            ProjectPath = projectPath,
            NewFiles = newFiles,
            ModifiedFiles = modifiedFiles,
            DeletedFiles = deletedFiles,
            TotalFilesOnDisk = filesOnDisk.Count,
            TotalFilesInDatabase = recordsInDatabase.Count
        };

        _logger.LogInformation(
            "Reconciliation complete: {NewCount} new, {ModifiedCount} modified, {DeletedCount} deleted files",
            newFiles.Count,
            modifiedFiles.Count,
            deletedFiles.Count);

        return result;
    }

    /// <summary>
    /// Gets all matching files on disk with their last modified timestamps.
    /// </summary>
    private Dictionary<string, DateTimeOffset> GetFilesOnDisk(string projectPath)
    {
        var result = new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase);

        // Build the matcher for include patterns
        var includeMatcher = new Matcher(StringComparison.OrdinalIgnoreCase);
        foreach (var pattern in _settings.IncludePatterns)
        {
            includeMatcher.AddInclude(pattern);
        }

        // Build the matcher for exclude patterns
        var excludeMatcher = new Matcher(StringComparison.OrdinalIgnoreCase);
        foreach (var pattern in _settings.ExcludePatterns)
        {
            excludeMatcher.AddInclude(pattern);
        }

        // Execute the include matcher
        var directoryInfo = new DirectoryInfo(projectPath);
        var matchResult = includeMatcher.Execute(new DirectoryInfoWrapper(directoryInfo));

        foreach (var match in matchResult.Files)
        {
            // Check if excluded
            if (_settings.ExcludePatterns.Count > 0 && excludeMatcher.Match(match.Path).HasMatches)
            {
                continue;
            }

            var fullPath = Path.GetFullPath(Path.Combine(projectPath, match.Path));

            try
            {
                var fileInfo = new FileInfo(fullPath);
                if (fileInfo.Exists)
                {
                    result[fullPath] = new DateTimeOffset(fileInfo.LastWriteTimeUtc, TimeSpan.Zero);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Could not access file: {Path}", fullPath);
            }
        }

        return result;
    }
}

/// <summary>
/// Stub implementation of <see cref="IDocumentRecordProvider"/> for testing without database dependencies.
/// For production use, <see cref="DatabaseDocumentRecordProvider"/> is registered via DI.
/// </summary>
public sealed class StubDocumentRecordProvider : IDocumentRecordProvider
{
    /// <inheritdoc />
    public Task<Dictionary<string, DateTimeOffset>> GetDocumentRecordsAsync(
        string projectPath,
        CancellationToken cancellationToken = default)
    {
        // Stub returns empty collection for testing (simulates fresh database)
        return Task.FromResult(new Dictionary<string, DateTimeOffset>(StringComparer.OrdinalIgnoreCase));
    }
}
