namespace CompoundDocs.McpServer.Session;

/// <summary>
/// Interface for session state management in the MCP server.
/// Provides access to the currently active project context and tenant information.
/// </summary>
public interface ISessionContext
{
    /// <summary>
    /// Gets the absolute path to the currently active project.
    /// Null if no project is active.
    /// </summary>
    string? ActiveProjectPath { get; }

    /// <summary>
    /// Gets the git branch name for the active project.
    /// Defaults to "main" if branch detection fails.
    /// </summary>
    string? ActiveBranch { get; }

    /// <summary>
    /// Gets the tenant key for the current session.
    /// Format: project_name:branch_name:path_hash
    /// Null if no project is active.
    /// </summary>
    string? TenantKey { get; }

    /// <summary>
    /// Gets whether a project is currently active.
    /// </summary>
    bool IsProjectActive { get; }

    /// <summary>
    /// Gets the project name extracted from the active project path.
    /// Null if no project is active.
    /// </summary>
    string? ProjectName { get; }

    /// <summary>
    /// Gets the path hash component of the tenant key.
    /// Null if no project is active.
    /// </summary>
    string? PathHash { get; }

    /// <summary>
    /// Activates a project for the current session.
    /// </summary>
    /// <param name="projectPath">The absolute path to the project root.</param>
    /// <param name="branchName">The git branch name for the project.</param>
    /// <exception cref="ArgumentNullException">Thrown when projectPath is null.</exception>
    /// <exception cref="ArgumentException">Thrown when projectPath is empty or whitespace.</exception>
    void ActivateProject(string projectPath, string branchName);

    /// <summary>
    /// Deactivates the current project, clearing all session state.
    /// </summary>
    void DeactivateProject();

    /// <summary>
    /// Gets the database connection string for the active tenant.
    /// </summary>
    /// <param name="baseConnectionString">The base connection string template.</param>
    /// <returns>The connection string with tenant-specific configuration.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no project is active.</exception>
    string GetConnectionString(string baseConnectionString);
}
