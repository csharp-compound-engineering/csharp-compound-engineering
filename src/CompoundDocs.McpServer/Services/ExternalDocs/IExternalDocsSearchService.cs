namespace CompoundDocs.McpServer.Services.ExternalDocs;

/// <summary>
/// Search result from an external documentation source.
/// </summary>
/// <param name="Source">The source provider name.</param>
/// <param name="Title">The document title.</param>
/// <param name="Url">The URL to the document.</param>
/// <param name="Snippet">A snippet from the document.</param>
/// <param name="RelevanceScore">The relevance score (0.0 to 1.0).</param>
public sealed record ExternalDocsSearchResult(
    string Source,
    string Title,
    string Url,
    string Snippet,
    float RelevanceScore);

/// <summary>
/// RAG result from an external documentation source.
/// </summary>
/// <param name="Source">The source provider name.</param>
/// <param name="Answer">The synthesized answer.</param>
/// <param name="Sources">The source documents used.</param>
/// <param name="ConfidenceScore">The confidence score (0.0 to 1.0).</param>
public sealed record ExternalDocsRagResult(
    string Source,
    string Answer,
    IReadOnlyList<ExternalDocsSearchResult> Sources,
    float ConfidenceScore);

/// <summary>
/// Configuration for an external documentation source.
/// </summary>
/// <param name="Name">The source identifier.</param>
/// <param name="DisplayName">Human-readable display name.</param>
/// <param name="Description">Description of the source.</param>
/// <param name="BaseUrl">Base URL of the source.</param>
public sealed record ExternalSourceConfig(
    string Name,
    string DisplayName,
    string Description,
    string BaseUrl);

/// <summary>
/// Service interface for searching external documentation sources.
/// Implementations can integrate with Context7, Anthropic docs, Microsoft docs, etc.
/// </summary>
public interface IExternalDocsSearchService
{
    /// <summary>
    /// Gets the list of configured external documentation sources.
    /// </summary>
    IReadOnlyList<ExternalSourceConfig> GetSources();

    /// <summary>
    /// Searches external documentation sources.
    /// </summary>
    /// <param name="query">The search query.</param>
    /// <param name="sources">List of sources to search (null for all).</param>
    /// <param name="limit">Maximum number of results to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of search results.</returns>
    Task<IReadOnlyList<ExternalDocsSearchResult>> SearchAsync(
        string query,
        IReadOnlyList<string>? sources = null,
        int limit = 10,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs a RAG query against external documentation sources.
    /// </summary>
    /// <param name="query">The question or query to answer.</param>
    /// <param name="sources">List of sources to query (null for all).</param>
    /// <param name="maxResults">Maximum number of source documents to use.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The RAG result with synthesized answer.</returns>
    Task<ExternalDocsRagResult> RagQueryAsync(
        string query,
        IReadOnlyList<string>? sources = null,
        int maxResults = 5,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a source is configured and available.
    /// </summary>
    /// <param name="sourceName">The source name to check.</param>
    /// <returns>True if the source is available.</returns>
    bool IsSourceAvailable(string sourceName);
}
