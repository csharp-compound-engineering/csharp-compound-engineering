using System.ComponentModel;
using System.Text.Json.Serialization;
using CompoundDocs.McpServer.Data.Repositories;
using CompoundDocs.McpServer.Filters;
using CompoundDocs.McpServer.Models;
using CompoundDocs.McpServer.SemanticKernel;
using CompoundDocs.McpServer.Session;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace CompoundDocs.McpServer.Tools;

/// <summary>
/// MCP tool for semantic vector similarity search across documents.
/// </summary>
[McpServerToolType]
public sealed class SemanticSearchTool
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IEmbeddingService _embeddingService;
    private readonly ISessionContext _sessionContext;
    private readonly ILogger<SemanticSearchTool> _logger;

    /// <summary>
    /// Creates a new instance of SemanticSearchTool.
    /// </summary>
    public SemanticSearchTool(
        IDocumentRepository documentRepository,
        IEmbeddingService embeddingService,
        ISessionContext sessionContext,
        ILogger<SemanticSearchTool> logger)
    {
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _sessionContext = sessionContext ?? throw new ArgumentNullException(nameof(sessionContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Perform semantic search across project documents.
    /// </summary>
    /// <param name="query">The search query text.</param>
    /// <param name="limit">Maximum number of results to return (default: 10).</param>
    /// <param name="docTypes">Optional comma-separated list of document types to filter by.</param>
    /// <param name="promotionLevel">Optional minimum promotion level filter (standard, important, critical).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Ranked list of matching documents.</returns>
    [McpServerTool(Name = "semantic_search")]
    [Description("Perform semantic vector similarity search across project documents. Returns documents ranked by relevance to the query.")]
    public async Task<ToolResponse<SemanticSearchResult>> SearchAsync(
        [Description("The search query text")] string query,
        [Description("Maximum number of results to return (default: 10)")] int limit = 10,
        [Description("Optional comma-separated list of document types to filter by (e.g., 'spec,adr')")] string? docTypes = null,
        [Description("Optional minimum promotion level: standard, important, or critical")] string? promotionLevel = null,
        CancellationToken cancellationToken = default)
    {
        if (!_sessionContext.IsProjectActive)
        {
            return ToolResponse<SemanticSearchResult>.Fail(ToolErrors.NoActiveProject);
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            return ToolResponse<SemanticSearchResult>.Fail(ToolErrors.EmptyQuery);
        }

        if (limit <= 0)
        {
            limit = 10;
        }
        else if (limit > 100)
        {
            limit = 100;
        }

        // Validate promotion level if provided
        PromotionLevel? minPromotion = null;
        if (!string.IsNullOrWhiteSpace(promotionLevel))
        {
            if (!Enum.TryParse<PromotionLevel>(promotionLevel, ignoreCase: true, out var parsed))
            {
                return ToolResponse<SemanticSearchResult>.Fail(
                    ToolErrors.InvalidPromotionLevel(promotionLevel));
            }
            minPromotion = parsed;
        }

        // Parse document types if provided
        List<string>? docTypeList = null;
        if (!string.IsNullOrWhiteSpace(docTypes))
        {
            docTypeList = docTypes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            foreach (var dt in docTypeList)
            {
                if (!DocumentTypes.IsValid(dt))
                {
                    return ToolResponse<SemanticSearchResult>.Fail(
                        ToolErrors.InvalidDocType(dt));
                }
            }
        }

        _logger.LogInformation(
            "Semantic search: query='{Query}', limit={Limit}, docTypes={DocTypes}, promotionLevel={PromotionLevel}",
            query,
            limit,
            docTypes ?? "all",
            promotionLevel ?? "all");

        try
        {
            // Generate query embedding
            var embedding = await _embeddingService.GenerateEmbeddingAsync(query, cancellationToken);

            // Search documents
            var searchResults = await _documentRepository.SearchAsync(
                embedding,
                _sessionContext.TenantKey!,
                limit: limit,
                minRelevance: 0.0f,
                docType: docTypeList?.FirstOrDefault(), // Repository currently supports single doc type
                cancellationToken: cancellationToken);

            // Apply promotion level boost and filter
            var results = searchResults
                .Select(r => new DocumentMatch
                {
                    FilePath = r.Document.FilePath,
                    Title = r.Document.Title,
                    DocType = r.Document.DocType,
                    PromotionLevel = r.Document.PromotionLevel,
                    RelevanceScore = ApplyPromotionBoost(r.RelevanceScore, r.Document.PromotionLevel),
                    ContentSnippet = GetContentSnippet(r.Document.Content, 200)
                })
                .OrderByDescending(r => r.RelevanceScore)
                .ToList();

            // Filter by promotion level if specified
            if (minPromotion.HasValue)
            {
                results = results
                    .Where(r => ParsePromotionLevel(r.PromotionLevel) >= minPromotion.Value)
                    .ToList();
            }

            // Filter by doc types if multiple were specified
            if (docTypeList != null && docTypeList.Count > 1)
            {
                results = results
                    .Where(r => docTypeList.Contains(r.DocType, StringComparer.OrdinalIgnoreCase))
                    .ToList();
            }

            _logger.LogInformation(
                "Semantic search completed: {ResultCount} results for query '{Query}'",
                results.Count,
                query);

            return ToolResponse<SemanticSearchResult>.Ok(new SemanticSearchResult
            {
                Query = query,
                TotalResults = results.Count,
                Documents = results
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Semantic search cancelled for query '{Query}'", query);
            return ToolResponse<SemanticSearchResult>.Fail(ToolErrors.OperationCancelled);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during semantic search");
            return ToolResponse<SemanticSearchResult>.Fail(
                ToolErrors.SearchFailed(ex.Message));
        }
    }

    private static float ApplyPromotionBoost(float score, string promotionLevel)
    {
        return score * PromotionLevels.GetBoostFactor(promotionLevel);
    }

    private static PromotionLevel ParsePromotionLevel(string level)
    {
        return level.ToLowerInvariant() switch
        {
            "standard" => PromotionLevel.Standard,
            "important" or "promoted" => PromotionLevel.Important,
            "critical" or "pinned" => PromotionLevel.Critical,
            _ => PromotionLevel.Standard
        };
    }

    private static string GetContentSnippet(string content, int maxLength)
    {
        if (string.IsNullOrEmpty(content))
        {
            return string.Empty;
        }

        if (content.Length <= maxLength)
        {
            return content;
        }

        return content[..maxLength] + "...";
    }
}

/// <summary>
/// Result data for semantic search.
/// </summary>
public sealed class SemanticSearchResult
{
    /// <summary>
    /// The original search query.
    /// </summary>
    [JsonPropertyName("query")]
    public required string Query { get; init; }

    /// <summary>
    /// Total number of results returned.
    /// </summary>
    [JsonPropertyName("total_results")]
    public required int TotalResults { get; init; }

    /// <summary>
    /// List of matching documents ranked by relevance.
    /// </summary>
    [JsonPropertyName("documents")]
    public required List<DocumentMatch> Documents { get; init; }
}

/// <summary>
/// A document match in search results.
/// </summary>
public sealed class DocumentMatch
{
    /// <summary>
    /// The file path of the document.
    /// </summary>
    [JsonPropertyName("file_path")]
    public required string FilePath { get; init; }

    /// <summary>
    /// The document title.
    /// </summary>
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    /// <summary>
    /// The document type.
    /// </summary>
    [JsonPropertyName("doc_type")]
    public required string DocType { get; init; }

    /// <summary>
    /// The promotion level.
    /// </summary>
    [JsonPropertyName("promotion_level")]
    public required string PromotionLevel { get; init; }

    /// <summary>
    /// The relevance score (0.0 to 1.0+, boosted by promotion).
    /// </summary>
    [JsonPropertyName("relevance_score")]
    public required float RelevanceScore { get; init; }

    /// <summary>
    /// A snippet of the document content.
    /// </summary>
    [JsonPropertyName("content_snippet")]
    public required string ContentSnippet { get; init; }
}
