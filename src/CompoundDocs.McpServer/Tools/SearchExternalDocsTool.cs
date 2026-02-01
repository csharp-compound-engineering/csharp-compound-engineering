using System.ComponentModel;
using System.Text.Json.Serialization;
using CompoundDocs.McpServer.Services.ExternalDocs;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace CompoundDocs.McpServer.Tools;

/// <summary>
/// MCP tool for searching external project documentation.
/// </summary>
[McpServerToolType]
public sealed class SearchExternalDocsTool
{
    private readonly IExternalDocsSearchService _externalDocsService;
    private readonly ILogger<SearchExternalDocsTool> _logger;

    /// <summary>
    /// Creates a new instance of SearchExternalDocsTool.
    /// </summary>
    public SearchExternalDocsTool(
        IExternalDocsSearchService externalDocsService,
        ILogger<SearchExternalDocsTool> logger)
    {
        _externalDocsService = externalDocsService ?? throw new ArgumentNullException(nameof(externalDocsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Search external project documentation.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <param name="limit">Maximum number of results to return (default: 10).</param>
    /// <param name="sources">Optional comma-separated list of sources to search (default: all).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Search results from external sources.</returns>
    [McpServerTool(Name = "search_external_docs")]
    [Description("Search external project documentation from configured sources like Context7, Anthropic docs, etc.")]
    public async Task<ToolResponse<ExternalSearchResult>> SearchAsync(
        [Description("The search query")] string query,
        [Description("Maximum number of results to return (default: 10)")] int limit = 10,
        [Description("Optional comma-separated list of sources to search (default: all)")] string? sources = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return ToolResponse<ExternalSearchResult>.Fail(ToolErrors.EmptyQuery);
        }

        if (limit <= 0)
        {
            limit = 10;
        }
        else if (limit > 50)
        {
            limit = 50;
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
                    return ToolResponse<ExternalSearchResult>.Fail(
                        ToolErrors.ExternalSourceNotConfigured(source));
                }
            }
        }

        _logger.LogInformation(
            "External docs search: query='{Query}', limit={Limit}, sources=[{Sources}]",
            query,
            limit,
            sourceList != null ? string.Join(", ", sourceList) : "all");

        try
        {
            var searchResults = await _externalDocsService.SearchAsync(query, sourceList, limit, cancellationToken);

            var results = searchResults.Select(r => new ExternalDocResult
            {
                Source = r.Source,
                Title = r.Title,
                Url = r.Url,
                Snippet = r.Snippet,
                RelevanceScore = r.RelevanceScore
            }).ToList();

            // Group results by source for summary
            var sourceResults = results
                .GroupBy(r => r.Source)
                .Select(g => new ExternalSourceSearchResult
                {
                    Source = g.Key,
                    ResultCount = g.Count(),
                    Success = true
                })
                .ToList();

            _logger.LogInformation(
                "External docs search completed: {ResultCount} results from {SourceCount} sources",
                results.Count,
                sourceResults.Count);

            return ToolResponse<ExternalSearchResult>.Ok(new ExternalSearchResult
            {
                Query = query,
                TotalResults = results.Count,
                Results = results,
                SourceResults = sourceResults
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("External docs search cancelled");
            return ToolResponse<ExternalSearchResult>.Fail(ToolErrors.OperationCancelled);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during external docs search");
            return ToolResponse<ExternalSearchResult>.Fail(
                ToolErrors.SearchFailed(ex.Message));
        }
    }

    /// <summary>
    /// List available external documentation sources.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of available sources.</returns>
    [McpServerTool(Name = "list_external_sources")]
    [Description("List available external documentation sources that can be searched.")]
    public Task<ToolResponse<ListExternalSourcesResult>> ListSourcesAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Listing external documentation sources");

        var configuredSources = _externalDocsService.GetSources();
        var sources = configuredSources
            .Select(s => new ExternalSourceInfo
            {
                Name = s.Name,
                DisplayName = s.DisplayName,
                Description = s.Description,
                BaseUrl = s.BaseUrl
            })
            .ToList();

        return Task.FromResult(ToolResponse<ListExternalSourcesResult>.Ok(new ListExternalSourcesResult
        {
            Sources = sources,
            TotalSources = sources.Count
        }));
    }
}

/// <summary>
/// Result data for external documentation search.
/// </summary>
public sealed class ExternalSearchResult
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
    /// Search results from external sources.
    /// </summary>
    [JsonPropertyName("results")]
    public required List<ExternalDocResult> Results { get; init; }

    /// <summary>
    /// Results breakdown by source.
    /// </summary>
    [JsonPropertyName("source_results")]
    public required List<ExternalSourceSearchResult> SourceResults { get; init; }
}

/// <summary>
/// A result from external documentation search.
/// </summary>
public sealed class ExternalDocResult
{
    /// <summary>
    /// The source of the result.
    /// </summary>
    [JsonPropertyName("source")]
    public required string Source { get; init; }

    /// <summary>
    /// The document title.
    /// </summary>
    [JsonPropertyName("title")]
    public required string Title { get; init; }

    /// <summary>
    /// The URL to the document.
    /// </summary>
    [JsonPropertyName("url")]
    public required string Url { get; init; }

    /// <summary>
    /// A snippet from the document.
    /// </summary>
    [JsonPropertyName("snippet")]
    public required string Snippet { get; init; }

    /// <summary>
    /// The relevance score.
    /// </summary>
    [JsonPropertyName("relevance_score")]
    public required float RelevanceScore { get; init; }
}

/// <summary>
/// Search result summary for a single source.
/// </summary>
public sealed class ExternalSourceSearchResult
{
    /// <summary>
    /// The source name.
    /// </summary>
    [JsonPropertyName("source")]
    public required string Source { get; init; }

    /// <summary>
    /// Number of results from this source.
    /// </summary>
    [JsonPropertyName("result_count")]
    public required int ResultCount { get; init; }

    /// <summary>
    /// Whether the search succeeded.
    /// </summary>
    [JsonPropertyName("success")]
    public required bool Success { get; init; }

    /// <summary>
    /// Error message if search failed.
    /// </summary>
    [JsonPropertyName("error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Error { get; init; }
}

/// <summary>
/// Result data for listing external sources.
/// </summary>
public sealed class ListExternalSourcesResult
{
    /// <summary>
    /// Available external documentation sources.
    /// </summary>
    [JsonPropertyName("sources")]
    public required List<ExternalSourceInfo> Sources { get; init; }

    /// <summary>
    /// Total number of sources.
    /// </summary>
    [JsonPropertyName("total_sources")]
    public required int TotalSources { get; init; }
}

/// <summary>
/// Information about an external documentation source.
/// </summary>
public sealed class ExternalSourceInfo
{
    /// <summary>
    /// The source identifier.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// Human-readable display name.
    /// </summary>
    [JsonPropertyName("display_name")]
    public required string DisplayName { get; init; }

    /// <summary>
    /// Description of the source.
    /// </summary>
    [JsonPropertyName("description")]
    public required string Description { get; init; }

    /// <summary>
    /// Base URL of the source.
    /// </summary>
    [JsonPropertyName("base_url")]
    public required string BaseUrl { get; init; }
}
