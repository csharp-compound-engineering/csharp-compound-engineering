using CompoundDocs.McpServer.Tools;

namespace CompoundDocs.McpServer.Skills.Query;

/// <summary>
/// Interface for handling query skill operations.
/// Provides methods for RAG query, semantic search, contextual recall, and related document discovery.
/// </summary>
public interface IQuerySkillHandler
{
    /// <summary>
    /// Handles RAG-based query operations with source attribution.
    /// </summary>
    /// <param name="request">The query request parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Query result with synthesized answer and sources.</returns>
    Task<ToolResponse<QueryResult>> HandleQueryAsync(
        QueryRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles semantic search operations across project documents.
    /// </summary>
    /// <param name="request">The search request parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Search result with ranked documents.</returns>
    Task<ToolResponse<SearchQueryResult>> HandleSearchAsync(
        SearchRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles contextual recall operations with multi-turn conversation support.
    /// </summary>
    /// <param name="request">The recall request parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Recall result with contextual answer and conversation metadata.</returns>
    Task<ToolResponse<RecallResult>> HandleRecallAsync(
        RecallRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles related document discovery using the document link graph.
    /// </summary>
    /// <param name="request">The related documents request parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Related documents result with relationship metadata.</returns>
    Task<ToolResponse<RelatedDocumentsResult>> HandleRelatedAsync(
        RelatedRequest request,
        CancellationToken cancellationToken = default);
}
