using CompoundDocs.McpServer.Session;

namespace CompoundDocs.McpServer.Filters;

/// <summary>
/// Builder for creating tenant filter criteria that enforce tenant isolation in vector searches.
/// Ensures all queries are scoped to the correct project, branch, and path.
/// This class provides a framework-agnostic filter specification that can be used with
/// Semantic Kernel's VectorSearchFilter or LINQ expressions.
/// </summary>
public sealed class TenantFilterCriteria
{
    /// <summary>
    /// The filter key for project name.
    /// </summary>
    public const string ProjectNameKey = "project_name";

    /// <summary>
    /// The filter key for branch name.
    /// </summary>
    public const string BranchNameKey = "branch_name";

    /// <summary>
    /// The filter key for path hash.
    /// </summary>
    public const string PathHashKey = "path_hash";

    /// <summary>
    /// The filter key for promotion level.
    /// </summary>
    public const string PromotionLevelKey = "promotion_level";

    /// <summary>
    /// Gets the project name filter value.
    /// </summary>
    public string ProjectName { get; }

    /// <summary>
    /// Gets the branch name filter value.
    /// </summary>
    public string BranchName { get; }

    /// <summary>
    /// Gets the path hash filter value.
    /// </summary>
    public string PathHash { get; }

    /// <summary>
    /// Gets the optional promotion level filter.
    /// </summary>
    public PromotionLevel? PromotionLevel { get; }

    /// <summary>
    /// Gets the optional minimum promotion level for range filtering.
    /// </summary>
    public PromotionLevel? MinimumPromotionLevel { get; }

    /// <summary>
    /// Creates a new TenantFilterCriteria instance.
    /// </summary>
    private TenantFilterCriteria(
        string projectName,
        string branchName,
        string pathHash,
        PromotionLevel? promotionLevel = null,
        PromotionLevel? minimumPromotionLevel = null)
    {
        ProjectName = projectName;
        BranchName = branchName;
        PathHash = pathHash;
        PromotionLevel = promotionLevel;
        MinimumPromotionLevel = minimumPromotionLevel;
    }

    /// <summary>
    /// Returns a dictionary of filter key-value pairs for the tenant criteria.
    /// </summary>
    public Dictionary<string, object> ToDictionary()
    {
        var result = new Dictionary<string, object>
        {
            [ProjectNameKey] = ProjectName,
            [BranchNameKey] = BranchName,
            [PathHashKey] = PathHash
        };

        if (PromotionLevel.HasValue)
        {
            result[PromotionLevelKey] = PromotionLevel.Value.ToString().ToLowerInvariant();
        }

        return result;
    }

    /// <summary>
    /// Gets all promotion levels at or above the minimum level.
    /// Useful for building OR filters.
    /// </summary>
    public IReadOnlyList<string> GetPromotionLevelsAtOrAboveMinimum()
    {
        if (!MinimumPromotionLevel.HasValue)
        {
            return Array.Empty<string>();
        }

        var levels = new List<string>();
        foreach (PromotionLevel level in Enum.GetValues<PromotionLevel>())
        {
            if (level >= MinimumPromotionLevel.Value)
            {
                levels.Add(level.ToString().ToLowerInvariant());
            }
        }

        return levels;
    }

    /// <summary>
    /// Returns a string representation of the filter criteria for debugging.
    /// </summary>
    public override string ToString()
    {
        var promo = PromotionLevel.HasValue
            ? $", promotion={PromotionLevel.Value}"
            : MinimumPromotionLevel.HasValue
                ? $", min_promotion={MinimumPromotionLevel.Value}"
                : "";

        return $"TenantFilter[{ProjectName}:{BranchName}:{PathHash[..Math.Min(8, PathHash.Length)]}{promo}]";
    }

    /// <summary>
    /// Creates filter criteria from a session context.
    /// </summary>
    public static TenantFilterCriteria FromSessionContext(ISessionContext sessionContext)
    {
        ArgumentNullException.ThrowIfNull(sessionContext);

        if (!sessionContext.IsProjectActive)
        {
            throw new InvalidOperationException(
                "Cannot create tenant filter: no project is currently active.");
        }

        return new TenantFilterCriteria(
            sessionContext.ProjectName!,
            sessionContext.ActiveBranch!,
            sessionContext.PathHash!);
    }

    /// <summary>
    /// Creates filter criteria from a session context with a specific promotion level.
    /// </summary>
    public static TenantFilterCriteria FromSessionContext(
        ISessionContext sessionContext,
        PromotionLevel promotionLevel)
    {
        ArgumentNullException.ThrowIfNull(sessionContext);

        if (!sessionContext.IsProjectActive)
        {
            throw new InvalidOperationException(
                "Cannot create tenant filter: no project is currently active.");
        }

        return new TenantFilterCriteria(
            sessionContext.ProjectName!,
            sessionContext.ActiveBranch!,
            sessionContext.PathHash!,
            promotionLevel: promotionLevel);
    }

    /// <summary>
    /// Creates filter criteria from a session context with a minimum promotion level.
    /// </summary>
    public static TenantFilterCriteria FromSessionContextWithMinimumPromotion(
        ISessionContext sessionContext,
        PromotionLevel minimumLevel)
    {
        ArgumentNullException.ThrowIfNull(sessionContext);

        if (!sessionContext.IsProjectActive)
        {
            throw new InvalidOperationException(
                "Cannot create tenant filter: no project is currently active.");
        }

        return new TenantFilterCriteria(
            sessionContext.ProjectName!,
            sessionContext.ActiveBranch!,
            sessionContext.PathHash!,
            minimumPromotionLevel: minimumLevel);
    }

    /// <summary>
    /// Creates filter criteria from explicit tenant components.
    /// </summary>
    public static TenantFilterCriteria FromComponents(
        string projectName,
        string branchName,
        string pathHash)
    {
        ArgumentNullException.ThrowIfNull(projectName);
        ArgumentNullException.ThrowIfNull(branchName);
        ArgumentNullException.ThrowIfNull(pathHash);
        ArgumentException.ThrowIfNullOrWhiteSpace(projectName, nameof(projectName));
        ArgumentException.ThrowIfNullOrWhiteSpace(branchName, nameof(branchName));
        ArgumentException.ThrowIfNullOrWhiteSpace(pathHash, nameof(pathHash));

        return new TenantFilterCriteria(projectName, branchName, pathHash);
    }

    /// <summary>
    /// Creates filter criteria from a parsed tenant key.
    /// </summary>
    public static TenantFilterCriteria FromTenantKey(string tenantKey)
    {
        var (projectName, branchName, pathHash) = TenantKeyProvider.ParseTenantKey(tenantKey);
        return new TenantFilterCriteria(projectName, branchName, pathHash);
    }

    /// <summary>
    /// Creates filter criteria for a specific tenant with a promotion level filter.
    /// </summary>
    public static TenantFilterCriteria ForTenantWithPromotionLevel(
        string tenantKey,
        PromotionLevel promotionLevel)
    {
        var (projectName, branchName, pathHash) = TenantKeyProvider.ParseTenantKey(tenantKey);
        return new TenantFilterCriteria(projectName, branchName, pathHash, promotionLevel: promotionLevel);
    }

    /// <summary>
    /// Creates filter criteria for a specific tenant with a minimum promotion level filter.
    /// </summary>
    public static TenantFilterCriteria ForTenantWithMinimumPromotionLevel(
        string tenantKey,
        PromotionLevel minimumPromotionLevel)
    {
        var (projectName, branchName, pathHash) = TenantKeyProvider.ParseTenantKey(tenantKey);
        return new TenantFilterCriteria(projectName, branchName, pathHash, minimumPromotionLevel: minimumPromotionLevel);
    }

    /// <summary>
    /// Attempts to create filter criteria from a session context.
    /// Returns null if no project is active instead of throwing.
    /// </summary>
    public static TenantFilterCriteria? TryFromSessionContext(ISessionContext? sessionContext)
    {
        if (sessionContext == null || !sessionContext.IsProjectActive)
        {
            return null;
        }

        return new TenantFilterCriteria(
            sessionContext.ProjectName!,
            sessionContext.ActiveBranch!,
            sessionContext.PathHash!);
    }
}

/// <summary>
/// Static helper class for creating tenant filters.
/// </summary>
public static class TenantFilter
{
    /// <summary>
    /// Creates tenant filter criteria from a session context.
    /// </summary>
    public static TenantFilterCriteria CreateFilter(ISessionContext sessionContext)
        => TenantFilterCriteria.FromSessionContext(sessionContext);

    /// <summary>
    /// Creates tenant filter criteria with a specific promotion level.
    /// </summary>
    public static TenantFilterCriteria CreateFilter(
        ISessionContext sessionContext,
        PromotionLevel? promotionLevel)
    {
        if (!promotionLevel.HasValue)
        {
            return TenantFilterCriteria.FromSessionContext(sessionContext);
        }

        return TenantFilterCriteria.FromSessionContext(sessionContext, promotionLevel.Value);
    }

    /// <summary>
    /// Creates tenant filter criteria with a minimum promotion level.
    /// </summary>
    public static TenantFilterCriteria CreateFilterWithMinimumPromotion(
        ISessionContext sessionContext,
        PromotionLevel minimumLevel)
        => TenantFilterCriteria.FromSessionContextWithMinimumPromotion(sessionContext, minimumLevel);

    /// <summary>
    /// Creates tenant filter criteria from explicit components.
    /// </summary>
    public static TenantFilterCriteria CreateFilter(
        string projectName,
        string branchName,
        string pathHash)
        => TenantFilterCriteria.FromComponents(projectName, branchName, pathHash);

    /// <summary>
    /// Creates tenant filter criteria from a tenant key.
    /// </summary>
    public static TenantFilterCriteria CreateFilterFromTenantKey(string tenantKey)
        => TenantFilterCriteria.FromTenantKey(tenantKey);

    /// <summary>
    /// Attempts to create tenant filter criteria from a session context.
    /// </summary>
    public static TenantFilterCriteria? TryCreateFilter(ISessionContext? sessionContext)
        => TenantFilterCriteria.TryFromSessionContext(sessionContext);
}

/// <summary>
/// Represents document promotion levels for search filtering and ranking.
/// Higher levels indicate more important/relevant documents.
/// </summary>
public enum PromotionLevel
{
    /// <summary>
    /// Standard documents with no special promotion.
    /// </summary>
    Standard = 0,

    /// <summary>
    /// Important documents that should be weighted higher in search results.
    /// </summary>
    Important = 1,

    /// <summary>
    /// Critical documents that should always be included in relevant results.
    /// </summary>
    Critical = 2
}
