using CompoundDocs.McpServer.Tools;

namespace CompoundDocs.McpServer.Skills.Utility;

/// <summary>
/// Interface for handling utility skill operations.
/// Provides methods for promote, demote, delete, and reindex operations.
/// </summary>
public interface IUtilitySkillHandler
{
    /// <summary>
    /// Handles document promotion requests to increase visibility in RAG results.
    /// </summary>
    /// <param name="request">The promote request parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Promotion result with updated level information.</returns>
    Task<ToolResponse<PromotionResult>> HandlePromoteAsync(
        PromoteRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles document demotion requests to decrease visibility in RAG results.
    /// </summary>
    /// <param name="request">The demote request parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Promotion result with updated level information.</returns>
    Task<ToolResponse<PromotionResult>> HandleDemoteAsync(
        DemoteRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles document deletion requests.
    /// </summary>
    /// <param name="request">The delete request parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Delete result with operation summary.</returns>
    Task<ToolResponse<DeleteResult>> HandleDeleteAsync(
        DeleteRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles document reindexing requests.
    /// </summary>
    /// <param name="request">The reindex request parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Reindex result with operation summary.</returns>
    Task<ToolResponse<ReindexResult>> HandleReindexAsync(
        ReindexRequest request,
        CancellationToken cancellationToken = default);
}
