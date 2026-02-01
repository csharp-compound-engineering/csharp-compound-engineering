using CompoundDocs.McpServer.Data.Entities;

namespace CompoundDocs.McpServer.Data.Repositories;

/// <summary>
/// Repository interface for Branch CRUD operations.
/// Provides methods for managing git branch entries used for tenant isolation.
/// </summary>
public interface IBranchRepository
{
    /// <summary>
    /// Gets a branch by its repository path ID and branch name.
    /// </summary>
    /// <param name="repoPathId">The ID of the parent repository path.</param>
    /// <param name="branchName">The git branch name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The Branch if found, null otherwise.</returns>
    Task<Branch?> GetByRepoAndNameAsync(Guid repoPathId, string branchName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets or creates a branch entry for a repository path.
    /// If the branch already exists, updates the LastAccessedAt timestamp.
    /// Otherwise, creates a new entry.
    /// </summary>
    /// <param name="repoPathId">The ID of the parent repository path.</param>
    /// <param name="branchName">The git branch name.</param>
    /// <param name="isDefault">Whether this is the default branch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The existing or newly created Branch.</returns>
    Task<Branch> GetOrCreateAsync(
        Guid repoPathId,
        string branchName,
        bool isDefault = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all branches for a specific repository path.
    /// </summary>
    /// <param name="repoPathId">The ID of the repository path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of branches for the repository.</returns>
    Task<IReadOnlyList<Branch>> GetBranchesForRepoAsync(Guid repoPathId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets a branch as the default for its repository path.
    /// Ensures only one branch per repository is marked as default.
    /// </summary>
    /// <param name="repoPathId">The ID of the repository path.</param>
    /// <param name="branchName">The branch name to set as default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the branch was found and updated, false otherwise.</returns>
    Task<bool> SetDefaultBranchAsync(Guid repoPathId, string branchName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a branch by its repository path ID and branch name.
    /// </summary>
    /// <param name="repoPathId">The ID of the parent repository path.</param>
    /// <param name="branchName">The git branch name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the branch was found and deleted, false otherwise.</returns>
    Task<bool> DeleteAsync(Guid repoPathId, string branchName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all branches for a repository path.
    /// </summary>
    /// <param name="repoPathId">The ID of the repository path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of branches deleted.</returns>
    Task<int> DeleteAllForRepoAsync(Guid repoPathId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the default branch for a repository path.
    /// </summary>
    /// <param name="repoPathId">The ID of the repository path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The default Branch if one is set, null otherwise.</returns>
    Task<Branch?> GetDefaultBranchAsync(Guid repoPathId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets branches that haven't been accessed since the specified date.
    /// Used for cleanup operations.
    /// </summary>
    /// <param name="olderThan">The cutoff date for last access.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of stale branches.</returns>
    Task<IReadOnlyList<Branch>> GetStaleBranchesAsync(DateTimeOffset olderThan, CancellationToken cancellationToken = default);
}
