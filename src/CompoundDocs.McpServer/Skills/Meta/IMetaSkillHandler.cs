using CompoundDocs.McpServer.Tools;

namespace CompoundDocs.McpServer.Skills.Meta;

/// <summary>
/// Interface for handling meta skill operations.
/// Provides methods for status, activate, deactivate, and help operations.
/// </summary>
public interface IMetaSkillHandler
{
    /// <summary>
    /// Handles status requests to display system state and statistics.
    /// </summary>
    /// <param name="request">The status request parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Status result with system information.</returns>
    Task<ToolResponse<StatusResult>> HandleStatusAsync(
        StatusRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles project activation requests.
    /// </summary>
    /// <param name="request">The activate request parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Activation result with project information.</returns>
    Task<ToolResponse<ActivateResult>> HandleActivateAsync(
        ActivateRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles project deactivation requests.
    /// </summary>
    /// <param name="request">The deactivate request parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Deactivation result.</returns>
    Task<ToolResponse<DeactivateResult>> HandleDeactivateAsync(
        DeactivateRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Handles help requests to display skill information.
    /// </summary>
    /// <param name="request">The help request parameters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Help result with skill information.</returns>
    Task<ToolResponse<HelpResult>> HandleHelpAsync(
        HelpRequest request,
        CancellationToken cancellationToken = default);
}
