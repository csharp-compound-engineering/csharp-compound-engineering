using System.ComponentModel;
using System.Text.Json.Serialization;
using CompoundDocs.McpServer.Data.Repositories;
using CompoundDocs.McpServer.Services.DocumentProcessing;
using CompoundDocs.McpServer.Session;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace CompoundDocs.McpServer.Tools;

/// <summary>
/// MCP tool for deleting documents from the index.
/// Supports dry-run mode for preview.
/// </summary>
[McpServerToolType]
public sealed class DeleteDocumentsTool
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IDocumentIndexer _documentIndexer;
    private readonly ISessionContext _sessionContext;
    private readonly ILogger<DeleteDocumentsTool> _logger;

    /// <summary>
    /// Creates a new instance of DeleteDocumentsTool.
    /// </summary>
    public DeleteDocumentsTool(
        IDocumentRepository documentRepository,
        IDocumentIndexer documentIndexer,
        ISessionContext sessionContext,
        ILogger<DeleteDocumentsTool> logger)
    {
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _documentIndexer = documentIndexer ?? throw new ArgumentNullException(nameof(documentIndexer));
        _sessionContext = sessionContext ?? throw new ArgumentNullException(nameof(sessionContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Delete documents from the index.
    /// </summary>
    /// <param name="filePaths">Comma-separated list of file paths to delete.</param>
    /// <param name="dryRun">If true, only preview what would be deleted without actually deleting.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The deletion result.</returns>
    [McpServerTool(Name = "delete_documents")]
    [Description("Delete documents from the index. Supports dry-run mode to preview deletions without executing them.")]
    public async Task<ToolResponse<DeleteDocumentsResult>> DeleteDocumentsAsync(
        [Description("Comma-separated list of file paths to delete")] string filePaths,
        [Description("If true, only preview what would be deleted without actually deleting (default: false)")] bool dryRun = false,
        CancellationToken cancellationToken = default)
    {
        if (!_sessionContext.IsProjectActive)
        {
            return ToolResponse<DeleteDocumentsResult>.Fail(ToolErrors.NoActiveProject);
        }

        if (string.IsNullOrWhiteSpace(filePaths))
        {
            return ToolResponse<DeleteDocumentsResult>.Fail(
                ToolErrors.MissingParameter("file_paths"));
        }

        var paths = filePaths.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        if (paths.Count == 0)
        {
            return ToolResponse<DeleteDocumentsResult>.Fail(
                ToolErrors.MissingParameter("file_paths"));
        }

        _logger.LogInformation(
            "Delete documents request: {FileCount} files, dryRun={DryRun}",
            paths.Count,
            dryRun);

        try
        {
            var results = new List<DeletedDocumentInfo>();
            var errors = new List<DeleteError>();
            var deletedCount = 0;
            var notFoundCount = 0;

            foreach (var filePath in paths)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    // Check if document exists
                    var document = await _documentRepository.GetByTenantKeyAsync(
                        _sessionContext.TenantKey!,
                        filePath,
                        cancellationToken);

                    if (document == null)
                    {
                        notFoundCount++;
                        results.Add(new DeletedDocumentInfo
                        {
                            FilePath = filePath,
                            WasDeleted = false,
                            Reason = "Document not found in index"
                        });
                        continue;
                    }

                    if (dryRun)
                    {
                        // Dry run - just report what would be deleted
                        results.Add(new DeletedDocumentInfo
                        {
                            FilePath = filePath,
                            DocumentId = document.Id,
                            Title = document.Title,
                            DocType = document.DocType,
                            WasDeleted = false,
                            Reason = "Would be deleted (dry run)"
                        });
                    }
                    else
                    {
                        // Actually delete the document
                        var deleted = await _documentIndexer.DeleteDocumentAsync(
                            _sessionContext.TenantKey!,
                            filePath,
                            cancellationToken);

                        if (deleted)
                        {
                            deletedCount++;
                            results.Add(new DeletedDocumentInfo
                            {
                                FilePath = filePath,
                                DocumentId = document.Id,
                                Title = document.Title,
                                DocType = document.DocType,
                                WasDeleted = true,
                                Reason = "Successfully deleted"
                            });
                        }
                        else
                        {
                            errors.Add(new DeleteError
                            {
                                FilePath = filePath,
                                Error = "Delete operation returned false"
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error processing deletion for {FilePath}", filePath);
                    errors.Add(new DeleteError
                    {
                        FilePath = filePath,
                        Error = ex.Message
                    });
                }
            }

            var message = dryRun
                ? $"Dry run complete: {results.Count(r => r.WasDeleted == false && r.Reason?.Contains("Would be deleted") == true)} documents would be deleted"
                : $"Deleted {deletedCount} documents, {notFoundCount} not found, {errors.Count} errors";

            _logger.LogInformation(
                "Delete documents completed: {Message}",
                message);

            return ToolResponse<DeleteDocumentsResult>.Ok(new DeleteDocumentsResult
            {
                IsDryRun = dryRun,
                TotalRequested = paths.Count,
                DeletedCount = deletedCount,
                NotFoundCount = notFoundCount,
                ErrorCount = errors.Count,
                Documents = results,
                Errors = errors,
                Message = message
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Delete documents cancelled");
            return ToolResponse<DeleteDocumentsResult>.Fail(ToolErrors.OperationCancelled);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during document deletion");
            return ToolResponse<DeleteDocumentsResult>.Fail(
                ToolErrors.UnexpectedError(ex.Message));
        }
    }
}

/// <summary>
/// Result data for document deletion.
/// </summary>
public sealed class DeleteDocumentsResult
{
    /// <summary>
    /// Whether this was a dry run.
    /// </summary>
    [JsonPropertyName("is_dry_run")]
    public required bool IsDryRun { get; init; }

    /// <summary>
    /// Total number of files requested for deletion.
    /// </summary>
    [JsonPropertyName("total_requested")]
    public required int TotalRequested { get; init; }

    /// <summary>
    /// Number of documents actually deleted.
    /// </summary>
    [JsonPropertyName("deleted_count")]
    public required int DeletedCount { get; init; }

    /// <summary>
    /// Number of documents not found.
    /// </summary>
    [JsonPropertyName("not_found_count")]
    public required int NotFoundCount { get; init; }

    /// <summary>
    /// Number of errors encountered.
    /// </summary>
    [JsonPropertyName("error_count")]
    public required int ErrorCount { get; init; }

    /// <summary>
    /// Details for each document processed.
    /// </summary>
    [JsonPropertyName("documents")]
    public required List<DeletedDocumentInfo> Documents { get; init; }

    /// <summary>
    /// Errors encountered during deletion.
    /// </summary>
    [JsonPropertyName("errors")]
    public required List<DeleteError> Errors { get; init; }

    /// <summary>
    /// Human-readable summary message.
    /// </summary>
    [JsonPropertyName("message")]
    public required string Message { get; init; }
}

/// <summary>
/// Information about a deleted document.
/// </summary>
public sealed class DeletedDocumentInfo
{
    /// <summary>
    /// The file path.
    /// </summary>
    [JsonPropertyName("file_path")]
    public required string FilePath { get; init; }

    /// <summary>
    /// The document ID if found.
    /// </summary>
    [JsonPropertyName("document_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DocumentId { get; init; }

    /// <summary>
    /// The document title if found.
    /// </summary>
    [JsonPropertyName("title")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Title { get; init; }

    /// <summary>
    /// The document type if found.
    /// </summary>
    [JsonPropertyName("doc_type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DocType { get; init; }

    /// <summary>
    /// Whether the document was deleted.
    /// </summary>
    [JsonPropertyName("was_deleted")]
    public required bool WasDeleted { get; init; }

    /// <summary>
    /// Reason for the result.
    /// </summary>
    [JsonPropertyName("reason")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Reason { get; init; }
}

/// <summary>
/// Error information for a failed deletion.
/// </summary>
public sealed class DeleteError
{
    /// <summary>
    /// The file path that failed.
    /// </summary>
    [JsonPropertyName("file_path")]
    public required string FilePath { get; init; }

    /// <summary>
    /// The error message.
    /// </summary>
    [JsonPropertyName("error")]
    public required string Error { get; init; }
}
