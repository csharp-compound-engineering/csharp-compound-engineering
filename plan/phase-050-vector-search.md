# Phase 050: Vector Search Service

> **Status**: PLANNED
> **Category**: Database & Storage
> **Estimated Effort**: M
> **Prerequisites**: Phase 048 (PostgreSQL Vector Store Integration), Phase 029 (Embedding Service)

---

## Spec References

- [mcp-server/tools.md - Semantic Search Tool](../spec/mcp-server/tools.md#2-semantic-search-tool)
- [mcp-server/database-schema.md - Query Filtering](../spec/mcp-server/database-schema.md#query-filtering)
- [research/semantic-kernel-ollama-rag-research.md - RAG Pipeline](../research/semantic-kernel-ollama-rag-research.md#5-rag-pipeline-with-ollama)

---

## Objectives

1. Create `IVectorSearchService` interface for semantic similarity search
2. Implement `VectorSearchFilter` for tenant isolation (project, branch, path_hash)
3. Implement configurable top-k retrieval with relevance score thresholds
4. Support promotion level filtering (`standard`, `important`, `critical`)
5. Provide result ranking by similarity score
6. Handle both document and chunk collections with unified interface
7. Integrate with `IEmbeddingService` for query embedding generation

---

## Acceptance Criteria

- [ ] `IVectorSearchService` interface defined with search methods
- [ ] `VectorSearchFilter` class implements tenant context filtering
- [ ] Default relevance score threshold of 0.5 for search operations
- [ ] Higher relevance threshold of 0.7 for RAG operations (configurable via parameter)
- [ ] Promotion level filtering works correctly
- [ ] Results are ranked by descending relevance score
- [ ] Searches respect tenant isolation (project_name, branch_name, path_hash)
- [ ] Both document and chunk search supported
- [ ] Unit tests cover filter building and result ranking
- [ ] Integration tests verify actual vector search with pgvector

---

## Implementation Notes

### 1. VectorSearchFilter Class

Create a filter builder for tenant-isolated queries:

```csharp
// src/CompoundDocs.Common/Search/VectorSearchFilter.cs
namespace CompoundDocs.Common.Search;

/// <summary>
/// Builds filters for vector search operations with tenant isolation.
/// All searches must be scoped to a specific tenant context.
/// </summary>
public sealed class VectorSearchFilter
{
    /// <summary>
    /// Required: Project name for tenant isolation.
    /// </summary>
    public required string ProjectName { get; init; }

    /// <summary>
    /// Required: Git branch name for tenant isolation.
    /// </summary>
    public required string BranchName { get; init; }

    /// <summary>
    /// Required: SHA256 hash of repository path for worktree isolation.
    /// </summary>
    public required string PathHash { get; init; }

    /// <summary>
    /// Optional: Filter to specific document types.
    /// If null or empty, searches all doc types.
    /// </summary>
    public IReadOnlyList<string>? DocTypes { get; init; }

    /// <summary>
    /// Optional: Filter to specific promotion levels.
    /// If null or empty, includes all promotion levels.
    /// Valid values: "standard", "important", "critical"
    /// </summary>
    public IReadOnlyList<string>? PromotionLevels { get; init; }

    /// <summary>
    /// Creates a filter from the active project context.
    /// </summary>
    public static VectorSearchFilter FromProjectContext(IActiveProjectContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return new VectorSearchFilter
        {
            ProjectName = context.ProjectName,
            BranchName = context.BranchName,
            PathHash = context.PathHash
        };
    }

    /// <summary>
    /// Returns a new filter with the specified doc types.
    /// </summary>
    public VectorSearchFilter WithDocTypes(params string[] docTypes)
    {
        return this with { DocTypes = docTypes };
    }

    /// <summary>
    /// Returns a new filter with the specified promotion levels.
    /// </summary>
    public VectorSearchFilter WithPromotionLevels(params string[] levels)
    {
        // Validate promotion levels
        var validLevels = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "standard", "important", "critical"
        };

        foreach (var level in levels)
        {
            if (!validLevels.Contains(level))
            {
                throw new ArgumentException(
                    $"Invalid promotion level: '{level}'. Valid values: standard, important, critical",
                    nameof(levels));
            }
        }

        return this with { PromotionLevels = levels };
    }

    /// <summary>
    /// Returns a new filter for minimum promotion level (inclusive).
    /// "standard" includes all; "important" excludes standard; "critical" only critical.
    /// </summary>
    public VectorSearchFilter WithMinPromotionLevel(string minLevel)
    {
        var levels = minLevel.ToLowerInvariant() switch
        {
            "standard" => new[] { "standard", "important", "critical" },
            "important" => new[] { "important", "critical" },
            "critical" => new[] { "critical" },
            _ => throw new ArgumentException(
                $"Invalid promotion level: '{minLevel}'. Valid values: standard, important, critical",
                nameof(minLevel))
        };

        return this with { PromotionLevels = levels };
    }
}
```

### 2. Vector Search Result Types

```csharp
// src/CompoundDocs.Common/Search/VectorSearchResult.cs
namespace CompoundDocs.Common.Search;

/// <summary>
/// A single search result with document metadata and relevance score.
/// </summary>
public sealed record VectorSearchResult
{
    /// <summary>
    /// Unique identifier of the document or chunk.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Relative path to the document within the compounding-docs folder.
    /// </summary>
    public required string RelativePath { get; init; }

    /// <summary>
    /// Document title from frontmatter.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Document summary (if available).
    /// </summary>
    public string? Summary { get; init; }

    /// <summary>
    /// Document type (e.g., "problem", "insight", "codebase").
    /// </summary>
    public required string DocType { get; init; }

    /// <summary>
    /// Promotion level: "standard", "important", or "critical".
    /// </summary>
    public required string PromotionLevel { get; init; }

    /// <summary>
    /// Character count of the document content.
    /// </summary>
    public int CharCount { get; init; }

    /// <summary>
    /// Cosine similarity score (0.0 to 1.0).
    /// Higher values indicate greater semantic similarity.
    /// </summary>
    public required double RelevanceScore { get; init; }

    /// <summary>
    /// Date from document frontmatter (if available).
    /// </summary>
    public DateOnly? Date { get; init; }

    /// <summary>
    /// If this is a chunk result, the parent document ID.
    /// Null for whole-document results.
    /// </summary>
    public string? ParentDocumentId { get; init; }

    /// <summary>
    /// If this is a chunk result, the chunk index within the parent.
    /// </summary>
    public int? ChunkIndex { get; init; }

    /// <summary>
    /// If this is a chunk result, the header path (e.g., "## Section > ### Subsection").
    /// </summary>
    public string? HeaderPath { get; init; }
}

/// <summary>
/// Aggregated search results with metadata.
/// </summary>
public sealed record VectorSearchResults
{
    /// <summary>
    /// Ranked list of search results, ordered by descending relevance score.
    /// </summary>
    public required IReadOnlyList<VectorSearchResult> Results { get; init; }

    /// <summary>
    /// Total number of matches found (before limit applied).
    /// </summary>
    public int TotalMatches { get; init; }

    /// <summary>
    /// Query text that was searched.
    /// </summary>
    public required string Query { get; init; }

    /// <summary>
    /// Minimum relevance score threshold applied.
    /// </summary>
    public double MinRelevanceScore { get; init; }

    /// <summary>
    /// Maximum results requested.
    /// </summary>
    public int Limit { get; init; }
}
```

### 3. IVectorSearchService Interface

```csharp
// src/CompoundDocs.Common/Services/IVectorSearchService.cs
namespace CompoundDocs.Common.Services;

/// <summary>
/// Service for performing semantic similarity searches on vector-embedded documents.
/// All searches are tenant-isolated via VectorSearchFilter.
/// </summary>
public interface IVectorSearchService
{
    /// <summary>
    /// Default minimum relevance score for search operations.
    /// </summary>
    const double DefaultSearchRelevanceThreshold = 0.5;

    /// <summary>
    /// Default minimum relevance score for RAG operations.
    /// </summary>
    const double DefaultRagRelevanceThreshold = 0.7;

    /// <summary>
    /// Default maximum results for search operations.
    /// </summary>
    const int DefaultSearchLimit = 10;

    /// <summary>
    /// Default maximum sources for RAG operations.
    /// </summary>
    const int DefaultRagMaxSources = 3;

    /// <summary>
    /// Searches compounding documents by semantic similarity.
    /// </summary>
    /// <param name="query">The natural language search query.</param>
    /// <param name="filter">Tenant isolation and filtering options.</param>
    /// <param name="limit">Maximum number of results to return (default: 10).</param>
    /// <param name="minRelevanceScore">Minimum relevance score threshold (default: 0.5).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Ranked search results ordered by descending relevance score.</returns>
    Task<VectorSearchResults> SearchDocumentsAsync(
        string query,
        VectorSearchFilter filter,
        int limit = DefaultSearchLimit,
        double minRelevanceScore = DefaultSearchRelevanceThreshold,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches document chunks by semantic similarity.
    /// Used for large documents that have been chunked.
    /// </summary>
    /// <param name="query">The natural language search query.</param>
    /// <param name="filter">Tenant isolation and filtering options.</param>
    /// <param name="limit">Maximum number of results to return (default: 10).</param>
    /// <param name="minRelevanceScore">Minimum relevance score threshold (default: 0.5).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Ranked chunk results ordered by descending relevance score.</returns>
    Task<VectorSearchResults> SearchChunksAsync(
        string query,
        VectorSearchFilter filter,
        int limit = DefaultSearchLimit,
        double minRelevanceScore = DefaultSearchRelevanceThreshold,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches external documents by semantic similarity.
    /// External docs are read-only reference material, not compounding docs.
    /// </summary>
    /// <param name="query">The natural language search query.</param>
    /// <param name="filter">Tenant isolation and filtering options.</param>
    /// <param name="limit">Maximum number of results to return (default: 10).</param>
    /// <param name="minRelevanceScore">Minimum relevance score threshold (default: 0.7 for external).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Ranked search results ordered by descending relevance score.</returns>
    Task<VectorSearchResults> SearchExternalDocumentsAsync(
        string query,
        VectorSearchFilter filter,
        int limit = DefaultSearchLimit,
        double minRelevanceScore = DefaultRagRelevanceThreshold,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves documents by relevance for RAG context building.
    /// Optionally prepends critical documents regardless of relevance.
    /// </summary>
    /// <param name="query">The natural language query.</param>
    /// <param name="filter">Tenant isolation and filtering options.</param>
    /// <param name="maxSources">Maximum documents to retrieve (default: 3).</param>
    /// <param name="minRelevanceScore">Minimum relevance score (default: 0.7).</param>
    /// <param name="includeCritical">If true, prepends critical docs regardless of score (default: true).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Documents suitable for RAG context, ranked by relevance.</returns>
    Task<VectorSearchResults> RetrieveForRagAsync(
        string query,
        VectorSearchFilter filter,
        int maxSources = DefaultRagMaxSources,
        double minRelevanceScore = DefaultRagRelevanceThreshold,
        bool includeCritical = true,
        CancellationToken cancellationToken = default);
}
```

### 4. VectorSearchService Implementation

```csharp
// src/CompoundDocs.McpServer/Services/VectorSearchService.cs
using CompoundDocs.Common.Search;
using CompoundDocs.Common.Services;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.PgVector;

namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Production implementation of IVectorSearchService using Semantic Kernel's
/// PostgreSQL vector store connector with pgvector.
/// </summary>
public sealed class VectorSearchService : IVectorSearchService
{
    private readonly IEmbeddingService _embeddingService;
    private readonly PostgresCollection<string, CompoundDocument> _documentsCollection;
    private readonly PostgresCollection<string, DocumentChunk> _chunksCollection;
    private readonly PostgresCollection<string, ExternalDocument> _externalDocsCollection;
    private readonly PostgresCollection<string, ExternalDocumentChunk> _externalChunksCollection;
    private readonly ILogger<VectorSearchService> _logger;

    public VectorSearchService(
        IEmbeddingService embeddingService,
        PostgresCollection<string, CompoundDocument> documentsCollection,
        PostgresCollection<string, DocumentChunk> chunksCollection,
        PostgresCollection<string, ExternalDocument> externalDocsCollection,
        PostgresCollection<string, ExternalDocumentChunk> externalChunksCollection,
        ILogger<VectorSearchService> logger)
    {
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _documentsCollection = documentsCollection ?? throw new ArgumentNullException(nameof(documentsCollection));
        _chunksCollection = chunksCollection ?? throw new ArgumentNullException(nameof(chunksCollection));
        _externalDocsCollection = externalDocsCollection ?? throw new ArgumentNullException(nameof(externalDocsCollection));
        _externalChunksCollection = externalChunksCollection ?? throw new ArgumentNullException(nameof(externalChunksCollection));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<VectorSearchResults> SearchDocumentsAsync(
        string query,
        VectorSearchFilter filter,
        int limit = IVectorSearchService.DefaultSearchLimit,
        double minRelevanceScore = IVectorSearchService.DefaultSearchRelevanceThreshold,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentNullException.ThrowIfNull(filter);
        ValidateSearchParameters(limit, minRelevanceScore);

        _logger.LogDebug(
            "Searching documents: query='{Query}', project={Project}, branch={Branch}, limit={Limit}",
            query[..Math.Min(50, query.Length)],
            filter.ProjectName,
            filter.BranchName,
            limit);

        // Generate query embedding
        var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query, cancellationToken);

        // Build Semantic Kernel filter
        var skFilter = BuildSemanticKernelFilter(filter);

        // Perform vector search
        var results = new List<VectorSearchResult>();
        var searchResults = _documentsCollection.SearchAsync(
            queryEmbedding,
            top: limit * 2, // Fetch extra to account for filtering by score
            filter: skFilter);

        await foreach (var result in searchResults.WithCancellation(cancellationToken))
        {
            var score = result.Score ?? 0;

            // Filter by minimum relevance score
            if (score < minRelevanceScore)
            {
                continue;
            }

            results.Add(MapToSearchResult(result.Record, score));

            // Stop if we have enough results
            if (results.Count >= limit)
            {
                break;
            }
        }

        // Results are already ranked by score from pgvector
        return new VectorSearchResults
        {
            Results = results,
            TotalMatches = results.Count,
            Query = query,
            MinRelevanceScore = minRelevanceScore,
            Limit = limit
        };
    }

    public async Task<VectorSearchResults> SearchChunksAsync(
        string query,
        VectorSearchFilter filter,
        int limit = IVectorSearchService.DefaultSearchLimit,
        double minRelevanceScore = IVectorSearchService.DefaultSearchRelevanceThreshold,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentNullException.ThrowIfNull(filter);
        ValidateSearchParameters(limit, minRelevanceScore);

        _logger.LogDebug(
            "Searching chunks: query='{Query}', project={Project}, limit={Limit}",
            query[..Math.Min(50, query.Length)],
            filter.ProjectName,
            limit);

        var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query, cancellationToken);
        var skFilter = BuildSemanticKernelFilter(filter);

        var results = new List<VectorSearchResult>();
        var searchResults = _chunksCollection.SearchAsync(
            queryEmbedding,
            top: limit * 2,
            filter: skFilter);

        await foreach (var result in searchResults.WithCancellation(cancellationToken))
        {
            var score = result.Score ?? 0;

            if (score < minRelevanceScore)
            {
                continue;
            }

            results.Add(MapChunkToSearchResult(result.Record, score));

            if (results.Count >= limit)
            {
                break;
            }
        }

        return new VectorSearchResults
        {
            Results = results,
            TotalMatches = results.Count,
            Query = query,
            MinRelevanceScore = minRelevanceScore,
            Limit = limit
        };
    }

    public async Task<VectorSearchResults> SearchExternalDocumentsAsync(
        string query,
        VectorSearchFilter filter,
        int limit = IVectorSearchService.DefaultSearchLimit,
        double minRelevanceScore = IVectorSearchService.DefaultRagRelevanceThreshold,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentNullException.ThrowIfNull(filter);
        ValidateSearchParameters(limit, minRelevanceScore);

        _logger.LogDebug(
            "Searching external documents: query='{Query}', project={Project}, limit={Limit}",
            query[..Math.Min(50, query.Length)],
            filter.ProjectName,
            limit);

        var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query, cancellationToken);

        // External docs don't have promotion levels, simpler filter
        var skFilter = BuildTenantOnlyFilter(filter);

        var results = new List<VectorSearchResult>();
        var searchResults = _externalDocsCollection.SearchAsync(
            queryEmbedding,
            top: limit * 2,
            filter: skFilter);

        await foreach (var result in searchResults.WithCancellation(cancellationToken))
        {
            var score = result.Score ?? 0;

            if (score < minRelevanceScore)
            {
                continue;
            }

            results.Add(MapExternalDocToSearchResult(result.Record, score));

            if (results.Count >= limit)
            {
                break;
            }
        }

        return new VectorSearchResults
        {
            Results = results,
            TotalMatches = results.Count,
            Query = query,
            MinRelevanceScore = minRelevanceScore,
            Limit = limit
        };
    }

    public async Task<VectorSearchResults> RetrieveForRagAsync(
        string query,
        VectorSearchFilter filter,
        int maxSources = IVectorSearchService.DefaultRagMaxSources,
        double minRelevanceScore = IVectorSearchService.DefaultRagRelevanceThreshold,
        bool includeCritical = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentNullException.ThrowIfNull(filter);
        ValidateSearchParameters(maxSources, minRelevanceScore);

        _logger.LogDebug(
            "Retrieving for RAG: query='{Query}', maxSources={MaxSources}, includeCritical={IncludeCritical}",
            query[..Math.Min(50, query.Length)],
            maxSources,
            includeCritical);

        var results = new List<VectorSearchResult>();

        // Step 1: If includeCritical, fetch critical documents first
        if (includeCritical)
        {
            var criticalFilter = filter.WithPromotionLevels("critical");
            var criticalResults = await SearchDocumentsAsync(
                query,
                criticalFilter,
                limit: maxSources,
                minRelevanceScore: 0.0, // Include all critical docs regardless of score
                cancellationToken);

            results.AddRange(criticalResults.Results);

            _logger.LogDebug(
                "Found {Count} critical documents for RAG context",
                criticalResults.Results.Count);
        }

        // Step 2: Fill remaining slots with relevance-based search
        var remainingSlots = maxSources - results.Count;
        if (remainingSlots > 0)
        {
            var generalResults = await SearchDocumentsAsync(
                query,
                filter,
                limit: remainingSlots + results.Count, // Fetch extra to deduplicate
                minRelevanceScore: minRelevanceScore,
                cancellationToken);

            // Add non-duplicate results
            var existingIds = results.Select(r => r.Id).ToHashSet();
            foreach (var result in generalResults.Results)
            {
                if (!existingIds.Contains(result.Id))
                {
                    results.Add(result);
                    if (results.Count >= maxSources)
                    {
                        break;
                    }
                }
            }
        }

        // Step 3: Also search chunks for large documents
        var chunkResults = await SearchChunksAsync(
            query,
            filter,
            limit: maxSources,
            minRelevanceScore: minRelevanceScore,
            cancellationToken);

        // Merge chunk results by parent document, keeping highest-scoring chunk
        var existingDocIds = results.Select(r => r.Id).ToHashSet();
        foreach (var chunkResult in chunkResults.Results)
        {
            if (chunkResult.ParentDocumentId != null &&
                !existingDocIds.Contains(chunkResult.ParentDocumentId))
            {
                results.Add(chunkResult);
                existingDocIds.Add(chunkResult.ParentDocumentId);

                if (results.Count >= maxSources)
                {
                    break;
                }
            }
        }

        // Final sort by relevance score (critical docs may have lower scores but are prioritized by position)
        var sortedResults = results
            .OrderByDescending(r => r.PromotionLevel == "critical" ? 1 : 0)
            .ThenByDescending(r => r.RelevanceScore)
            .Take(maxSources)
            .ToList();

        return new VectorSearchResults
        {
            Results = sortedResults,
            TotalMatches = sortedResults.Count,
            Query = query,
            MinRelevanceScore = minRelevanceScore,
            Limit = maxSources
        };
    }

    #region Private Helper Methods

    private static void ValidateSearchParameters(int limit, double minRelevanceScore)
    {
        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be greater than 0.");
        }

        if (minRelevanceScore < 0.0 || minRelevanceScore > 1.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minRelevanceScore),
                "Minimum relevance score must be between 0.0 and 1.0.");
        }
    }

    private static VectorSearchFilter BuildSemanticKernelFilter(VectorSearchFilter filter)
    {
        // Build Semantic Kernel VectorSearchFilter with tenant isolation
        var skFilter = new Microsoft.Extensions.VectorData.VectorSearchFilter()
            .EqualTo("project_name", filter.ProjectName)
            .EqualTo("branch_name", filter.BranchName)
            .EqualTo("path_hash", filter.PathHash);

        // Add doc type filter if specified
        if (filter.DocTypes?.Count > 0)
        {
            // Note: Semantic Kernel filter supports single value equality
            // For multiple doc types, we may need to do client-side filtering
            // or use the first doc type for now
            if (filter.DocTypes.Count == 1)
            {
                skFilter = skFilter.EqualTo("doc_type", filter.DocTypes[0]);
            }
            // TODO: Handle multiple doc types with IN clause when SK supports it
        }

        // Add promotion level filter if specified
        if (filter.PromotionLevels?.Count > 0 && filter.PromotionLevels.Count == 1)
        {
            skFilter = skFilter.EqualTo("promotion_level", filter.PromotionLevels[0]);
        }

        return skFilter;
    }

    private static VectorSearchFilter BuildTenantOnlyFilter(VectorSearchFilter filter)
    {
        return new Microsoft.Extensions.VectorData.VectorSearchFilter()
            .EqualTo("project_name", filter.ProjectName)
            .EqualTo("branch_name", filter.BranchName)
            .EqualTo("path_hash", filter.PathHash);
    }

    private static VectorSearchResult MapToSearchResult(CompoundDocument doc, double score)
    {
        return new VectorSearchResult
        {
            Id = doc.Id,
            RelativePath = doc.RelativePath,
            Title = doc.Title,
            Summary = doc.Summary,
            DocType = doc.DocType,
            PromotionLevel = doc.PromotionLevel,
            CharCount = doc.CharCount,
            RelevanceScore = score,
            Date = ParseDateFromFrontmatter(doc.FrontmatterJson)
        };
    }

    private static VectorSearchResult MapChunkToSearchResult(DocumentChunk chunk, double score)
    {
        return new VectorSearchResult
        {
            Id = chunk.Id,
            RelativePath = string.Empty, // Chunks don't have path directly
            Title = chunk.HeaderPath,
            DocType = string.Empty, // Inherited from parent
            PromotionLevel = chunk.PromotionLevel,
            RelevanceScore = score,
            ParentDocumentId = chunk.DocumentId,
            ChunkIndex = chunk.ChunkIndex,
            HeaderPath = chunk.HeaderPath
        };
    }

    private static VectorSearchResult MapExternalDocToSearchResult(ExternalDocument doc, double score)
    {
        return new VectorSearchResult
        {
            Id = doc.Id,
            RelativePath = doc.RelativePath,
            Title = doc.Title,
            Summary = doc.Summary,
            DocType = "external",
            PromotionLevel = "standard", // External docs don't have promotion
            CharCount = doc.CharCount,
            RelevanceScore = score
        };
    }

    private static DateOnly? ParseDateFromFrontmatter(string? frontmatterJson)
    {
        if (string.IsNullOrEmpty(frontmatterJson))
        {
            return null;
        }

        try
        {
            var json = JsonDocument.Parse(frontmatterJson);
            if (json.RootElement.TryGetProperty("date", out var dateElement))
            {
                var dateStr = dateElement.GetString();
                if (DateOnly.TryParse(dateStr, out var date))
                {
                    return date;
                }
            }
        }
        catch
        {
            // Ignore parse errors
        }

        return null;
    }

    #endregion
}
```

### 5. Service Registration

```csharp
// In ServiceCollectionExtensions.cs
public static IServiceCollection AddVectorSearchServices(this IServiceCollection services)
{
    services.AddScoped<IVectorSearchService, VectorSearchService>();
    return services;
}
```

---

## Dependencies

### Depends On

- **Phase 048**: PostgreSQL Vector Store Integration - Provides PostgresCollection<T> instances
- **Phase 029**: Embedding Service - Provides IEmbeddingService for query embedding generation

### Blocks

- **Phase 051+**: RAG Query Tool - Uses vector search for document retrieval
- **Phase 052+**: Semantic Search Tool - Direct exposure of vector search to MCP
- **Phase 053+**: Context Builder - Uses search results for RAG context

---

## Testing Verification

### Unit Tests

```csharp
// tests/CompoundDocs.Tests/Search/VectorSearchFilterTests.cs
public class VectorSearchFilterTests
{
    [Fact]
    public void FromProjectContext_CreatesCorrectFilter()
    {
        // Arrange
        var context = new MockProjectContext
        {
            ProjectName = "test-project",
            BranchName = "main",
            PathHash = "abc123"
        };

        // Act
        var filter = VectorSearchFilter.FromProjectContext(context);

        // Assert
        Assert.Equal("test-project", filter.ProjectName);
        Assert.Equal("main", filter.BranchName);
        Assert.Equal("abc123", filter.PathHash);
    }

    [Fact]
    public void WithDocTypes_ReturnsNewFilterWithDocTypes()
    {
        // Arrange
        var original = CreateTestFilter();

        // Act
        var filtered = original.WithDocTypes("problem", "insight");

        // Assert
        Assert.Null(original.DocTypes);
        Assert.NotNull(filtered.DocTypes);
        Assert.Equal(2, filtered.DocTypes.Count);
        Assert.Contains("problem", filtered.DocTypes);
        Assert.Contains("insight", filtered.DocTypes);
    }

    [Fact]
    public void WithMinPromotionLevel_Important_ExcludesStandard()
    {
        // Arrange
        var original = CreateTestFilter();

        // Act
        var filtered = original.WithMinPromotionLevel("important");

        // Assert
        Assert.NotNull(filtered.PromotionLevels);
        Assert.Equal(2, filtered.PromotionLevels.Count);
        Assert.Contains("important", filtered.PromotionLevels);
        Assert.Contains("critical", filtered.PromotionLevels);
        Assert.DoesNotContain("standard", filtered.PromotionLevels);
    }

    [Fact]
    public void WithPromotionLevels_InvalidLevel_ThrowsArgumentException()
    {
        // Arrange
        var filter = CreateTestFilter();

        // Act & Assert
        Assert.Throws<ArgumentException>(
            () => filter.WithPromotionLevels("invalid"));
    }

    private static VectorSearchFilter CreateTestFilter()
    {
        return new VectorSearchFilter
        {
            ProjectName = "test",
            BranchName = "main",
            PathHash = "abc123"
        };
    }
}
```

```csharp
// tests/CompoundDocs.Tests/Services/VectorSearchServiceTests.cs
public class VectorSearchServiceTests
{
    [Fact]
    public async Task SearchDocumentsAsync_FiltersbyMinRelevanceScore()
    {
        // Arrange
        var mockEmbeddingService = new Mock<IEmbeddingService>();
        mockEmbeddingService
            .Setup(s => s.GenerateEmbeddingAsync(It.IsAny<string>(), default))
            .ReturnsAsync(new ReadOnlyMemory<float>(new float[1024]));

        var mockCollection = CreateMockCollection(new[]
        {
            (0.9, CreateTestDocument("high-relevance")),
            (0.6, CreateTestDocument("medium-relevance")),
            (0.3, CreateTestDocument("low-relevance"))
        });

        var service = new VectorSearchService(
            mockEmbeddingService.Object,
            mockCollection,
            /* ... other dependencies */);

        // Act
        var results = await service.SearchDocumentsAsync(
            "test query",
            CreateTestFilter(),
            limit: 10,
            minRelevanceScore: 0.5);

        // Assert
        Assert.Equal(2, results.Results.Count);
        Assert.All(results.Results, r => Assert.True(r.RelevanceScore >= 0.5));
    }

    [Fact]
    public async Task RetrieveForRagAsync_IncludesCriticalDocsFirst()
    {
        // Test that critical docs are included regardless of score
        // and appear first in results
    }

    [Fact]
    public async Task SearchDocumentsAsync_RespectsLimit()
    {
        // Test that results are limited correctly
    }

    [Fact]
    public async Task SearchDocumentsAsync_ResultsOrderedByRelevance()
    {
        // Test that results are sorted by descending relevance score
    }
}
```

### Integration Tests

```csharp
// tests/CompoundDocs.IntegrationTests/Services/VectorSearchServiceIntegrationTests.cs
[Trait("Category", "Integration")]
public class VectorSearchServiceIntegrationTests : IClassFixture<PostgresVectorFixture>
{
    private readonly IVectorSearchService _service;
    private readonly VectorSearchFilter _filter;

    public VectorSearchServiceIntegrationTests(PostgresVectorFixture fixture)
    {
        _service = fixture.GetService<IVectorSearchService>();
        _filter = new VectorSearchFilter
        {
            ProjectName = "test-project",
            BranchName = "main",
            PathHash = fixture.TestPathHash
        };
    }

    [Fact]
    public async Task SearchDocuments_WithSeedData_ReturnsRankedResults()
    {
        // Act
        var results = await _service.SearchDocumentsAsync(
            "database connection pool",
            _filter);

        // Assert
        Assert.NotEmpty(results.Results);
        Assert.True(results.Results.First().RelevanceScore >= results.Results.Last().RelevanceScore);
    }

    [Fact]
    public async Task SearchDocuments_WithDocTypeFilter_OnlyReturnsMatchingTypes()
    {
        // Arrange
        var filter = _filter.WithDocTypes("problem");

        // Act
        var results = await _service.SearchDocumentsAsync(
            "test query",
            filter);

        // Assert
        Assert.All(results.Results, r => Assert.Equal("problem", r.DocType));
    }

    [Fact]
    public async Task RetrieveForRag_WithCriticalDocs_PrioritizesCritical()
    {
        // Act
        var results = await _service.RetrieveForRagAsync(
            "test query",
            _filter,
            maxSources: 3,
            includeCritical: true);

        // Assert - critical docs should be at the top if present
        var criticalResults = results.Results.Where(r => r.PromotionLevel == "critical").ToList();
        if (criticalResults.Any())
        {
            var firstCriticalIndex = results.Results.ToList().FindIndex(r => r.PromotionLevel == "critical");
            Assert.Equal(0, firstCriticalIndex);
        }
    }
}
```

### Manual Verification

```bash
# 1. Verify HNSW index is created
psql -h localhost -p 5433 -U compounding -d compounding_docs -c "
SELECT indexname, indexdef FROM pg_indexes
WHERE tablename = 'documents' AND indexdef LIKE '%hnsw%';"

# 2. Test vector search query
psql -h localhost -p 5433 -U compounding -d compounding_docs -c "
SET hnsw.ef_search = 64;
SELECT id, title, promotion_level,
       1 - (embedding <=> '[0.1, 0.2, ...]'::vector) as similarity
FROM compounding.documents
WHERE project_name = 'test-project'
  AND branch_name = 'main'
ORDER BY embedding <=> '[0.1, 0.2, ...]'::vector
LIMIT 10;"

# 3. Check filter combinations work
psql -h localhost -p 5433 -U compounding -d compounding_docs -c "
SELECT COUNT(*) FROM compounding.documents
WHERE project_name = 'test-project'
  AND doc_type = 'problem'
  AND promotion_level IN ('important', 'critical');"
```

---

## Files Created/Modified

| File | Action | Description |
|------|--------|-------------|
| `src/CompoundDocs.Common/Search/VectorSearchFilter.cs` | Create | Filter builder for tenant-isolated searches |
| `src/CompoundDocs.Common/Search/VectorSearchResult.cs` | Create | Search result types |
| `src/CompoundDocs.Common/Services/IVectorSearchService.cs` | Create | Vector search interface |
| `src/CompoundDocs.McpServer/Services/VectorSearchService.cs` | Create | Production implementation |
| `src/CompoundDocs.McpServer/Extensions/ServiceCollectionExtensions.cs` | Modify | Add vector search registration |
| `tests/CompoundDocs.Tests/Search/VectorSearchFilterTests.cs` | Create | Filter unit tests |
| `tests/CompoundDocs.Tests/Services/VectorSearchServiceTests.cs` | Create | Service unit tests |
| `tests/CompoundDocs.IntegrationTests/Services/VectorSearchServiceIntegrationTests.cs` | Create | Integration tests |

---

## Configuration Reference

### Search Default Thresholds

| Operation | Min Relevance Score | Max Results | Notes |
|-----------|---------------------|-------------|-------|
| `semantic_search` tool | 0.5 | 10 | Lower threshold for broad discovery |
| `rag_query` tool | 0.7 | 3 | Higher threshold for relevant context |
| `search_external_docs` | 0.7 | 10 | External docs use RAG threshold |

### Promotion Level Hierarchy

| Level | Description | RAG Behavior |
|-------|-------------|--------------|
| `standard` | Default level for all documents | Included based on relevance |
| `important` | High-value documents | Included based on relevance |
| `critical` | Must-include documents | Always prepended to RAG context |

---

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| Slow vector search on large datasets | HNSW index with tuned parameters (m=32, ef_search=64) |
| Missing tenant isolation | All filters require project_name, branch_name, path_hash |
| Irrelevant results in RAG | Default 0.7 threshold; critical docs bypass for important context |
| Memory pressure from large result sets | Limit parameter with server-side enforcement |
| Inconsistent chunk/document scoring | Unified result type with source tracking |
