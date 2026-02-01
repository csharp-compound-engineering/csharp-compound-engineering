namespace CompoundDocs.McpServer.Data.Entities;

/// <summary>
/// Represents a git branch tracked for multi-tenant isolation.
/// Maps to tenant_management.branches table.
/// </summary>
/// <remarks>
/// A branch is associated with a specific repository path.
/// The combination of repo_path_id and branch_name is unique.
/// </remarks>
public sealed class Branch
{
    /// <summary>
    /// Unique identifier for the branch record.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Foreign key to the parent repository path.
    /// </summary>
    public Guid RepoPathId { get; set; }

    /// <summary>
    /// Name of the git branch (e.g., "main", "feature/foo").
    /// </summary>
    public string BranchName { get; set; } = string.Empty;

    /// <summary>
    /// Indicates if this is the default branch (main/master).
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// Timestamp when this branch was first activated.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Timestamp when this branch was last accessed.
    /// Updated on each activation.
    /// </summary>
    public DateTimeOffset LastAccessedAt { get; set; }

    /// <summary>
    /// Navigation property for the parent repository path.
    /// </summary>
    public RepoPath RepoPath { get; set; } = null!;
}
