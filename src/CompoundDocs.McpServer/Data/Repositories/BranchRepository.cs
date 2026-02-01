using CompoundDocs.McpServer.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CompoundDocs.McpServer.Data.Repositories;

/// <summary>
/// Repository implementation for Branch CRUD operations using TenantDbContext.
/// Ensures single default branch per repository and provides cascade operations.
/// </summary>
public sealed class BranchRepository : IBranchRepository
{
    private readonly TenantDbContext _context;
    private readonly ILogger<BranchRepository> _logger;

    /// <summary>
    /// Initializes a new instance of the BranchRepository.
    /// </summary>
    /// <param name="context">The tenant database context.</param>
    /// <param name="logger">The logger instance.</param>
    public BranchRepository(TenantDbContext context, ILogger<BranchRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<Branch?> GetByRepoAndNameAsync(Guid repoPathId, string branchName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branchName);

        _logger.LogDebug(
            "Getting branch by repo path and name: {RepoPathId}/{BranchName}",
            repoPathId,
            branchName);

        return await _context.Branches
            .AsNoTracking()
            .Include(b => b.RepoPath)
            .FirstOrDefaultAsync(
                b => b.RepoPathId == repoPathId && b.BranchName == branchName,
                cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Branch> GetOrCreateAsync(
        Guid repoPathId,
        string branchName,
        bool isDefault = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branchName);

        _logger.LogDebug(
            "Getting or creating branch: {RepoPathId}/{BranchName} (isDefault: {IsDefault})",
            repoPathId,
            branchName,
            isDefault);

        var existing = await _context.Branches
            .Include(b => b.RepoPath)
            .FirstOrDefaultAsync(
                b => b.RepoPathId == repoPathId && b.BranchName == branchName,
                cancellationToken);

        if (existing is not null)
        {
            _logger.LogDebug("Branch found, updating last accessed timestamp: {BranchName}", branchName);
            existing.LastAccessedAt = DateTimeOffset.UtcNow;

            // If setting as default, ensure no other branch is default
            if (isDefault && !existing.IsDefault)
            {
                await ClearDefaultBranchAsync(repoPathId, cancellationToken);
                existing.IsDefault = true;
            }

            await _context.SaveChangesAsync(cancellationToken);
            return existing;
        }

        _logger.LogInformation(
            "Creating new branch: {RepoPathId}/{BranchName} (isDefault: {IsDefault})",
            repoPathId,
            branchName,
            isDefault);

        // If setting as default, ensure no other branch is default
        if (isDefault)
        {
            await ClearDefaultBranchAsync(repoPathId, cancellationToken);
        }

        var branch = new Branch
        {
            Id = Guid.NewGuid(),
            RepoPathId = repoPathId,
            BranchName = branchName,
            IsDefault = isDefault,
            CreatedAt = DateTimeOffset.UtcNow,
            LastAccessedAt = DateTimeOffset.UtcNow
        };

        _context.Branches.Add(branch);
        await _context.SaveChangesAsync(cancellationToken);

        return branch;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Branch>> GetBranchesForRepoAsync(Guid repoPathId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting all branches for repo path: {RepoPathId}", repoPathId);

        var results = await _context.Branches
            .AsNoTracking()
            .Where(b => b.RepoPathId == repoPathId)
            .OrderByDescending(b => b.IsDefault)
            .ThenByDescending(b => b.LastAccessedAt)
            .ToListAsync(cancellationToken);

        _logger.LogDebug("Found {Count} branches for repo path: {RepoPathId}", results.Count, repoPathId);
        return results;
    }

    /// <inheritdoc />
    public async Task<bool> SetDefaultBranchAsync(Guid repoPathId, string branchName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branchName);

        _logger.LogDebug(
            "Setting default branch: {RepoPathId}/{BranchName}",
            repoPathId,
            branchName);

        // Start a transaction to ensure atomicity
        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            // Clear any existing default branch
            await ClearDefaultBranchAsync(repoPathId, cancellationToken);

            // Set the new default branch
            var rowsAffected = await _context.Branches
                .Where(b => b.RepoPathId == repoPathId && b.BranchName == branchName)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(b => b.IsDefault, true)
                        .SetProperty(b => b.LastAccessedAt, DateTimeOffset.UtcNow),
                    cancellationToken);

            await transaction.CommitAsync(cancellationToken);

            if (rowsAffected > 0)
            {
                _logger.LogInformation(
                    "Set default branch: {RepoPathId}/{BranchName}",
                    repoPathId,
                    branchName);
                return true;
            }

            _logger.LogWarning(
                "Branch not found for setting as default: {RepoPathId}/{BranchName}",
                repoPathId,
                branchName);
            return false;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(
                ex,
                "Failed to set default branch: {RepoPathId}/{BranchName}",
                repoPathId,
                branchName);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid repoPathId, string branchName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branchName);

        _logger.LogDebug(
            "Deleting branch: {RepoPathId}/{BranchName}",
            repoPathId,
            branchName);

        var rowsAffected = await _context.Branches
            .Where(b => b.RepoPathId == repoPathId && b.BranchName == branchName)
            .ExecuteDeleteAsync(cancellationToken);

        if (rowsAffected > 0)
        {
            _logger.LogInformation(
                "Deleted branch: {RepoPathId}/{BranchName}",
                repoPathId,
                branchName);
            return true;
        }

        _logger.LogWarning(
            "Branch not found for deletion: {RepoPathId}/{BranchName}",
            repoPathId,
            branchName);
        return false;
    }

    /// <inheritdoc />
    public async Task<int> DeleteAllForRepoAsync(Guid repoPathId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Deleting all branches for repo path: {RepoPathId}", repoPathId);

        var rowsAffected = await _context.Branches
            .Where(b => b.RepoPathId == repoPathId)
            .ExecuteDeleteAsync(cancellationToken);

        _logger.LogInformation(
            "Deleted {Count} branches for repo path: {RepoPathId}",
            rowsAffected,
            repoPathId);

        return rowsAffected;
    }

    /// <inheritdoc />
    public async Task<Branch?> GetDefaultBranchAsync(Guid repoPathId, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting default branch for repo path: {RepoPathId}", repoPathId);

        return await _context.Branches
            .AsNoTracking()
            .Include(b => b.RepoPath)
            .FirstOrDefaultAsync(
                b => b.RepoPathId == repoPathId && b.IsDefault,
                cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Branch>> GetStaleBranchesAsync(DateTimeOffset olderThan, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting stale branches older than: {OlderThan}", olderThan);

        var results = await _context.Branches
            .AsNoTracking()
            .Include(b => b.RepoPath)
            .Where(b => b.LastAccessedAt < olderThan)
            .OrderBy(b => b.LastAccessedAt)
            .ToListAsync(cancellationToken);

        _logger.LogDebug("Found {Count} stale branches", results.Count);
        return results;
    }

    /// <summary>
    /// Clears the default flag from all branches in a repository.
    /// </summary>
    private async Task ClearDefaultBranchAsync(Guid repoPathId, CancellationToken cancellationToken)
    {
        await _context.Branches
            .Where(b => b.RepoPathId == repoPathId && b.IsDefault)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(b => b.IsDefault, false),
                cancellationToken);
    }
}
