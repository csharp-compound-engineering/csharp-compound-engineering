using System.ComponentModel;
using System.Text.Json.Serialization;
using CompoundDocs.McpServer.Session;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;

namespace CompoundDocs.McpServer.Tools;

/// <summary>
/// MCP tool for activating a project for the current session.
/// This establishes the tenant context for all subsequent operations.
/// </summary>
[McpServerToolType]
public sealed class ActivateProjectTool
{
    private readonly ProjectActivationService _activationService;
    private readonly ISessionContext _sessionContext;
    private readonly ILogger<ActivateProjectTool> _logger;

    /// <summary>
    /// Creates a new instance of ActivateProjectTool.
    /// </summary>
    public ActivateProjectTool(
        ProjectActivationService activationService,
        ISessionContext sessionContext,
        ILogger<ActivateProjectTool> logger)
    {
        _activationService = activationService ?? throw new ArgumentNullException(nameof(activationService));
        _sessionContext = sessionContext ?? throw new ArgumentNullException(nameof(sessionContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Activates a project for the current session, establishing the tenant context.
    /// </summary>
    /// <param name="projectPath">The absolute path to the project root directory.</param>
    /// <param name="branchOverride">Optional branch name override. If not provided, git detection is used.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The activation result with tenant information.</returns>
    [McpServerTool(Name = "activate_project")]
    [Description("Activate a project for the current session. This must be called before using other tools that require project context.")]
    public async Task<ToolResponse<ActivateProjectResult>> ActivateProjectAsync(
        [Description("The absolute path to the project root directory")] string projectPath,
        [Description("Optional branch name override. If not provided, the current git branch is detected.")] string? branchOverride = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return ToolResponse<ActivateProjectResult>.Fail(
                ToolErrors.MissingParameter("project_path"));
        }

        _logger.LogInformation(
            "Activating project at path: {ProjectPath}",
            projectPath);

        try
        {
            var result = await _activationService.ActivateProjectAsync(
                projectPath,
                branchOverride,
                cancellationToken);

            if (!result.IsSuccess)
            {
                _logger.LogWarning(
                    "Project activation failed: {Error}",
                    result.ErrorMessage);

                return ToolResponse<ActivateProjectResult>.Fail(
                    ToolErrors.ProjectActivationFailed(result.ErrorMessage ?? "Unknown error"));
            }

            _logger.LogInformation(
                "Project activated successfully: {ProjectName} on branch {BranchName}",
                result.ProjectName,
                result.BranchName);

            return ToolResponse<ActivateProjectResult>.Ok(new ActivateProjectResult
            {
                ProjectName = result.ProjectName!,
                BranchName = result.BranchName!,
                TenantKey = result.TenantKey!,
                PathHash = result.PathHash!,
                Message = $"Project '{result.ProjectName}' activated on branch '{result.BranchName}'"
            });
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("Project activation cancelled");
            return ToolResponse<ActivateProjectResult>.Fail(ToolErrors.OperationCancelled);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during project activation");
            return ToolResponse<ActivateProjectResult>.Fail(
                ToolErrors.UnexpectedError(ex.Message));
        }
    }
}

/// <summary>
/// Result data for project activation.
/// </summary>
public sealed class ActivateProjectResult
{
    /// <summary>
    /// The activated project name.
    /// </summary>
    [JsonPropertyName("project_name")]
    public required string ProjectName { get; init; }

    /// <summary>
    /// The git branch name.
    /// </summary>
    [JsonPropertyName("branch_name")]
    public required string BranchName { get; init; }

    /// <summary>
    /// The tenant key for this session.
    /// </summary>
    [JsonPropertyName("tenant_key")]
    public required string TenantKey { get; init; }

    /// <summary>
    /// The path hash component of the tenant key.
    /// </summary>
    [JsonPropertyName("path_hash")]
    public required string PathHash { get; init; }

    /// <summary>
    /// Human-readable success message.
    /// </summary>
    [JsonPropertyName("message")]
    public required string Message { get; init; }
}
