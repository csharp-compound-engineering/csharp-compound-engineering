using System.Text.Json.Serialization;

namespace CompoundDocs.McpServer.Skills.Query;

/// <summary>
/// Base request model for query operations.
/// </summary>
public abstract class BaseQueryRequest
{
    /// <summary>
    /// The query text.
    /// </summary>
    [JsonPropertyName("query")]
    public required string Query { get; init; }

    /// <summary>
    /// Maximum number of results to return.
    /// </summary>
    [JsonPropertyName("limit")]
    public int Limit { get; init; } = 10;

    /// <summary>
    /// Optional comma-separated list of document types to filter by.
    /// </summary>
    [JsonPropertyName("doc_types")]
    public string? DocTypes { get; init; }

    /// <summary>
    /// Optional minimum promotion level filter (standard, important, critical).
    /// </summary>
    [JsonPropertyName("promotion_level")]
    public string? PromotionLevel { get; init; }
}

/// <summary>
/// Request model for RAG query operations.
/// </summary>
public sealed class QueryRequest : BaseQueryRequest
{
    /// <summary>
    /// Whether to include individual chunks in the response.
    /// </summary>
    [JsonPropertyName("include_chunks")]
    public bool IncludeChunks { get; init; }

    /// <summary>
    /// Creates a new QueryRequest with default values.
    /// </summary>
    public QueryRequest()
    {
        Limit = 5; // Default for RAG queries
    }
}

/// <summary>
/// Request model for semantic search operations.
/// </summary>
public sealed class SearchRequest : BaseQueryRequest
{
    /// <summary>
    /// Minimum relevance score threshold (0.0 to 1.0).
    /// </summary>
    [JsonPropertyName("min_relevance")]
    public float MinRelevance { get; init; }

    /// <summary>
    /// Creates a new SearchRequest with default values.
    /// </summary>
    public SearchRequest()
    {
        Limit = 10; // Default for search
    }
}

/// <summary>
/// Context mode for recall operations.
/// </summary>
public enum ContextMode
{
    /// <summary>
    /// Automatically detect if this is a follow-up question.
    /// </summary>
    Auto,

    /// <summary>
    /// Start a new conversation context.
    /// </summary>
    New,

    /// <summary>
    /// Explicitly continue previous context.
    /// </summary>
    Continue
}

/// <summary>
/// Request model for contextual recall operations.
/// </summary>
public sealed class RecallRequest : BaseQueryRequest
{
    /// <summary>
    /// Context handling mode.
    /// </summary>
    [JsonPropertyName("context_mode")]
    public ContextMode ContextMode { get; init; } = ContextMode.Auto;

    /// <summary>
    /// Whether to include conversation history in context.
    /// </summary>
    [JsonPropertyName("include_history")]
    public bool IncludeHistory { get; init; } = true;

    /// <summary>
    /// Optional session identifier for conversation tracking.
    /// </summary>
    [JsonPropertyName("session_id")]
    public string? SessionId { get; init; }

    /// <summary>
    /// Creates a new RecallRequest with default values.
    /// </summary>
    public RecallRequest()
    {
        Limit = 5; // Default for recall
    }
}

/// <summary>
/// Link traversal types for related document discovery.
/// </summary>
public enum LinkType
{
    /// <summary>
    /// Follow all link types.
    /// </summary>
    All,

    /// <summary>
    /// Only links from the source document.
    /// </summary>
    Outgoing,

    /// <summary>
    /// Only links pointing to the source document.
    /// </summary>
    Incoming,

    /// <summary>
    /// Only mutual links.
    /// </summary>
    Bidirectional
}

/// <summary>
/// Request model for related document discovery operations.
/// </summary>
public sealed class RelatedRequest
{
    /// <summary>
    /// File path of the document to find relations for.
    /// Either FilePath or Query must be provided.
    /// </summary>
    [JsonPropertyName("file_path")]
    public string? FilePath { get; init; }

    /// <summary>
    /// Search query to find a starting document.
    /// Either FilePath or Query must be provided.
    /// </summary>
    [JsonPropertyName("query")]
    public string? Query { get; init; }

    /// <summary>
    /// Depth of link traversal (1-3).
    /// </summary>
    [JsonPropertyName("depth")]
    public int Depth { get; init; } = 1;

    /// <summary>
    /// Whether to include semantically similar documents.
    /// </summary>
    [JsonPropertyName("include_semantic")]
    public bool IncludeSemantic { get; init; } = true;

    /// <summary>
    /// Maximum number of related documents to return.
    /// </summary>
    [JsonPropertyName("limit")]
    public int Limit { get; init; } = 10;

    /// <summary>
    /// Optional comma-separated list of document types to filter results.
    /// </summary>
    [JsonPropertyName("doc_types")]
    public string? DocTypes { get; init; }

    /// <summary>
    /// Types of links to follow.
    /// </summary>
    [JsonPropertyName("link_types")]
    public LinkType LinkTypes { get; init; } = LinkType.All;
}
