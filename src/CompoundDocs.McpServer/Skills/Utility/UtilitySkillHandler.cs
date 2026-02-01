using System.Text.Json.Serialization;
using CompoundDocs.McpServer.Data.Repositories;
using CompoundDocs.McpServer.Models;
using CompoundDocs.McpServer.Services.DocumentProcessing;
using CompoundDocs.McpServer.Session;
using CompoundDocs.McpServer.Tools;
using Microsoft.Extensions.Logging;

namespace CompoundDocs.McpServer.Skills.Utility;

/// <summary>
/// Handles utility skills for promote, demote, delete, and reindex operations.
/// </summary>
public sealed class UtilitySkillHandler : IUtilitySkillHandler
{
    private readonly ISessionContext _sessionContext;
    private readonly IDocumentRepository _documentRepository;
    private readonly IDocumentIndexer _documentIndexer;
    private readonly ILogger<UtilitySkillHandler> _logger;

    /// <summary>
    /// Creates a new instance of UtilitySkillHandler.
    /// </summary>
    public UtilitySkillHandler(
        ISessionContext sessionContext,
        IDocumentRepository documentRepository,
        IDocumentIndexer documentIndexer,
        ILogger<UtilitySkillHandler> logger)
    {
        _sessionContext = sessionContext ?? throw new ArgumentNullException(nameof(sessionContext));
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _documentIndexer = documentIndexer ?? throw new ArgumentNullException(nameof(documentIndexer));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #region HandlePromoteAsync

    /// <inheritdoc />
    public async Task<ToolResponse<PromotionResult>> HandlePromoteAsync(
        PromoteRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_sessionContext.IsProjectActive)
        {
            return ToolResponse<PromotionResult>.Fail(ToolErrors.NoActiveProject);
        }

        if (string.IsNullOrWhiteSpace(request.FilePath))
        {
            return ToolResponse<PromotionResult>.Fail(ToolErrors.MissingParameter("file_path"));
        }

        // Normalize promotion level (promote/demote uses different naming)
        var targetLevel = NormalizePromotionLevel(request.Level ?? "promoted");
        if (targetLevel == null)
        {
            return ToolResponse<PromotionResult>.Fail(ToolErrors.InvalidPromotionLevel(request.Level ?? ""));
        }

        _logger.LogInformation("Promoting document {FilePath} to {Level}", request.FilePath, targetLevel);

        try
        {
            var document = await _documentRepository.GetByTenantKeyAsync(
                _sessionContext.TenantKey!,
                request.FilePath,
                cancellationToken);

            if (document == null)
            {
                return ToolResponse<PromotionResult>.Fail(ToolErrors.DocumentNotFound(request.FilePath));
            }

            var previousLevel = document.PromotionLevel;

            var success = await _documentRepository.UpdatePromotionLevelAsync(
                document.Id,
                targetLevel,
                cancellationToken);

            if (!success)
            {
                return ToolResponse<PromotionResult>.Fail(
                    ToolErrors.UnexpectedError("Failed to update promotion level"));
            }

            _logger.LogInformation("Document {FilePath} promoted: {Previous} -> {New}",
                request.FilePath, previousLevel, targetLevel);

            return ToolResponse<PromotionResult>.Ok(new PromotionResult
            {
                Success = true,
                FilePath = request.FilePath,
                DocumentId = document.Id,
                Title = document.Title,
                PreviousLevel = previousLevel,
                NewLevel = targetLevel,
                BoostFactor = PromotionLevels.GetBoostFactor(targetLevel),
                Message = $"Document promoted from '{previousLevel}' to '{targetLevel}'"
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Promote operation cancelled");
            return ToolResponse<PromotionResult>.Fail(ToolErrors.OperationCancelled);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during promote operation");
            return ToolResponse<PromotionResult>.Fail(ToolErrors.UnexpectedError(ex.Message));
        }
    }

    #endregion

    #region HandleDemoteAsync

    /// <inheritdoc />
    public async Task<ToolResponse<PromotionResult>> HandleDemoteAsync(
        DemoteRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_sessionContext.IsProjectActive)
        {
            return ToolResponse<PromotionResult>.Fail(ToolErrors.NoActiveProject);
        }

        if (string.IsNullOrWhiteSpace(request.FilePath))
        {
            return ToolResponse<PromotionResult>.Fail(ToolErrors.MissingParameter("file_path"));
        }

        // Normalize promotion level
        var targetLevel = NormalizePromotionLevel(request.Level ?? "standard");
        if (targetLevel == null)
        {
            return ToolResponse<PromotionResult>.Fail(ToolErrors.InvalidPromotionLevel(request.Level ?? ""));
        }

        _logger.LogInformation("Demoting document {FilePath} to {Level}", request.FilePath, targetLevel);

        try
        {
            var document = await _documentRepository.GetByTenantKeyAsync(
                _sessionContext.TenantKey!,
                request.FilePath,
                cancellationToken);

            if (document == null)
            {
                return ToolResponse<PromotionResult>.Fail(ToolErrors.DocumentNotFound(request.FilePath));
            }

            var previousLevel = document.PromotionLevel;

            var success = await _documentRepository.UpdatePromotionLevelAsync(
                document.Id,
                targetLevel,
                cancellationToken);

            if (!success)
            {
                return ToolResponse<PromotionResult>.Fail(
                    ToolErrors.UnexpectedError("Failed to update promotion level"));
            }

            _logger.LogInformation("Document {FilePath} demoted: {Previous} -> {New}",
                request.FilePath, previousLevel, targetLevel);

            return ToolResponse<PromotionResult>.Ok(new PromotionResult
            {
                Success = true,
                FilePath = request.FilePath,
                DocumentId = document.Id,
                Title = document.Title,
                PreviousLevel = previousLevel,
                NewLevel = targetLevel,
                BoostFactor = PromotionLevels.GetBoostFactor(targetLevel),
                Message = $"Document demoted from '{previousLevel}' to '{targetLevel}'"
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Demote operation cancelled");
            return ToolResponse<PromotionResult>.Fail(ToolErrors.OperationCancelled);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during demote operation");
            return ToolResponse<PromotionResult>.Fail(ToolErrors.UnexpectedError(ex.Message));
        }
    }

    #endregion

    #region HandleDeleteAsync

    /// <inheritdoc />
    public async Task<ToolResponse<DeleteResult>> HandleDeleteAsync(
        DeleteRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_sessionContext.IsProjectActive)
        {
            return ToolResponse<DeleteResult>.Fail(ToolErrors.NoActiveProject);
        }

        if (string.IsNullOrWhiteSpace(request.FilePaths))
        {
            return ToolResponse<DeleteResult>.Fail(ToolErrors.MissingParameter("file_paths"));
        }

        var paths = request.FilePaths
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        if (paths.Count == 0)
        {
            return ToolResponse<DeleteResult>.Fail(ToolErrors.MissingParameter("file_paths"));
        }

        _logger.LogInformation("Delete request: {Count} files, dryRun={DryRun}", paths.Count, request.DryRun);

        try
        {
            var documents = new List<DeletedDocInfo>();
            var errors = new List<DeleteErrorInfo>();
            var deletedCount = 0;
            var notFoundCount = 0;

            foreach (var filePath in paths)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var document = await _documentRepository.GetByTenantKeyAsync(
                        _sessionContext.TenantKey!,
                        filePath,
                        cancellationToken);

                    if (document == null)
                    {
                        notFoundCount++;
                        documents.Add(new DeletedDocInfo
                        {
                            FilePath = filePath,
                            WasDeleted = false,
                            Reason = "Document not found in index"
                        });
                        continue;
                    }

                    if (request.DryRun)
                    {
                        documents.Add(new DeletedDocInfo
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
                        var deleted = await _documentIndexer.DeleteDocumentAsync(
                            _sessionContext.TenantKey!,
                            filePath,
                            cancellationToken);

                        if (deleted)
                        {
                            deletedCount++;
                            documents.Add(new DeletedDocInfo
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
                            errors.Add(new DeleteErrorInfo
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
                    errors.Add(new DeleteErrorInfo
                    {
                        FilePath = filePath,
                        Error = ex.Message
                    });
                }
            }

            var message = request.DryRun
                ? $"Dry run complete: {documents.Count(d => d.Reason?.Contains("Would be deleted") == true)} documents would be deleted"
                : $"Deleted {deletedCount} documents, {notFoundCount} not found, {errors.Count} errors";

            _logger.LogInformation("Delete completed: {Message}", message);

            return ToolResponse<DeleteResult>.Ok(new DeleteResult
            {
                Success = true,
                IsDryRun = request.DryRun,
                TotalRequested = paths.Count,
                DeletedCount = deletedCount,
                NotFoundCount = notFoundCount,
                ErrorCount = errors.Count,
                Documents = documents,
                Errors = errors,
                Message = message
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Delete operation cancelled");
            return ToolResponse<DeleteResult>.Fail(ToolErrors.OperationCancelled);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during delete operation");
            return ToolResponse<DeleteResult>.Fail(ToolErrors.UnexpectedError(ex.Message));
        }
    }

    #endregion

    #region HandleReindexAsync

    /// <inheritdoc />
    public async Task<ToolResponse<ReindexResult>> HandleReindexAsync(
        ReindexRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_sessionContext.IsProjectActive)
        {
            return ToolResponse<ReindexResult>.Fail(ToolErrors.NoActiveProject);
        }

        _logger.LogInformation("Reindex request: filePaths={FilePaths}, docTypes={DocTypes}, force={Force}, dryRun={DryRun}",
            request.FilePaths ?? "all", request.DocTypes ?? "all", request.Force, request.DryRun);

        try
        {
            // Get documents to reindex
            var allDocuments = await _documentRepository.GetAllForTenantAsync(
                _sessionContext.TenantKey!,
                cancellationToken: cancellationToken);

            var documentsToReindex = allDocuments.AsEnumerable();

            // Filter by file paths if specified
            if (!string.IsNullOrWhiteSpace(request.FilePaths))
            {
                var paths = request.FilePaths
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                documentsToReindex = documentsToReindex.Where(d => paths.Contains(d.FilePath));
            }

            // Filter by doc types if specified
            if (!string.IsNullOrWhiteSpace(request.DocTypes))
            {
                var types = request.DocTypes
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                documentsToReindex = documentsToReindex.Where(d => types.Contains(d.DocType));
            }

            var documentList = documentsToReindex.ToList();
            var reindexedDocuments = new List<ReindexedDocInfo>();
            var skippedDocuments = new List<SkippedDocInfo>();
            var errors = new List<ReindexErrorInfo>();
            var totalChunks = 0;

            foreach (var document in documentList)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    if (request.DryRun)
                    {
                        reindexedDocuments.Add(new ReindexedDocInfo
                        {
                            FilePath = document.FilePath,
                            Title = document.Title,
                            DocType = document.DocType,
                            ChunkCount = 0
                        });
                        continue;
                    }

                    // Read the file content from disk
                    var absolutePath = Path.Combine(_sessionContext.ActiveProjectPath!, document.FilePath);
                    if (!File.Exists(absolutePath))
                    {
                        skippedDocuments.Add(new SkippedDocInfo
                        {
                            FilePath = document.FilePath,
                            Reason = "File not found on disk"
                        });
                        continue;
                    }

                    var content = await File.ReadAllTextAsync(absolutePath, cancellationToken);

                    // Reindex the document
                    var indexResult = await _documentIndexer.IndexDocumentAsync(
                        document.FilePath,
                        content,
                        _sessionContext.TenantKey!,
                        cancellationToken);

                    if (indexResult.IsSuccess)
                    {
                        totalChunks += indexResult.ChunkCount;
                        reindexedDocuments.Add(new ReindexedDocInfo
                        {
                            FilePath = document.FilePath,
                            Title = document.Title,
                            DocType = document.DocType,
                            ChunkCount = indexResult.ChunkCount
                        });
                    }
                    else
                    {
                        errors.Add(new ReindexErrorInfo
                        {
                            FilePath = document.FilePath,
                            Error = indexResult.Error ?? "Unknown error"
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error reindexing {FilePath}", document.FilePath);
                    errors.Add(new ReindexErrorInfo
                    {
                        FilePath = document.FilePath,
                        Error = ex.Message
                    });
                }
            }

            var message = request.DryRun
                ? $"Dry run complete: {reindexedDocuments.Count} documents would be reindexed"
                : $"Reindexed {reindexedDocuments.Count} documents with {totalChunks} chunks, {skippedDocuments.Count} skipped, {errors.Count} errors";

            _logger.LogInformation("Reindex completed: {Message}", message);

            return ToolResponse<ReindexResult>.Ok(new ReindexResult
            {
                Success = true,
                IsDryRun = request.DryRun,
                DocumentsProcessed = documentList.Count,
                ReindexedCount = reindexedDocuments.Count,
                TotalChunks = totalChunks,
                ErrorCount = errors.Count,
                ReindexedDocuments = reindexedDocuments,
                SkippedDocuments = skippedDocuments,
                Errors = errors,
                Message = message
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Reindex operation cancelled");
            return ToolResponse<ReindexResult>.Fail(ToolErrors.OperationCancelled);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during reindex operation");
            return ToolResponse<ReindexResult>.Fail(ToolErrors.UnexpectedError(ex.Message));
        }
    }

    #endregion

    #region Helper Methods

    private static string? NormalizePromotionLevel(string level)
    {
        return level.ToLowerInvariant() switch
        {
            "standard" => PromotionLevels.Standard,
            "promoted" or "important" => PromotionLevels.Promoted,
            "pinned" or "critical" => PromotionLevels.Pinned,
            _ => null
        };
    }

    #endregion
}

#region Request/Result Types

/// <summary>
/// Request for promote skill.
/// </summary>
public sealed class PromoteRequest
{
    /// <summary>
    /// The relative path to the document.
    /// </summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>
    /// Target promotion level (promoted/important or pinned/critical).
    /// </summary>
    public string? Level { get; init; }
}

/// <summary>
/// Request for demote skill.
/// </summary>
public sealed class DemoteRequest
{
    /// <summary>
    /// The relative path to the document.
    /// </summary>
    public string FilePath { get; init; } = string.Empty;

    /// <summary>
    /// Target promotion level to demote to.
    /// </summary>
    public string? Level { get; init; }
}

/// <summary>
/// Result of promote/demote operations.
/// </summary>
public sealed class PromotionResult
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("file_path")]
    public string FilePath { get; init; } = string.Empty;

    [JsonPropertyName("document_id")]
    public string DocumentId { get; init; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("previous_level")]
    public string PreviousLevel { get; init; } = string.Empty;

    [JsonPropertyName("new_level")]
    public string NewLevel { get; init; } = string.Empty;

    [JsonPropertyName("boost_factor")]
    public float BoostFactor { get; init; }

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Request for delete skill.
/// </summary>
public sealed class DeleteRequest
{
    /// <summary>
    /// Comma-separated list of file paths to delete.
    /// </summary>
    public string FilePaths { get; init; } = string.Empty;

    /// <summary>
    /// If true, only preview what would be deleted.
    /// </summary>
    public bool DryRun { get; init; }
}

/// <summary>
/// Result of delete operation.
/// </summary>
public sealed class DeleteResult
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("is_dry_run")]
    public bool IsDryRun { get; init; }

    [JsonPropertyName("total_requested")]
    public int TotalRequested { get; init; }

    [JsonPropertyName("deleted_count")]
    public int DeletedCount { get; init; }

    [JsonPropertyName("not_found_count")]
    public int NotFoundCount { get; init; }

    [JsonPropertyName("error_count")]
    public int ErrorCount { get; init; }

    [JsonPropertyName("documents")]
    public List<DeletedDocInfo> Documents { get; init; } = [];

    [JsonPropertyName("errors")]
    public List<DeleteErrorInfo> Errors { get; init; } = [];

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Information about a deleted document.
/// </summary>
public sealed class DeletedDocInfo
{
    [JsonPropertyName("file_path")]
    public string FilePath { get; init; } = string.Empty;

    [JsonPropertyName("document_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DocumentId { get; init; }

    [JsonPropertyName("title")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Title { get; init; }

    [JsonPropertyName("doc_type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DocType { get; init; }

    [JsonPropertyName("was_deleted")]
    public bool WasDeleted { get; init; }

    [JsonPropertyName("reason")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Reason { get; init; }
}

/// <summary>
/// Error information for a failed deletion.
/// </summary>
public sealed class DeleteErrorInfo
{
    [JsonPropertyName("file_path")]
    public string FilePath { get; init; } = string.Empty;

    [JsonPropertyName("error")]
    public string Error { get; init; } = string.Empty;
}

/// <summary>
/// Request for reindex skill.
/// </summary>
public sealed class ReindexRequest
{
    /// <summary>
    /// Comma-separated list of file paths to reindex. If empty, reindexes all.
    /// </summary>
    public string? FilePaths { get; init; }

    /// <summary>
    /// Comma-separated list of document types to reindex.
    /// </summary>
    public string? DocTypes { get; init; }

    /// <summary>
    /// Force reindex even if unchanged.
    /// </summary>
    public bool Force { get; init; }

    /// <summary>
    /// If true, only preview what would be reindexed.
    /// </summary>
    public bool DryRun { get; init; }
}

/// <summary>
/// Result of reindex operation.
/// </summary>
public sealed class ReindexResult
{
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("is_dry_run")]
    public bool IsDryRun { get; init; }

    [JsonPropertyName("documents_processed")]
    public int DocumentsProcessed { get; init; }

    [JsonPropertyName("reindexed_count")]
    public int ReindexedCount { get; init; }

    [JsonPropertyName("total_chunks")]
    public int TotalChunks { get; init; }

    [JsonPropertyName("error_count")]
    public int ErrorCount { get; init; }

    [JsonPropertyName("reindexed_documents")]
    public List<ReindexedDocInfo> ReindexedDocuments { get; init; } = [];

    [JsonPropertyName("skipped_documents")]
    public List<SkippedDocInfo> SkippedDocuments { get; init; } = [];

    [JsonPropertyName("errors")]
    public List<ReindexErrorInfo> Errors { get; init; } = [];

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Information about a reindexed document.
/// </summary>
public sealed class ReindexedDocInfo
{
    [JsonPropertyName("file_path")]
    public string FilePath { get; init; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("doc_type")]
    public string DocType { get; init; } = string.Empty;

    [JsonPropertyName("chunk_count")]
    public int ChunkCount { get; init; }
}

/// <summary>
/// Information about a skipped document.
/// </summary>
public sealed class SkippedDocInfo
{
    [JsonPropertyName("file_path")]
    public string FilePath { get; init; } = string.Empty;

    [JsonPropertyName("reason")]
    public string Reason { get; init; } = string.Empty;
}

/// <summary>
/// Error information for a failed reindex.
/// </summary>
public sealed class ReindexErrorInfo
{
    [JsonPropertyName("file_path")]
    public string FilePath { get; init; } = string.Empty;

    [JsonPropertyName("error")]
    public string Error { get; init; } = string.Empty;
}

#endregion
