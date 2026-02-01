namespace CompoundDocs.McpServer.Data.Entities;

/// <summary>
/// Represents a repository path tracked for multi-tenant isolation.
/// Maps to tenant_management.repo_paths table.
/// </summary>
/// <remarks>
/// A repo path uniquely identifies a worktree location. The path_hash is a SHA-256
/// hash of the normalized absolute path, used as the tenant key component.
/// Multiple branches can be associated with a single repo path.
/// </remarks>
public sealed class RepoPath
{
    /// <summary>
    /// Unique identifier for the repository path.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Name of the project extracted from configuration or directory name.
    /// </summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>
    /// Absolute filesystem path to the repository root.
    /// </summary>
    public string AbsolutePath { get; set; } = string.Empty;

    /// <summary>
    /// SHA-256 hash of the normalized absolute path (64 hex characters).
    /// Used as a component of the tenant key for isolation.
    /// </summary>
    public string PathHash { get; set; } = string.Empty;

    /// <summary>
    /// Timestamp when this repository path was first activated.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// Timestamp when this repository path was last accessed.
    /// Updated on each activation.
    /// </summary>
    public DateTimeOffset LastAccessedAt { get; set; }

    /// <summary>
    /// Navigation property for branches associated with this repository path.
    /// </summary>
    public ICollection<Branch> Branches { get; set; } = new List<Branch>();
}
