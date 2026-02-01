using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace CompoundDocs.McpServer.Services.ExternalDocs;

/// <summary>
/// Default implementation of IExternalDocsSearchService that provides web-based search
/// functionality for external documentation sources.
/// </summary>
public sealed class DefaultExternalDocsSearchService : IExternalDocsSearchService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<DefaultExternalDocsSearchService> _logger;
    private readonly Dictionary<string, ExternalSourceConfig> _sources;

    /// <summary>
    /// Creates a new instance of DefaultExternalDocsSearchService.
    /// </summary>
    /// <param name="httpClient">The HTTP client for making requests.</param>
    /// <param name="logger">The logger instance.</param>
    public DefaultExternalDocsSearchService(
        HttpClient httpClient,
        ILogger<DefaultExternalDocsSearchService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Initialize known sources
        _sources = new Dictionary<string, ExternalSourceConfig>(StringComparer.OrdinalIgnoreCase)
        {
            ["context7"] = new ExternalSourceConfig(
                "context7",
                "Context7",
                "Context7 external documentation provider",
                "https://context7.com"),
            ["anthropic"] = new ExternalSourceConfig(
                "anthropic",
                "Anthropic Docs",
                "Anthropic API and Claude documentation",
                "https://docs.anthropic.com"),
            ["microsoft"] = new ExternalSourceConfig(
                "microsoft",
                "Microsoft Docs",
                "Microsoft .NET and Azure documentation",
                "https://learn.microsoft.com")
        };
    }

    /// <inheritdoc />
    public IReadOnlyList<ExternalSourceConfig> GetSources() => _sources.Values.ToList();

    /// <inheritdoc />
    public bool IsSourceAvailable(string sourceName) =>
        _sources.ContainsKey(sourceName);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ExternalDocsSearchResult>> SearchAsync(
        string query,
        IReadOnlyList<string>? sources = null,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        var sourcesToSearch = sources ?? _sources.Keys.ToList();
        var results = new List<ExternalDocsSearchResult>();
        var resultsPerSource = Math.Max(1, limit / sourcesToSearch.Count);

        foreach (var sourceName in sourcesToSearch)
        {
            if (!_sources.TryGetValue(sourceName, out var sourceConfig))
            {
                _logger.LogWarning("Unknown external source: {Source}", sourceName);
                continue;
            }

            try
            {
                var sourceResults = await SearchSourceAsync(
                    sourceConfig,
                    query,
                    resultsPerSource,
                    cancellationToken);

                results.AddRange(sourceResults);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to search external source: {Source}", sourceName);
            }
        }

        return results
            .OrderByDescending(r => r.RelevanceScore)
            .Take(limit)
            .ToList();
    }

    /// <inheritdoc />
    public async Task<ExternalDocsRagResult> RagQueryAsync(
        string query,
        IReadOnlyList<string>? sources = null,
        int maxResults = 5,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        // First search for relevant documents
        var searchResults = await SearchAsync(query, sources, maxResults * 2, cancellationToken);
        var topResults = searchResults.Take(maxResults).ToList();

        if (topResults.Count == 0)
        {
            return new ExternalDocsRagResult(
                "combined",
                "No relevant information found from external documentation sources.",
                Array.Empty<ExternalDocsSearchResult>(),
                0.0f);
        }

        // Synthesize answer from results
        var answer = SynthesizeAnswer(query, topResults);
        var avgConfidence = topResults.Average(r => r.RelevanceScore);

        return new ExternalDocsRagResult(
            "combined",
            answer,
            topResults,
            Math.Min(1.0f, avgConfidence * 1.1f));
    }

    /// <summary>
    /// Searches a specific external source.
    /// </summary>
    private async Task<IReadOnlyList<ExternalDocsSearchResult>> SearchSourceAsync(
        ExternalSourceConfig source,
        string query,
        int limit,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Searching {Source} for: {Query}", source.Name, query);

        // For now, create a search URL and return a result pointing to it
        // In a full implementation, this would call the actual API of each source
        var searchUrl = source.Name switch
        {
            "context7" => $"https://context7.com/search?q={Uri.EscapeDataString(query)}",
            "anthropic" => $"https://docs.anthropic.com/search?q={Uri.EscapeDataString(query)}",
            "microsoft" => $"https://learn.microsoft.com/en-us/search/?terms={Uri.EscapeDataString(query)}",
            _ => $"{source.BaseUrl}/search?q={Uri.EscapeDataString(query)}"
        };

        // Try to fetch actual search results if the source has an API
        try
        {
            var results = await TryFetchSearchResultsAsync(source, query, limit, cancellationToken);
            if (results.Count > 0)
            {
                return results;
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogDebug(ex, "Could not fetch results from {Source}, returning search link", source.Name);
        }

        // Fall back to returning a search link
        return new List<ExternalDocsSearchResult>
        {
            new(
                source.Name,
                $"Search {source.DisplayName} for '{query}'",
                searchUrl,
                $"Click to search {source.DisplayName} documentation for '{query}'",
                0.5f)
        };
    }

    /// <summary>
    /// Attempts to fetch actual search results from a source's API.
    /// </summary>
    private async Task<List<ExternalDocsSearchResult>> TryFetchSearchResultsAsync(
        ExternalSourceConfig source,
        string query,
        int limit,
        CancellationToken cancellationToken)
    {
        // This is where you would implement actual API calls to each source
        // For now, we just return empty to trigger the fallback
        // In production, you would:
        // - Call Context7's search API
        // - Call Anthropic's docs API
        // - Call Microsoft's search API

        await Task.CompletedTask; // Placeholder for async operation
        return new List<ExternalDocsSearchResult>();
    }

    /// <summary>
    /// Synthesizes an answer from search results.
    /// </summary>
    private static string SynthesizeAnswer(string query, IReadOnlyList<ExternalDocsSearchResult> results)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Based on external documentation sources, here are relevant resources:");
        sb.AppendLine();

        foreach (var result in results)
        {
            sb.AppendLine($"- **[{result.Title}]({result.Url})** ({result.Source})");
            if (!string.IsNullOrEmpty(result.Snippet))
            {
                sb.AppendLine($"  {result.Snippet}");
            }
            sb.AppendLine();
        }

        sb.AppendLine();
        sb.AppendLine("For more detailed information, please visit the linked documentation.");

        return sb.ToString();
    }
}
