using System.ComponentModel;
using System.Text.Json.Serialization;
using CompoundDocs.McpServer.Data.Repositories;
using CompoundDocs.McpServer.Filters;
using CompoundDocs.McpServer.Models;
using CompoundDocs.McpServer.Session;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace CompoundDocs.McpServer.Tools;

/// <summary>
/// MCP tool for updating document promotion levels.
/// </summary>
[McpServerToolType]
public sealed class UpdatePromotionLevelTool
{
    private readonly IDocumentRepository _documentRepository;
    private readonly ISessionContext _sessionContext;
    private readonly ILogger<UpdatePromotionLevelTool> _logger;

    /// <summary>
    /// Creates a new instance of UpdatePromotionLevelTool.
    /// </summary>
    public UpdatePromotionLevelTool(
        IDocumentRepository documentRepository,
        ISessionContext sessionContext,
        ILogger<UpdatePromotionLevelTool> logger)
    {
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _sessionContext = sessionContext ?? throw new ArgumentNullException(nameof(sessionContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Update the promotion level of a document.
    /// </summary>
    /// <param name="filePath">The relative path to the document.</param>
    /// <param name="promotionLevel">The new promotion level (standard, promoted, or pinned).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The update result.</returns>
    [McpServerTool(Name = "update_promotion_level")]
    [Description("Update the promotion level of a document. Higher promotion levels receive boost in search results.")]
    public async Task<ToolResponse<UpdatePromotionResult>> UpdatePromotionLevelAsync(
        [Description("The relative path to the document from the project root")] string filePath,
        [Description("The new promotion level: standard, promoted (or important), or pinned (or critical)")] string promotionLevel,
        CancellationToken cancellationToken = default)
    {
        if (!_sessionContext.IsProjectActive)
        {
            return ToolResponse<UpdatePromotionResult>.Fail(ToolErrors.NoActiveProject);
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            return ToolResponse<UpdatePromotionResult>.Fail(
                ToolErrors.MissingParameter("file_path"));
        }

        if (string.IsNullOrWhiteSpace(promotionLevel))
        {
            return ToolResponse<UpdatePromotionResult>.Fail(
                ToolErrors.MissingParameter("promotion_level"));
        }

        // Normalize promotion level
        var normalizedLevel = NormalizePromotionLevel(promotionLevel);
        if (normalizedLevel == null)
        {
            return ToolResponse<UpdatePromotionResult>.Fail(
                ToolErrors.InvalidPromotionLevel(promotionLevel));
        }

        _logger.LogInformation(
            "Updating promotion level for {FilePath} to {PromotionLevel}",
            filePath,
            normalizedLevel);

        try
        {
            // Get the document
            var document = await _documentRepository.GetByTenantKeyAsync(
                _sessionContext.TenantKey!,
                filePath,
                cancellationToken);

            if (document == null)
            {
                return ToolResponse<UpdatePromotionResult>.Fail(
                    ToolErrors.DocumentNotFound(filePath));
            }

            var previousLevel = document.PromotionLevel;

            // Update the promotion level
            var success = await _documentRepository.UpdatePromotionLevelAsync(
                document.Id,
                normalizedLevel,
                cancellationToken);

            if (!success)
            {
                return ToolResponse<UpdatePromotionResult>.Fail(
                    ToolErrors.UnexpectedError("Failed to update promotion level"));
            }

            _logger.LogInformation(
                "Promotion level updated for {FilePath}: {PreviousLevel} -> {NewLevel}",
                filePath,
                previousLevel,
                normalizedLevel);

            return ToolResponse<UpdatePromotionResult>.Ok(new UpdatePromotionResult
            {
                FilePath = filePath,
                DocumentId = document.Id,
                Title = document.Title,
                PreviousLevel = previousLevel,
                NewLevel = normalizedLevel,
                BoostFactor = PromotionLevels.GetBoostFactor(normalizedLevel),
                Message = $"Promotion level updated from '{previousLevel}' to '{normalizedLevel}'"
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Update promotion level cancelled");
            return ToolResponse<UpdatePromotionResult>.Fail(ToolErrors.OperationCancelled);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error updating promotion level");
            return ToolResponse<UpdatePromotionResult>.Fail(
                ToolErrors.UnexpectedError(ex.Message));
        }
    }

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
}

/// <summary>
/// Result data for promotion level update.
/// </summary>
public sealed class UpdatePromotionResult
{
    /// <summary>
    /// The file path of the document.
    /// </summary>
    [JsonPropertyName("file_path")]
    public required string FilePath { get; init; }

    /// <summary>
    /// The document ID.
    /// </summary>
    [JsonPropertyName("document_id")]
    public required string DocumentId { get; init; }

    /// <summary>
    /// The document title.
    /// </summary>
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    /// <summary>
    /// The previous promotion level.
    /// </summary>
    [JsonPropertyName("previous_level")]
    public required string PreviousLevel { get; init; }

    /// <summary>
    /// The new promotion level.
    /// </summary>
    [JsonPropertyName("new_level")]
    public required string NewLevel { get; init; }

    /// <summary>
    /// The boost factor for the new level.
    /// </summary>
    [JsonPropertyName("boost_factor")]
    public required float BoostFactor { get; init; }

    /// <summary>
    /// Human-readable success message.
    /// </summary>
    [JsonPropertyName("message")]
    public required string Message { get; init; }
}
