using CompoundDocs.McpServer.Data.Entities;

namespace CompoundDocs.McpServer.Data.Repositories;

/// <summary>
/// Repository interface for RepoPath CRUD operations.
/// Provides methods for managing repository path entries used for tenant isolation.
/// </summary>
public interface IRepoPathRepository
{
    /// <summary>
    /// Gets a repository path by its path hash.
    /// </summary>
    /// <param name="pathHash">The SHA-256 hash of the normalized absolute path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The RepoPath if found, null otherwise.</returns>
    Task<RepoPath?> GetByPathHashAsync(string pathHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets or creates a repository path entry.
    /// If the path already exists (by hash), updates the LastAccessedAt timestamp.
    /// Otherwise, creates a new entry.
    /// </summary>
    /// <param name="absolutePath">The absolute filesystem path to the repository.</param>
    /// <param name="projectName">The project name extracted from config or directory.</param>
    /// <param name="pathHash">The SHA-256 hash of the normalized path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The existing or newly created RepoPath.</returns>
    Task<RepoPath> GetOrCreateAsync(
        string absolutePath,
        string projectName,
        string pathHash,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the LastAccessedAt timestamp for a repository path.
    /// </summary>
    /// <param name="pathHash">The path hash of the repository to update.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the record was found and updated, false otherwise.</returns>
    Task<bool> UpdateLastAccessedAsync(string pathHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all repository paths, optionally filtered by project name.
    /// </summary>
    /// <param name="projectName">Optional project name filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of repository paths.</returns>
    Task<IReadOnlyList<RepoPath>> GetAllAsync(string? projectName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a repository path by its path hash.
    /// This will cascade delete all associated branches.
    /// </summary>
    /// <param name="pathHash">The path hash of the repository to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the record was found and deleted, false otherwise.</returns>
    Task<bool> DeleteAsync(string pathHash, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets repository paths that haven't been accessed since the specified date.
    /// Used for cleanup operations.
    /// </summary>
    /// <param name="olderThan">The cutoff date for last access.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of stale repository paths.</returns>
    Task<IReadOnlyList<RepoPath>> GetStalePathsAsync(DateTimeOffset olderThan, CancellationToken cancellationToken = default);
}
