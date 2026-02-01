using System.ComponentModel;
using System.Text.Json.Serialization;
using CompoundDocs.McpServer.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace CompoundDocs.McpServer.Tools;

/// <summary>
/// MCP tool for enumerating available document types and their schemas.
/// </summary>
[McpServerToolType]
public sealed class ListDocTypesTool
{
    private readonly ILogger<ListDocTypesTool> _logger;

    /// <summary>
    /// Creates a new instance of ListDocTypesTool.
    /// </summary>
    public ListDocTypesTool(ILogger<ListDocTypesTool> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// List all available document types and their descriptions.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of document types with their schemas.</returns>
    [McpServerTool(Name = "list_doc_types")]
    [Description("List all available document types and their schemas. Returns both built-in and custom doc-types.")]
    public Task<ToolResponse<ListDocTypesResult>> ListDocTypesAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Listing available document types");

        try
        {
            var docTypes = new List<DocTypeInfo>
            {
                new()
                {
                    Name = DocumentTypes.Spec,
                    DisplayName = "Specification",
                    Description = "Specification documents defining system behavior, requirements, and constraints.",
                    RequiredFields = ["title"],
                    OptionalFields = ["version", "status", "links"],
                    IsBuiltIn = true
                },
                new()
                {
                    Name = DocumentTypes.Adr,
                    DisplayName = "Architecture Decision Record",
                    Description = "Architecture Decision Records documenting significant architectural choices.",
                    RequiredFields = ["title", "status"],
                    OptionalFields = ["date", "context", "decision", "consequences"],
                    IsBuiltIn = true
                },
                new()
                {
                    Name = DocumentTypes.Research,
                    DisplayName = "Research",
                    Description = "Research documents with background information, analysis, and findings.",
                    RequiredFields = ["title"],
                    OptionalFields = ["date", "author", "links"],
                    IsBuiltIn = true
                },
                new()
                {
                    Name = DocumentTypes.Doc,
                    DisplayName = "Documentation",
                    Description = "General documentation for guides, tutorials, and reference material.",
                    RequiredFields = ["title"],
                    OptionalFields = ["category", "links"],
                    IsBuiltIn = true
                },
                new()
                {
                    Name = DocumentTypes.Problem,
                    DisplayName = "Problem Statement",
                    Description = "Problem statements and issue descriptions for tracking challenges.",
                    RequiredFields = ["title"],
                    OptionalFields = ["severity", "status", "links"],
                    IsBuiltIn = true
                },
                new()
                {
                    Name = DocumentTypes.Insight,
                    DisplayName = "Insight",
                    Description = "Insights and lessons learned from project experiences.",
                    RequiredFields = ["title"],
                    OptionalFields = ["date", "category", "links"],
                    IsBuiltIn = true
                },
                new()
                {
                    Name = DocumentTypes.Codebase,
                    DisplayName = "Codebase Documentation",
                    Description = "Documentation specific to the codebase structure and implementation.",
                    RequiredFields = ["title"],
                    OptionalFields = ["module", "links"],
                    IsBuiltIn = true
                },
                new()
                {
                    Name = DocumentTypes.Tool,
                    DisplayName = "Tool Documentation",
                    Description = "Documentation for tools, utilities, and scripts used in the project.",
                    RequiredFields = ["title"],
                    OptionalFields = ["version", "usage", "links"],
                    IsBuiltIn = true
                },
                new()
                {
                    Name = DocumentTypes.Style,
                    DisplayName = "Style Guide",
                    Description = "Style guides and coding standards for the project.",
                    RequiredFields = ["title"],
                    OptionalFields = ["language", "links"],
                    IsBuiltIn = true
                }
            };

            var promotionLevels = new List<PromotionLevelInfo>
            {
                new()
                {
                    Name = PromotionLevels.Standard,
                    DisplayName = "Standard",
                    Description = "Standard documents appear in RAG results based on similarity score.",
                    BoostFactor = 1.0f
                },
                new()
                {
                    Name = PromotionLevels.Promoted,
                    DisplayName = "Promoted",
                    Description = "Promoted documents receive a boost in RAG ranking.",
                    BoostFactor = 1.5f
                },
                new()
                {
                    Name = PromotionLevels.Pinned,
                    DisplayName = "Pinned",
                    Description = "Pinned documents always appear at the top of RAG results.",
                    BoostFactor = 2.0f
                }
            };

            _logger.LogDebug("Listed {Count} document types", docTypes.Count);

            return Task.FromResult(ToolResponse<ListDocTypesResult>.Ok(new ListDocTypesResult
            {
                DocTypes = docTypes,
                PromotionLevels = promotionLevels,
                TotalDocTypes = docTypes.Count,
                TotalPromotionLevels = promotionLevels.Count
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error listing document types");
            return Task.FromResult(ToolResponse<ListDocTypesResult>.Fail(
                ToolErrors.UnexpectedError(ex.Message)));
        }
    }
}

/// <summary>
/// Result data for listing document types.
/// </summary>
public sealed class ListDocTypesResult
{
    /// <summary>
    /// List of available document types.
    /// </summary>
    [JsonPropertyName("doc_types")]
    public required List<DocTypeInfo> DocTypes { get; init; }

    /// <summary>
    /// List of available promotion levels.
    /// </summary>
    [JsonPropertyName("promotion_levels")]
    public required List<PromotionLevelInfo> PromotionLevels { get; init; }

    /// <summary>
    /// Total number of document types.
    /// </summary>
    [JsonPropertyName("total_doc_types")]
    public required int TotalDocTypes { get; init; }

    /// <summary>
    /// Total number of promotion levels.
    /// </summary>
    [JsonPropertyName("total_promotion_levels")]
    public required int TotalPromotionLevels { get; init; }
}

/// <summary>
/// Information about a document type.
/// </summary>
public sealed class DocTypeInfo
{
    /// <summary>
    /// The document type identifier.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// Human-readable display name.
    /// </summary>
    [JsonPropertyName("display_name")]
    public required string DisplayName { get; init; }

    /// <summary>
    /// Description of the document type.
    /// </summary>
    [JsonPropertyName("description")]
    public required string Description { get; init; }

    /// <summary>
    /// Required frontmatter fields.
    /// </summary>
    [JsonPropertyName("required_fields")]
    public required List<string> RequiredFields { get; init; }

    /// <summary>
    /// Optional frontmatter fields.
    /// </summary>
    [JsonPropertyName("optional_fields")]
    public required List<string> OptionalFields { get; init; }

    /// <summary>
    /// Whether this is a built-in type.
    /// </summary>
    [JsonPropertyName("is_built_in")]
    public required bool IsBuiltIn { get; init; }
}

/// <summary>
/// Information about a promotion level.
/// </summary>
public sealed class PromotionLevelInfo
{
    /// <summary>
    /// The promotion level identifier.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// Human-readable display name.
    /// </summary>
    [JsonPropertyName("display_name")]
    public required string DisplayName { get; init; }

    /// <summary>
    /// Description of the promotion level.
    /// </summary>
    [JsonPropertyName("description")]
    public required string Description { get; init; }

    /// <summary>
    /// The boost factor applied to relevance scores.
    /// </summary>
    [JsonPropertyName("boost_factor")]
    public required float BoostFactor { get; init; }
}
