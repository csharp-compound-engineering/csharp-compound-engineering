using CompoundDocs.McpServer.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CompoundDocs.McpServer.Data.Repositories;

/// <summary>
/// Repository implementation for RepoPath CRUD operations using TenantDbContext.
/// Provides transactional support and proper async/await patterns.
/// </summary>
public sealed class RepoPathRepository : IRepoPathRepository
{
    private readonly TenantDbContext _context;
    private readonly ILogger<RepoPathRepository> _logger;

    /// <summary>
    /// Initializes a new instance of the RepoPathRepository.
    /// </summary>
    /// <param name="context">The tenant database context.</param>
    /// <param name="logger">The logger instance.</param>
    public RepoPathRepository(TenantDbContext context, ILogger<RepoPathRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<RepoPath?> GetByPathHashAsync(string pathHash, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pathHash);

        _logger.LogDebug("Getting repository path by hash: {PathHash}", pathHash);

        return await _context.RepoPaths
            .AsNoTracking()
            .Include(r => r.Branches)
            .FirstOrDefaultAsync(r => r.PathHash == pathHash, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RepoPath> GetOrCreateAsync(
        string absolutePath,
        string projectName,
        string pathHash,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(absolutePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectName);
        ArgumentException.ThrowIfNullOrWhiteSpace(pathHash);

        _logger.LogDebug(
            "Getting or creating repository path: {ProjectName} at {AbsolutePath}",
            projectName,
            absolutePath);

        var existing = await _context.RepoPaths
            .Include(r => r.Branches)
            .FirstOrDefaultAsync(r => r.PathHash == pathHash, cancellationToken);

        if (existing is not null)
        {
            _logger.LogDebug("Repository path found, updating last accessed timestamp: {PathHash}", pathHash);
            existing.LastAccessedAt = DateTimeOffset.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            return existing;
        }

        _logger.LogInformation(
            "Creating new repository path: {ProjectName} at {AbsolutePath}",
            projectName,
            absolutePath);

        var repoPath = new RepoPath
        {
            Id = Guid.NewGuid(),
            ProjectName = projectName,
            AbsolutePath = absolutePath,
            PathHash = pathHash,
            CreatedAt = DateTimeOffset.UtcNow,
            LastAccessedAt = DateTimeOffset.UtcNow
        };

        _context.RepoPaths.Add(repoPath);
        await _context.SaveChangesAsync(cancellationToken);

        return repoPath;
    }

    /// <inheritdoc />
    public async Task<bool> UpdateLastAccessedAsync(string pathHash, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pathHash);

        _logger.LogDebug("Updating last accessed timestamp for path hash: {PathHash}", pathHash);

        var rowsAffected = await _context.RepoPaths
            .Where(r => r.PathHash == pathHash)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(r => r.LastAccessedAt, DateTimeOffset.UtcNow),
                cancellationToken);

        if (rowsAffected > 0)
        {
            _logger.LogDebug("Updated last accessed timestamp for path hash: {PathHash}", pathHash);
            return true;
        }

        _logger.LogWarning("Repository path not found for update: {PathHash}", pathHash);
        return false;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RepoPath>> GetAllAsync(string? projectName = null, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting all repository paths with filter: {ProjectName}", projectName ?? "(none)");

        var query = _context.RepoPaths
            .AsNoTracking()
            .Include(r => r.Branches)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(projectName))
        {
            query = query.Where(r => r.ProjectName == projectName);
        }

        var results = await query
            .OrderByDescending(r => r.LastAccessedAt)
            .ToListAsync(cancellationToken);

        _logger.LogDebug("Found {Count} repository paths", results.Count);
        return results;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(string pathHash, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pathHash);

        _logger.LogDebug("Deleting repository path: {PathHash}", pathHash);

        var rowsAffected = await _context.RepoPaths
            .Where(r => r.PathHash == pathHash)
            .ExecuteDeleteAsync(cancellationToken);

        if (rowsAffected > 0)
        {
            _logger.LogInformation("Deleted repository path and associated branches: {PathHash}", pathHash);
            return true;
        }

        _logger.LogWarning("Repository path not found for deletion: {PathHash}", pathHash);
        return false;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RepoPath>> GetStalePathsAsync(DateTimeOffset olderThan, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting stale repository paths older than: {OlderThan}", olderThan);

        var results = await _context.RepoPaths
            .AsNoTracking()
            .Where(r => r.LastAccessedAt < olderThan)
            .OrderBy(r => r.LastAccessedAt)
            .ToListAsync(cancellationToken);

        _logger.LogDebug("Found {Count} stale repository paths", results.Count);
        return results;
    }
}
