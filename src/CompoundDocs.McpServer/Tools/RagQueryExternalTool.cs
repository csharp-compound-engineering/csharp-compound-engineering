using System.ComponentModel;
using System.Text.Json.Serialization;
using CompoundDocs.McpServer.Services.ExternalDocs;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace CompoundDocs.McpServer.Tools;

/// <summary>
/// MCP tool for RAG queries against external documentation.
/// </summary>
[McpServerToolType]
public sealed class RagQueryExternalTool
{
    private readonly IExternalDocsSearchService _externalDocsService;
    private readonly ILogger<RagQueryExternalTool> _logger;

    /// <summary>
    /// Creates a new instance of RagQueryExternalTool.
    /// </summary>
    public RagQueryExternalTool(
        IExternalDocsSearchService externalDocsService,
        ILogger<RagQueryExternalTool> logger)
    {
        _externalDocsService = externalDocsService ?? throw new ArgumentNullException(nameof(externalDocsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Perform RAG query against external documentation sources.
    /// </summary>
    /// <param name="query">The question or query to answer.</param>
    /// <param name="sources">Comma-separated list of sources to query (default: all).</param>
    /// <param name="maxResults">Maximum number of source documents to use (default: 5).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The RAG response from external sources.</returns>
    [McpServerTool(Name = "rag_query_external")]
    [Description("Perform RAG-based question answering against external documentation sources. Retrieves and synthesizes information from external docs.")]
    public async Task<ToolResponse<ExternalRagResult>> QueryAsync(
        [Description("The question or query to answer")] string query,
        [Description("Comma-separated list of sources to query (default: all)")] string? sources = null,
        [Description("Maximum number of source documents to use (default: 5)")] int maxResults = 5,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return ToolResponse<ExternalRagResult>.Fail(ToolErrors.EmptyQuery);
        }

        if (maxResults <= 0)
        {
            maxResults = 5;
        }
        else if (maxResults > 20)
        {
            maxResults = 20;
        }

        // Parse source list
        List<string>? sourceList = null;
        if (!string.IsNullOrWhiteSpace(sources))
        {
            sourceList = sources.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            // Validate sources
            foreach (var source in sourceList)
            {
                if (!_externalDocsService.IsSourceAvailable(source))
                {
                    return ToolResponse<ExternalRagResult>.Fail(
                        ToolErrors.ExternalSourceNotConfigured(source));
                }
            }
        }

        _logger.LogInformation(
            "External RAG query: '{Query}', sources=[{Sources}], maxResults={MaxResults}",
            query,
            sourceList != null ? string.Join(", ", sourceList) : "all",
            maxResults);

        try
        {
            var ragResult = await _externalDocsService.RagQueryAsync(query, sourceList, maxResults, cancellationToken);

            var responseSources = ragResult.Sources
                .Select(s => new ExternalRagSource
                {
                    Source = s.Source,
                    Title = s.Title,
                    Url = s.Url,
                    RelevanceScore = s.RelevanceScore
                })
                .ToList();

            // Group results by source for status
            var sourceStatuses = responseSources
                .GroupBy(s => s.Source)
                .Select(g => new ExternalRagSourceStatus
                {
                    Source = g.Key,
                    Success = true,
                    ResultCount = g.Count()
                })
                .ToList();

            _logger.LogInformation(
                "External RAG query completed: {SourceCount} sources, confidence={Confidence:F2}",
                responseSources.Count,
                ragResult.ConfidenceScore);

            return ToolResponse<ExternalRagResult>.Ok(new ExternalRagResult
            {
                Query = query,
                Answer = ragResult.Answer,
                Sources = responseSources,
                SourceStatuses = sourceStatuses,
                ConfidenceScore = ragResult.ConfidenceScore
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("External RAG query cancelled");
            return ToolResponse<ExternalRagResult>.Fail(ToolErrors.OperationCancelled);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during external RAG query");
            return ToolResponse<ExternalRagResult>.Fail(
                ToolErrors.RagSynthesisFailed(ex.Message));
        }
    }

}

/// <summary>
/// Result data for external RAG query.
/// </summary>
public sealed class ExternalRagResult
{
    /// <summary>
    /// The original query.
    /// </summary>
    [JsonPropertyName("query")]
    public required string Query { get; init; }

    /// <summary>
    /// The synthesized answer from external sources.
    /// </summary>
    [JsonPropertyName("answer")]
    public required string Answer { get; init; }

    /// <summary>
    /// Source documents from external providers.
    /// </summary>
    [JsonPropertyName("sources")]
    public required List<ExternalRagSource> Sources { get; init; }

    /// <summary>
    /// Status of each queried source.
    /// </summary>
    [JsonPropertyName("source_statuses")]
    public required List<ExternalRagSourceStatus> SourceStatuses { get; init; }

    /// <summary>
    /// Confidence score for the answer (0.0 to 1.0).
    /// </summary>
    [JsonPropertyName("confidence_score")]
    public required float ConfidenceScore { get; init; }
}

/// <summary>
/// A source from external RAG query.
/// </summary>
public sealed class ExternalRagSource
{
    /// <summary>
    /// The source provider name.
    /// </summary>
    [JsonPropertyName("source")]
    public required string Source { get; init; }

    /// <summary>
    /// The document title.
    /// </summary>
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    /// <summary>
    /// URL to the source document.
    /// </summary>
    [JsonPropertyName("url")]
    public required string Url { get; init; }

    /// <summary>
    /// The relevance score.
    /// </summary>
    [JsonPropertyName("relevance_score")]
    public required float RelevanceScore { get; init; }
}

/// <summary>
/// Status of an external source query.
/// </summary>
public sealed class ExternalRagSourceStatus
{
    /// <summary>
    /// The source name.
    /// </summary>
    [JsonPropertyName("source")]
    public required string Source { get; init; }

    /// <summary>
    /// Whether the query succeeded.
    /// </summary>
    [JsonPropertyName("success")]
    public required bool Success { get; init; }

    /// <summary>
    /// Number of results from this source.
    /// </summary>
    [JsonPropertyName("result_count")]
    public int ResultCount { get; init; }

    /// <summary>
    /// Error message if query failed.
    /// </summary>
    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; init; }
}
