# Phase 052: Promotion Level Boosting Logic

> **Status**: [PLANNED]
> **Category**: Database & Storage
> **Estimated Effort**: M
> **Prerequisites**: Phase 050 (Document Storage and Retrieval)

---

## Spec References

- [doc-types/promotion.md - Relevance Boosting for Important](../spec/doc-types/promotion.md#relevance-boosting-for-important)
- [doc-types/promotion.md - Critical Document Injection](../spec/doc-types/promotion.md#critical-document-injection)
- [doc-types/promotion.md - Promotion Level Enum](../spec/doc-types/promotion.md#promotion-level-enum)
- [mcp-server/tools.md - RAG Query Tool](../spec/mcp-server/tools.md#1-rag-query-tool)
- [mcp-server/tools.md - Semantic Search Tool](../spec/mcp-server/tools.md#2-semantic-search-tool)
- [mcp-server/tools.md - Update Promotion Level Tool](../spec/mcp-server/tools.md#8-update-promotion-level-tool)
- [mcp-server/database-schema.md - CompoundDocument](../spec/mcp-server/database-schema.md#documents-schema-semantic-kernel-model)

---

## Objectives

1. Define `PromotionLevel` enum with standard, important, and critical values
2. Implement 1.5x relevance boost calculation for important documents
3. Implement critical document mandatory surfacing logic
4. Create boost calculation service for search result ranking
5. Add promotion-level filtering to vector search queries
6. Ensure atomic chunk promotion when parent document is promoted
7. Integrate boost logic with RAG and semantic search pipelines

---

## Acceptance Criteria

- [ ] `PromotionLevel` enum created with `Standard`, `Important`, `Critical` values
- [ ] `PromotionLevelExtensions` class provides string conversion methods
- [ ] `IPromotionBoostService` interface defined for boost calculations
- [ ] `PromotionBoostService` implementation applies 1.5x boost for important documents
- [ ] Critical documents receive mandatory surfacing (always included when semantically relevant)
- [ ] `VectorSearchFilter` extension methods for promotion-level filtering
- [ ] `min_promotion_level` filter implemented (returns docs at or above specified level)
- [ ] `promotion_levels` filter implemented (returns docs matching specific levels)
- [ ] `include_critical` flag injects critical docs matching query domain
- [ ] Search results sorted by boosted relevance score
- [ ] Atomic promotion update for document and all associated chunks
- [ ] Unit tests for boost calculations with various score scenarios
- [ ] Unit tests for promotion filtering logic
- [ ] Integration tests for RAG query with promotion filters

---

## Implementation Notes

### 1. PromotionLevel Enum

Create `src/CompoundDocs.McpServer/Models/PromotionLevel.cs`:

```csharp
namespace CompoundDocs.McpServer.Models;

/// <summary>
/// Document promotion levels controlling visibility in RAG queries and search.
/// </summary>
public enum PromotionLevel
{
    /// <summary>
    /// Default level. Retrieved via normal RAG/search.
    /// </summary>
    Standard = 0,

    /// <summary>
    /// Higher relevance boost (1.5x). Surfaces more readily in related queries.
    /// </summary>
    Important = 1,

    /// <summary>
    /// Required Reading. Must be surfaced before code generation in related areas.
    /// </summary>
    Critical = 2
}
```

### 2. PromotionLevel Extensions

Create `src/CompoundDocs.McpServer/Models/PromotionLevelExtensions.cs`:

```csharp
namespace CompoundDocs.McpServer.Models;

/// <summary>
/// Extension methods for PromotionLevel enum.
/// </summary>
public static class PromotionLevelExtensions
{
    /// <summary>
    /// Convert enum to database storage string (lowercase).
    /// </summary>
    public static string ToStorageString(this PromotionLevel level)
        => level switch
        {
            PromotionLevel.Standard => "standard",
            PromotionLevel.Important => "important",
            PromotionLevel.Critical => "critical",
            _ => throw new ArgumentOutOfRangeException(nameof(level))
        };

    /// <summary>
    /// Parse storage string to enum.
    /// </summary>
    public static PromotionLevel ParsePromotionLevel(string value)
        => value?.ToLowerInvariant() switch
        {
            "standard" => PromotionLevel.Standard,
            "important" => PromotionLevel.Important,
            "critical" => PromotionLevel.Critical,
            null or "" => PromotionLevel.Standard, // Default
            _ => throw new ArgumentException($"Invalid promotion level: {value}", nameof(value))
        };

    /// <summary>
    /// Check if level meets minimum threshold.
    /// </summary>
    public static bool MeetsMinimumLevel(this PromotionLevel level, PromotionLevel minimum)
        => level >= minimum;

    /// <summary>
    /// Get relevance boost multiplier for this level.
    /// </summary>
    public static float GetBoostMultiplier(this PromotionLevel level)
        => level switch
        {
            PromotionLevel.Standard => 1.0f,
            PromotionLevel.Important => 1.5f,  // Per spec: 1.5x boost
            PromotionLevel.Critical => 1.0f,   // Critical uses mandatory surfacing, not boost
            _ => 1.0f
        };
}
```

### 3. IPromotionBoostService Interface

Create `src/CompoundDocs.McpServer/Services/Abstractions/IPromotionBoostService.cs`:

```csharp
using CompoundDocs.McpServer.Models;

namespace CompoundDocs.McpServer.Services.Abstractions;

/// <summary>
/// Service for calculating relevance boosts based on promotion level.
/// </summary>
public interface IPromotionBoostService
{
    /// <summary>
    /// Apply promotion-based boost to a relevance score.
    /// </summary>
    /// <param name="rawScore">Original relevance score from vector search (0-1).</param>
    /// <param name="promotionLevel">Document's promotion level.</param>
    /// <returns>Boosted score (may exceed 1.0 for important documents).</returns>
    float ApplyBoost(float rawScore, PromotionLevel promotionLevel);

    /// <summary>
    /// Apply promotion-based boost to search results, returning sorted results.
    /// </summary>
    /// <typeparam name="T">Search result type.</typeparam>
    /// <param name="results">Raw search results.</param>
    /// <param name="scoreSelector">Function to extract raw score.</param>
    /// <param name="levelSelector">Function to extract promotion level.</param>
    /// <returns>Results with boosted scores, sorted descending.</returns>
    IReadOnlyList<SearchResultWithBoost<T>> ApplyBoostAndSort<T>(
        IEnumerable<T> results,
        Func<T, float> scoreSelector,
        Func<T, PromotionLevel> levelSelector);

    /// <summary>
    /// Identify critical documents that should be mandatorily surfaced.
    /// </summary>
    /// <typeparam name="T">Document type.</typeparam>
    /// <param name="candidates">Candidate documents from semantic search.</param>
    /// <param name="levelSelector">Function to extract promotion level.</param>
    /// <param name="minRelevanceScore">Minimum relevance to be considered related.</param>
    /// <param name="scoreSelector">Function to extract relevance score.</param>
    /// <returns>Critical documents meeting relevance threshold.</returns>
    IReadOnlyList<T> GetMandatoryCriticalDocuments<T>(
        IEnumerable<T> candidates,
        Func<T, PromotionLevel> levelSelector,
        Func<T, float> scoreSelector,
        float minRelevanceScore = 0.5f);
}

/// <summary>
/// Search result with applied boost.
/// </summary>
/// <typeparam name="T">Original result type.</typeparam>
public sealed record SearchResultWithBoost<T>(
    T Result,
    float RawScore,
    float BoostedScore,
    PromotionLevel PromotionLevel);
```

### 4. PromotionBoostService Implementation

Create `src/CompoundDocs.McpServer/Services/PromotionBoostService.cs`:

```csharp
using Microsoft.Extensions.Logging;
using CompoundDocs.McpServer.Models;
using CompoundDocs.McpServer.Services.Abstractions;

namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Production implementation of promotion-based relevance boosting.
/// </summary>
public sealed class PromotionBoostService : IPromotionBoostService
{
    private readonly ILogger<PromotionBoostService> _logger;

    /// <summary>
    /// Boost multiplier for important documents (per spec).
    /// </summary>
    private const float ImportantBoostMultiplier = 1.5f;

    public PromotionBoostService(ILogger<PromotionBoostService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public float ApplyBoost(float rawScore, PromotionLevel promotionLevel)
    {
        // Validate input score
        if (rawScore < 0)
        {
            _logger.LogWarning("Negative raw score {Score} clamped to 0", rawScore);
            rawScore = 0;
        }

        var boostedScore = promotionLevel switch
        {
            PromotionLevel.Important => rawScore * ImportantBoostMultiplier,
            _ => rawScore // Standard and Critical use raw score
        };

        _logger.LogDebug(
            "Applied boost: {PromotionLevel} | Raw: {RawScore:F4} -> Boosted: {BoostedScore:F4}",
            promotionLevel,
            rawScore,
            boostedScore);

        return boostedScore;
    }

    public IReadOnlyList<SearchResultWithBoost<T>> ApplyBoostAndSort<T>(
        IEnumerable<T> results,
        Func<T, float> scoreSelector,
        Func<T, PromotionLevel> levelSelector)
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentNullException.ThrowIfNull(scoreSelector);
        ArgumentNullException.ThrowIfNull(levelSelector);

        var boostedResults = results
            .Select(r =>
            {
                var rawScore = scoreSelector(r);
                var level = levelSelector(r);
                var boostedScore = ApplyBoost(rawScore, level);

                return new SearchResultWithBoost<T>(r, rawScore, boostedScore, level);
            })
            .OrderByDescending(r => r.BoostedScore)
            .ToList();

        _logger.LogDebug(
            "Applied boost to {Count} results. Top boosted score: {TopScore:F4}",
            boostedResults.Count,
            boostedResults.FirstOrDefault()?.BoostedScore ?? 0);

        return boostedResults.AsReadOnly();
    }

    public IReadOnlyList<T> GetMandatoryCriticalDocuments<T>(
        IEnumerable<T> candidates,
        Func<T, PromotionLevel> levelSelector,
        Func<T, float> scoreSelector,
        float minRelevanceScore = 0.5f)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(levelSelector);
        ArgumentNullException.ThrowIfNull(scoreSelector);

        var criticalDocs = candidates
            .Where(c => levelSelector(c) == PromotionLevel.Critical)
            .Where(c => scoreSelector(c) >= minRelevanceScore)
            .ToList();

        _logger.LogDebug(
            "Found {Count} mandatory critical documents with relevance >= {MinScore:F2}",
            criticalDocs.Count,
            minRelevanceScore);

        return criticalDocs.AsReadOnly();
    }
}
```

### 5. Vector Search Filter Extensions

Create `src/CompoundDocs.McpServer/Extensions/VectorSearchFilterExtensions.cs`:

```csharp
using Microsoft.Extensions.VectorData;
using CompoundDocs.McpServer.Models;

namespace CompoundDocs.McpServer.Extensions;

/// <summary>
/// Extension methods for VectorSearchFilter to support promotion-level filtering.
/// </summary>
public static class VectorSearchFilterExtensions
{
    /// <summary>
    /// Filter to documents at or above the specified promotion level.
    /// </summary>
    /// <param name="filter">The filter to extend.</param>
    /// <param name="minLevel">Minimum promotion level (inclusive).</param>
    /// <returns>Filter with promotion constraint.</returns>
    public static VectorSearchFilter WithMinPromotionLevel(
        this VectorSearchFilter filter,
        PromotionLevel minLevel)
    {
        ArgumentNullException.ThrowIfNull(filter);

        // For minimum level filtering, we need to include all levels >= minLevel
        var allowedLevels = Enum.GetValues<PromotionLevel>()
            .Where(l => l >= minLevel)
            .Select(l => l.ToStorageString())
            .ToArray();

        // Use AnyTagEqualTo for "OR" semantics across allowed levels
        return filter.AnyTagEqualTo("promotion_level", allowedLevels);
    }

    /// <summary>
    /// Filter to documents matching specific promotion levels.
    /// </summary>
    /// <param name="filter">The filter to extend.</param>
    /// <param name="levels">Promotion levels to include.</param>
    /// <returns>Filter with promotion constraint.</returns>
    public static VectorSearchFilter WithPromotionLevels(
        this VectorSearchFilter filter,
        params PromotionLevel[] levels)
    {
        ArgumentNullException.ThrowIfNull(filter);

        if (levels.Length == 0)
        {
            return filter; // No filter applied
        }

        var levelStrings = levels
            .Select(l => l.ToStorageString())
            .ToArray();

        return filter.AnyTagEqualTo("promotion_level", levelStrings);
    }

    /// <summary>
    /// Filter to a single specific promotion level.
    /// </summary>
    /// <param name="filter">The filter to extend.</param>
    /// <param name="level">Promotion level to filter to.</param>
    /// <returns>Filter with promotion constraint.</returns>
    public static VectorSearchFilter WithPromotionLevel(
        this VectorSearchFilter filter,
        PromotionLevel level)
    {
        ArgumentNullException.ThrowIfNull(filter);

        return filter.EqualTo("promotion_level", level.ToStorageString());
    }
}
```

### 6. Search Result Merger for Critical Injection

Create `src/CompoundDocs.McpServer/Services/SearchResultMerger.cs`:

```csharp
using CompoundDocs.McpServer.Models;
using CompoundDocs.McpServer.Services.Abstractions;

namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Merges critical documents with regular search results, ensuring critical
/// documents appear at the top when include_critical is enabled.
/// </summary>
public sealed class SearchResultMerger
{
    private readonly IPromotionBoostService _boostService;

    public SearchResultMerger(IPromotionBoostService boostService)
    {
        _boostService = boostService ?? throw new ArgumentNullException(nameof(boostService));
    }

    /// <summary>
    /// Merge critical documents with search results.
    /// Critical docs are prepended, duplicates removed, and results deduplicated.
    /// </summary>
    /// <typeparam name="T">Document type.</typeparam>
    /// <param name="searchResults">Regular search results (already boosted and sorted).</param>
    /// <param name="criticalDocuments">Critical documents to inject.</param>
    /// <param name="idSelector">Function to extract document ID for deduplication.</param>
    /// <param name="maxResults">Maximum total results to return.</param>
    /// <returns>Merged results with critical docs prepended.</returns>
    public IReadOnlyList<T> MergeWithCriticalDocuments<T>(
        IReadOnlyList<SearchResultWithBoost<T>> searchResults,
        IReadOnlyList<T> criticalDocuments,
        Func<T, string> idSelector,
        int maxResults)
    {
        ArgumentNullException.ThrowIfNull(searchResults);
        ArgumentNullException.ThrowIfNull(criticalDocuments);
        ArgumentNullException.ThrowIfNull(idSelector);

        // Track seen IDs for deduplication
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var merged = new List<T>(maxResults);

        // 1. Add critical documents first (they have priority)
        foreach (var critical in criticalDocuments)
        {
            var id = idSelector(critical);
            if (seenIds.Add(id) && merged.Count < maxResults)
            {
                merged.Add(critical);
            }
        }

        // 2. Add remaining search results (skip duplicates)
        foreach (var result in searchResults)
        {
            var id = idSelector(result.Result);
            if (seenIds.Add(id) && merged.Count < maxResults)
            {
                merged.Add(result.Result);
            }
        }

        return merged.AsReadOnly();
    }
}
```

### 7. Promotion Update Service for Atomic Operations

Create `src/CompoundDocs.McpServer/Services/Abstractions/IPromotionUpdateService.cs`:

```csharp
using CompoundDocs.McpServer.Models;

namespace CompoundDocs.McpServer.Services.Abstractions;

/// <summary>
/// Service for atomically updating promotion levels across documents and chunks.
/// </summary>
public interface IPromotionUpdateService
{
    /// <summary>
    /// Update promotion level for a document and all its chunks atomically.
    /// </summary>
    /// <param name="documentId">Document ID.</param>
    /// <param name="newLevel">New promotion level.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result including previous level and count of updated chunks.</returns>
    Task<PromotionUpdateResult> UpdatePromotionLevelAsync(
        string documentId,
        PromotionLevel newLevel,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a promotion level update operation.
/// </summary>
public sealed record PromotionUpdateResult(
    string DocumentId,
    PromotionLevel PreviousLevel,
    PromotionLevel NewLevel,
    int ChunksUpdated,
    bool Success);
```

Create `src/CompoundDocs.McpServer/Services/PromotionUpdateService.cs`:

```csharp
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.VectorData;
using CompoundDocs.McpServer.Models;
using CompoundDocs.McpServer.Services.Abstractions;

namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Implementation of atomic promotion level updates.
/// </summary>
public sealed class PromotionUpdateService : IPromotionUpdateService
{
    private readonly IVectorStoreRecordCollection<string, CompoundDocument> _documentsCollection;
    private readonly IVectorStoreRecordCollection<string, DocumentChunk> _chunksCollection;
    private readonly ILogger<PromotionUpdateService> _logger;

    public PromotionUpdateService(
        IVectorStoreRecordCollection<string, CompoundDocument> documentsCollection,
        IVectorStoreRecordCollection<string, DocumentChunk> chunksCollection,
        ILogger<PromotionUpdateService> logger)
    {
        _documentsCollection = documentsCollection ?? throw new ArgumentNullException(nameof(documentsCollection));
        _chunksCollection = chunksCollection ?? throw new ArgumentNullException(nameof(chunksCollection));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PromotionUpdateResult> UpdatePromotionLevelAsync(
        string documentId,
        PromotionLevel newLevel,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);

        _logger.LogInformation(
            "Updating promotion level for document {DocumentId} to {NewLevel}",
            documentId,
            newLevel);

        // 1. Get the document
        var document = await _documentsCollection.GetAsync(documentId, cancellationToken: cancellationToken);
        if (document is null)
        {
            _logger.LogWarning("Document {DocumentId} not found for promotion update", documentId);
            return new PromotionUpdateResult(documentId, PromotionLevel.Standard, newLevel, 0, Success: false);
        }

        var previousLevel = PromotionLevelExtensions.ParsePromotionLevel(document.PromotionLevel);

        if (previousLevel == newLevel)
        {
            _logger.LogDebug("Document {DocumentId} already at level {Level}, no update needed", documentId, newLevel);
            return new PromotionUpdateResult(documentId, previousLevel, newLevel, 0, Success: true);
        }

        // 2. Update the document
        document.PromotionLevel = newLevel.ToStorageString();
        await _documentsCollection.UpsertAsync(document, cancellationToken: cancellationToken);

        // 3. Find and update all associated chunks
        var chunksUpdated = 0;
        var chunkFilter = new VectorSearchFilter()
            .EqualTo("document_id", documentId);

        // Note: Using search to find chunks by document_id
        // In practice, this may need a different approach depending on SK capabilities
        // This is a placeholder for the atomic chunk update pattern
        await foreach (var chunkResult in _chunksCollection.SearchAsync(
            vector: null, // We're filtering, not searching by vector
            top: 1000, // Reasonable upper bound
            filter: chunkFilter,
            cancellationToken: cancellationToken))
        {
            var chunk = chunkResult.Record;
            chunk.PromotionLevel = newLevel.ToStorageString();
            await _chunksCollection.UpsertAsync(chunk, cancellationToken: cancellationToken);
            chunksUpdated++;
        }

        _logger.LogInformation(
            "Updated promotion level for document {DocumentId}: {Previous} -> {New}, {ChunkCount} chunks updated",
            documentId,
            previousLevel,
            newLevel,
            chunksUpdated);

        return new PromotionUpdateResult(documentId, previousLevel, newLevel, chunksUpdated, Success: true);
    }
}
```

### 8. DI Registration

Add to `src/CompoundDocs.McpServer/Extensions/PromotionServiceCollectionExtensions.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using CompoundDocs.McpServer.Services;
using CompoundDocs.McpServer.Services.Abstractions;

namespace CompoundDocs.McpServer.Extensions;

/// <summary>
/// Extension methods for registering promotion-related services.
/// </summary>
public static class PromotionServiceCollectionExtensions
{
    /// <summary>
    /// Add promotion boost and update services to the DI container.
    /// </summary>
    public static IServiceCollection AddPromotionServices(this IServiceCollection services)
    {
        services.AddSingleton<IPromotionBoostService, PromotionBoostService>();
        services.AddSingleton<IPromotionUpdateService, PromotionUpdateService>();
        services.AddSingleton<SearchResultMerger>();

        return services;
    }
}
```

### 9. Integration with RAG Query

Update RAG query to use promotion boosting (in Phase 053+):

```csharp
// Example usage in RAG query handler
public async Task<RagResponse> QueryAsync(RagQueryRequest request, CancellationToken ct)
{
    // 1. Perform vector search with optional promotion filter
    var filter = new VectorSearchFilter()
        .EqualTo("project_name", _context.ProjectName)
        .EqualTo("branch_name", _context.BranchName);

    if (request.MinPromotionLevel.HasValue)
    {
        filter = filter.WithMinPromotionLevel(request.MinPromotionLevel.Value);
    }

    var searchResults = await _vectorStore.SearchAsync(queryEmbedding, filter, ct);

    // 2. Apply boost to search results
    var boostedResults = _boostService.ApplyBoostAndSort(
        searchResults,
        r => r.Score,
        r => PromotionLevelExtensions.ParsePromotionLevel(r.Record.PromotionLevel));

    // 3. If include_critical is enabled (default: true), inject critical docs
    if (request.IncludeCritical ?? true)
    {
        var criticalDocs = _boostService.GetMandatoryCriticalDocuments(
            searchResults,
            r => PromotionLevelExtensions.ParsePromotionLevel(r.Record.PromotionLevel),
            r => r.Score,
            minRelevanceScore: 0.5f);

        var mergedResults = _resultMerger.MergeWithCriticalDocuments(
            boostedResults,
            criticalDocs.Select(c => c.Record).ToList(),
            doc => doc.Id,
            request.MaxSources ?? 3);

        return BuildResponse(mergedResults);
    }

    return BuildResponse(boostedResults.Take(request.MaxSources ?? 3).Select(r => r.Result));
}
```

---

## Dependencies

### Depends On

- **Phase 050**: Document Storage and Retrieval - Document models and collection infrastructure must exist

### Blocks

- **Phase 053+**: RAG Query Tool Implementation - Requires boost logic for result ranking
- **Phase 054+**: Semantic Search Tool Implementation - Requires promotion filtering
- **Phase 055+**: Update Promotion Level Tool - Requires atomic update service

---

## Testing Verification

After implementation, verify with:

```bash
# 1. Build succeeds
dotnet build src/CompoundDocs.McpServer/

# 2. Run unit tests for promotion logic
dotnet test tests/CompoundDocs.Tests/ --filter "FullyQualifiedName~Promotion"

# 3. Verify boost calculations
dotnet test tests/CompoundDocs.Tests/ --filter "FullyQualifiedName~BoostService"
```

---

## Unit Test Examples

### PromotionBoostServiceTests

```csharp
public class PromotionBoostServiceTests
{
    private readonly PromotionBoostService _sut;

    public PromotionBoostServiceTests()
    {
        var mockLogger = new Mock<ILogger<PromotionBoostService>>();
        _sut = new PromotionBoostService(mockLogger.Object);
    }

    [Theory]
    [InlineData(0.8f, PromotionLevel.Standard, 0.8f)]
    [InlineData(0.8f, PromotionLevel.Important, 1.2f)]  // 0.8 * 1.5 = 1.2
    [InlineData(0.8f, PromotionLevel.Critical, 0.8f)]
    [InlineData(0.6f, PromotionLevel.Important, 0.9f)]  // 0.6 * 1.5 = 0.9
    public void ApplyBoost_ReturnsCorrectScore(float raw, PromotionLevel level, float expected)
    {
        var result = _sut.ApplyBoost(raw, level);
        Assert.Equal(expected, result, precision: 4);
    }

    [Fact]
    public void ApplyBoostAndSort_SortsImportantAboveStandard()
    {
        // Arrange
        var results = new[]
        {
            new TestDoc("A", 0.9f, PromotionLevel.Standard),   // Boosted: 0.9
            new TestDoc("B", 0.7f, PromotionLevel.Important),  // Boosted: 1.05
            new TestDoc("C", 0.8f, PromotionLevel.Standard),   // Boosted: 0.8
        };

        // Act
        var boosted = _sut.ApplyBoostAndSort(
            results,
            r => r.Score,
            r => r.Level);

        // Assert
        Assert.Equal("B", boosted[0].Result.Id);  // Important with 1.05
        Assert.Equal("A", boosted[1].Result.Id);  // Standard with 0.9
        Assert.Equal("C", boosted[2].Result.Id);  // Standard with 0.8
    }

    [Fact]
    public void GetMandatoryCriticalDocuments_FiltersBelowThreshold()
    {
        // Arrange
        var candidates = new[]
        {
            new TestDoc("A", 0.8f, PromotionLevel.Critical),  // Above threshold
            new TestDoc("B", 0.4f, PromotionLevel.Critical),  // Below threshold
            new TestDoc("C", 0.9f, PromotionLevel.Standard),  // Not critical
        };

        // Act
        var critical = _sut.GetMandatoryCriticalDocuments(
            candidates,
            c => c.Level,
            c => c.Score,
            minRelevanceScore: 0.5f);

        // Assert
        Assert.Single(critical);
        Assert.Equal("A", critical[0].Id);
    }

    private record TestDoc(string Id, float Score, PromotionLevel Level);
}
```

---

## Files Created/Modified

| File | Action | Description |
|------|--------|-------------|
| `src/CompoundDocs.McpServer/Models/PromotionLevel.cs` | Create | Promotion level enum |
| `src/CompoundDocs.McpServer/Models/PromotionLevelExtensions.cs` | Create | Enum helper methods |
| `src/CompoundDocs.McpServer/Services/Abstractions/IPromotionBoostService.cs` | Create | Boost service interface |
| `src/CompoundDocs.McpServer/Services/PromotionBoostService.cs` | Create | Boost service implementation |
| `src/CompoundDocs.McpServer/Services/Abstractions/IPromotionUpdateService.cs` | Create | Update service interface |
| `src/CompoundDocs.McpServer/Services/PromotionUpdateService.cs` | Create | Atomic update implementation |
| `src/CompoundDocs.McpServer/Services/SearchResultMerger.cs` | Create | Critical doc injection merger |
| `src/CompoundDocs.McpServer/Extensions/VectorSearchFilterExtensions.cs` | Create | Promotion filter extensions |
| `src/CompoundDocs.McpServer/Extensions/PromotionServiceCollectionExtensions.cs` | Create | DI registration |
| `tests/CompoundDocs.Tests/Services/PromotionBoostServiceTests.cs` | Create | Unit tests |
| `tests/CompoundDocs.Tests/Services/PromotionUpdateServiceTests.cs` | Create | Unit tests |
| `tests/CompoundDocs.Tests/Models/PromotionLevelExtensionsTests.cs` | Create | Unit tests |

---

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| Boost calculation edge cases | Clamp negative scores, log warnings for unexpected values |
| Atomic update failures | Transaction-like pattern with rollback on failure |
| Performance with many chunks | Batch chunk updates, consider parallel processing |
| Critical doc explosion | Min relevance threshold prevents low-relevance critical docs from surfacing |
| Filter API changes | Isolate behind extension methods for easy adaptation |

---

## Notes

- The 1.5x boost for important documents means an important doc with 0.7 relevance (boosted to 1.05) will rank above a standard doc with 0.9 relevance
- Critical documents use mandatory surfacing rather than boosting to ensure they always appear when semantically relevant
- The `include_critical` flag defaults to `true` per spec, ensuring critical knowledge surfaces by default
- Chunk promotion is inherited from parent document and updated atomically to maintain consistency
- Boosted scores can exceed 1.0, which is intentional for ranking purposes
