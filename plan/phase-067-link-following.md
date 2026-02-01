# Phase 067: Link Depth Following for RAG

> **Status**: NOT_STARTED
> **Effort Estimate**: 6-8 hours
> **Category**: Document Processing
> **Prerequisites**: Phase 065 (RAG Pipeline Core), Phase 016 (QuikGraph Integration)

---

## Spec References

This phase implements link following for RAG context expansion as defined in:

- **spec/configuration.md** - [Link Resolution Settings](../spec/configuration.md#retrieval-settings) (lines 138-148, 239-246)
- **spec/mcp-server/tools.md** - [RAG Query Tool](../spec/mcp-server/tools.md#1-rag-query-tool) (lines 34-88)
- **research/dotnet-graph-libraries.md** - QuikGraph traversal patterns

---

## Objectives

1. Implement configurable link depth traversal from project configuration
2. Build breadth-first traversal algorithm for related document discovery
3. Create link relevance scoring to prioritize most relevant linked documents
4. Integrate linked documents into RAG context building
5. Implement performance optimizations for deep link traversal
6. Handle circular references gracefully during traversal

---

## Acceptance Criteria

### Configuration Integration

- [ ] `link_resolution.max_depth` read from project configuration (default: 2)
- [ ] `retrieval.max_linked_docs` respected for total linked documents (default: 5)
- [ ] Configuration overridable per-tool-call via `RagGenerationOptions`
- [ ] Zero depth disables link following entirely

### Breadth-First Traversal

- [ ] `ILinkTraversalService` interface defined with BFS traversal methods
- [ ] `BreadthFirstLinkTraversalService` implementation using QuikGraph
- [ ] Traversal respects max depth limit from configuration
- [ ] Traversal respects max linked docs limit from configuration
- [ ] Visited document tracking prevents infinite loops on circular references
- [ ] Traversal starts from all primary RAG results (not just first document)

### Related Document Inclusion

- [ ] `LinkedDocumentResult` model captures link relationship metadata
- [ ] Linked documents include `linked_from` path for source attribution
- [ ] Linked documents include `link_depth` indicating hops from primary result
- [ ] RAG context builder integrates linked documents after primary results
- [ ] Context window management accounts for linked document size

### Link Relevance Scoring

- [ ] `ILinkRelevanceScorer` interface for scoring linked document relevance
- [ ] Base relevance score decays with link depth (e.g., 0.9^depth)
- [ ] Promotion level boosts linked document scores (critical > important > standard)
- [ ] Documents linked from multiple primary results get score boost
- [ ] Linked documents sorted by computed relevance score before inclusion

### Performance Optimizations

- [ ] Batch document retrieval for linked documents (single DB query)
- [ ] Early termination when max_linked_docs limit reached
- [ ] Caching of link graph in memory (already provided by QuikGraph integration)
- [ ] Parallel embedding generation for linked document content (if needed)
- [ ] Metrics/telemetry for link traversal duration and document counts

### Error Handling

- [ ] Missing linked documents logged as warnings, not errors
- [ ] Broken links (documents that don't exist) gracefully skipped
- [ ] Timeout handling for slow traversals with large link graphs
- [ ] Clear error messages when link graph is unavailable

---

## Implementation Notes

### ILinkTraversalService Interface

Create `Services/ILinkTraversalService.cs`:

```csharp
namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Service for traversing document links to expand RAG context.
/// </summary>
public interface ILinkTraversalService
{
    /// <summary>
    /// Gets linked documents from the provided source documents using BFS traversal.
    /// </summary>
    /// <param name="sourceDocumentPaths">Paths of primary RAG result documents.</param>
    /// <param name="maxDepth">Maximum link depth to traverse (0 disables).</param>
    /// <param name="maxLinkedDocs">Maximum total linked documents to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Linked documents with relevance metadata.</returns>
    Task<IReadOnlyList<LinkedDocumentResult>> GetLinkedDocumentsAsync(
        IEnumerable<string> sourceDocumentPaths,
        int maxDepth,
        int maxLinkedDocs,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets linked documents using configuration defaults.
    /// </summary>
    Task<IReadOnlyList<LinkedDocumentResult>> GetLinkedDocumentsAsync(
        IEnumerable<string> sourceDocumentPaths,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of link traversal including relationship metadata.
/// </summary>
public record LinkedDocumentResult(
    string Path,
    string Title,
    string Content,
    int CharCount,
    string LinkedFrom,
    int LinkDepth,
    double RelevanceScore,
    string PromotionLevel);
```

### BreadthFirstLinkTraversalService Implementation

Create `Services/BreadthFirstLinkTraversalService.cs`:

```csharp
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using CompoundDocs.Common.Graphs;
using CompoundDocs.McpServer.Options;
using CompoundDocs.McpServer.Repositories;

namespace CompoundDocs.McpServer.Services;

/// <summary>
/// BFS-based link traversal service for RAG context expansion.
/// </summary>
public class BreadthFirstLinkTraversalService : ILinkTraversalService
{
    private readonly IDocumentLinkGraph _linkGraph;
    private readonly IDocumentRepository _documentRepository;
    private readonly ILinkRelevanceScorer _relevanceScorer;
    private readonly IOptions<ProjectConfiguration> _projectConfig;
    private readonly ILogger<BreadthFirstLinkTraversalService> _logger;

    public BreadthFirstLinkTraversalService(
        IDocumentLinkGraph linkGraph,
        IDocumentRepository documentRepository,
        ILinkRelevanceScorer relevanceScorer,
        IOptions<ProjectConfiguration> projectConfig,
        ILogger<BreadthFirstLinkTraversalService> logger)
    {
        _linkGraph = linkGraph;
        _documentRepository = documentRepository;
        _relevanceScorer = relevanceScorer;
        _projectConfig = projectConfig;
        _logger = logger;
    }

    public Task<IReadOnlyList<LinkedDocumentResult>> GetLinkedDocumentsAsync(
        IEnumerable<string> sourceDocumentPaths,
        CancellationToken cancellationToken = default)
    {
        var config = _projectConfig.Value;
        return GetLinkedDocumentsAsync(
            sourceDocumentPaths,
            config.LinkResolution?.MaxDepth ?? 2,
            config.Retrieval?.MaxLinkedDocs ?? 5,
            cancellationToken);
    }

    public async Task<IReadOnlyList<LinkedDocumentResult>> GetLinkedDocumentsAsync(
        IEnumerable<string> sourceDocumentPaths,
        int maxDepth,
        int maxLinkedDocs,
        CancellationToken cancellationToken = default)
    {
        if (maxDepth <= 0 || maxLinkedDocs <= 0)
        {
            _logger.LogDebug("Link following disabled (maxDepth={MaxDepth}, maxLinkedDocs={MaxLinkedDocs})",
                maxDepth, maxLinkedDocs);
            return Array.Empty<LinkedDocumentResult>();
        }

        var sourcePaths = sourceDocumentPaths.ToList();
        if (sourcePaths.Count == 0)
        {
            return Array.Empty<LinkedDocumentResult>();
        }

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // BFS traversal state
        var visited = new HashSet<string>(sourcePaths); // Source docs already in context
        var linkedPathsWithMetadata = new List<(string Path, string LinkedFrom, int Depth)>();
        var currentLevel = new Queue<(string Path, string LinkedFrom)>();

        // Initialize BFS with outgoing links from all source documents
        foreach (var sourcePath in sourcePaths)
        {
            currentLevel.Enqueue((sourcePath, sourcePath));
        }

        // BFS traversal
        for (int depth = 1; depth <= maxDepth && linkedPathsWithMetadata.Count < maxLinkedDocs; depth++)
        {
            var nextLevel = new Queue<(string Path, string LinkedFrom)>();

            while (currentLevel.Count > 0 && linkedPathsWithMetadata.Count < maxLinkedDocs)
            {
                var (currentPath, originalSource) = currentLevel.Dequeue();

                // Get outgoing links from current document
                var linkedPaths = _linkGraph.GetLinkedDocuments(currentPath, 1, maxLinkedDocs * 2);

                foreach (var linkedPath in linkedPaths)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    if (visited.Add(linkedPath))
                    {
                        linkedPathsWithMetadata.Add((linkedPath, originalSource, depth));
                        nextLevel.Enqueue((linkedPath, originalSource));

                        if (linkedPathsWithMetadata.Count >= maxLinkedDocs)
                            break;
                    }
                }
            }

            currentLevel = nextLevel;
        }

        _logger.LogDebug(
            "BFS traversal found {LinkCount} linked documents from {SourceCount} sources in {ElapsedMs}ms",
            linkedPathsWithMetadata.Count,
            sourcePaths.Count,
            stopwatch.ElapsedMilliseconds);

        if (linkedPathsWithMetadata.Count == 0)
        {
            return Array.Empty<LinkedDocumentResult>();
        }

        // Batch retrieve documents from database
        var pathsToRetrieve = linkedPathsWithMetadata.Select(x => x.Path).ToList();
        var documents = await _documentRepository.GetDocumentsByPathsAsync(
            pathsToRetrieve,
            cancellationToken);

        // Build results with relevance scoring
        var documentsByPath = documents.ToDictionary(d => d.RelativePath);
        var results = new List<LinkedDocumentResult>();

        foreach (var (path, linkedFrom, depth) in linkedPathsWithMetadata)
        {
            if (!documentsByPath.TryGetValue(path, out var doc))
            {
                _logger.LogWarning("Linked document not found in database: {Path}", path);
                continue;
            }

            var relevanceScore = _relevanceScorer.ComputeScore(
                depth: depth,
                promotionLevel: doc.PromotionLevel,
                linkCount: linkedPathsWithMetadata.Count(x => x.Path == path));

            results.Add(new LinkedDocumentResult(
                Path: doc.RelativePath,
                Title: doc.Title,
                Content: doc.Content,
                CharCount: doc.CharCount,
                LinkedFrom: linkedFrom,
                LinkDepth: depth,
                RelevanceScore: relevanceScore,
                PromotionLevel: doc.PromotionLevel));
        }

        // Sort by relevance score descending
        var sortedResults = results
            .OrderByDescending(r => r.RelevanceScore)
            .Take(maxLinkedDocs)
            .ToList();

        stopwatch.Stop();
        _logger.LogInformation(
            "Link traversal completed: {ResultCount} linked docs from {SourceCount} sources, " +
            "max depth {MaxDepth}, total time {ElapsedMs}ms",
            sortedResults.Count,
            sourcePaths.Count,
            maxDepth,
            stopwatch.ElapsedMilliseconds);

        return sortedResults;
    }
}
```

### ILinkRelevanceScorer Interface and Implementation

Create `Services/ILinkRelevanceScorer.cs`:

```csharp
namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Computes relevance scores for linked documents.
/// </summary>
public interface ILinkRelevanceScorer
{
    /// <summary>
    /// Computes a relevance score for a linked document.
    /// </summary>
    /// <param name="depth">Link depth from primary result (1 = directly linked).</param>
    /// <param name="promotionLevel">Document promotion level.</param>
    /// <param name="linkCount">Number of primary results linking to this document.</param>
    /// <returns>Relevance score between 0 and 1.</returns>
    double ComputeScore(int depth, string promotionLevel, int linkCount);
}

/// <summary>
/// Default implementation using depth decay and promotion level boosts.
/// </summary>
public class DepthDecayLinkRelevanceScorer : ILinkRelevanceScorer
{
    // Decay factor per depth level (0.9 = 10% reduction per hop)
    private const double DepthDecayFactor = 0.9;

    // Base score for depth 1 linked documents
    private const double BaseScore = 0.8;

    // Promotion level multipliers
    private static readonly Dictionary<string, double> PromotionMultipliers = new()
    {
        { "critical", 1.3 },
        { "important", 1.15 },
        { "standard", 1.0 }
    };

    // Bonus for documents linked from multiple primary results
    private const double MultiLinkBonusPerLink = 0.05;
    private const double MaxMultiLinkBonus = 0.2;

    public double ComputeScore(int depth, string promotionLevel, int linkCount)
    {
        // Base score with depth decay
        var score = BaseScore * Math.Pow(DepthDecayFactor, depth - 1);

        // Apply promotion level multiplier
        var promotionMultiplier = PromotionMultipliers.GetValueOrDefault(
            promotionLevel?.ToLowerInvariant() ?? "standard",
            1.0);
        score *= promotionMultiplier;

        // Apply multi-link bonus (capped)
        var multiLinkBonus = Math.Min((linkCount - 1) * MultiLinkBonusPerLink, MaxMultiLinkBonus);
        score += multiLinkBonus;

        // Clamp to [0, 1]
        return Math.Clamp(score, 0.0, 1.0);
    }
}
```

### Integration with RAG Context Builder

Update the RAG generation service to include linked documents:

```csharp
// In SemanticKernelRagGenerationService.cs

public async Task<RagResponse> GenerateResponseAsync(
    string query,
    IReadOnlyList<RetrievedDocument> documents,
    RagGenerationOptions? options = null,
    CancellationToken cancellationToken = default)
{
    options ??= new RagGenerationOptions();
    var stopwatch = System.Diagnostics.Stopwatch.StartNew();

    // Get linked documents via BFS traversal
    IReadOnlyList<LinkedDocumentResult> linkedDocs = Array.Empty<LinkedDocumentResult>();
    if (options.IncludeLinkedDocs && documents.Count > 0)
    {
        var sourcePaths = documents.Select(d => d.Path);
        linkedDocs = await _linkTraversalService.GetLinkedDocumentsAsync(
            sourcePaths,
            options.MaxLinkDepth,
            options.MaxLinkedDocs,
            cancellationToken);
    }

    _logger.LogDebug(
        "Generating RAG response for query with {DocumentCount} primary documents " +
        "and {LinkedCount} linked documents",
        documents.Count,
        linkedDocs.Count);

    // Build context respecting token limits (primary docs first, then linked)
    var (contextText, includedDocs, includedLinkedDocs) = BuildContextWithLinks(
        documents, linkedDocs, options);

    // ... rest of generation logic ...

    return new RagResponse(
        Answer: answer,
        Sources: includedDocs.Select(d => new SourceAttribution(
            d.Path, d.Title, d.CharCount, d.RelevanceScore)).ToList(),
        LinkedDocs: includedLinkedDocs.Select(d => new LinkedDocumentAttribution(
            d.Path, d.Title, d.CharCount, d.LinkedFrom)).ToList(),
        ProcessingTime: stopwatch.Elapsed);
}

private (string Context, List<RetrievedDocument> IncludedDocs, List<LinkedDocumentResult> IncludedLinkedDocs)
    BuildContextWithLinks(
        IReadOnlyList<RetrievedDocument> documents,
        IReadOnlyList<LinkedDocumentResult> linkedDocs,
        RagGenerationOptions options)
{
    var availableTokens = options.MaxContextTokens - options.ReservedResponseTokens;
    var usedTokens = EstimateTokens(DefaultSystemPrompt);

    var contextBuilder = new StringBuilder();
    var includedDocs = new List<RetrievedDocument>();
    var includedLinkedDocs = new List<LinkedDocumentResult>();

    // Sort primary documents: critical first, then by relevance score
    var sortedDocs = documents
        .OrderByDescending(d => d.PromotionLevel == "critical" ? 2 :
                               d.PromotionLevel == "important" ? 1 : 0)
        .ThenByDescending(d => d.RelevanceScore)
        .ToList();

    contextBuilder.AppendLine("Primary Documents:");
    contextBuilder.AppendLine();

    // Add primary documents
    foreach (var doc in sortedDocs)
    {
        var docSection = FormatDocumentSection(doc);
        var docTokens = EstimateTokens(docSection);

        if (usedTokens + docTokens > availableTokens)
        {
            _logger.LogDebug(
                "Excluding primary document {Path} due to context window limit",
                doc.Path);
            continue;
        }

        contextBuilder.Append(docSection);
        includedDocs.Add(doc);
        usedTokens += docTokens;
    }

    // Add linked documents if space permits
    if (linkedDocs.Count > 0)
    {
        contextBuilder.AppendLine();
        contextBuilder.AppendLine("Related Documents (via links):");
        contextBuilder.AppendLine();

        foreach (var linkedDoc in linkedDocs)
        {
            var docSection = FormatLinkedDocumentSection(linkedDoc);
            var docTokens = EstimateTokens(docSection);

            if (usedTokens + docTokens > availableTokens)
            {
                _logger.LogDebug(
                    "Excluding linked document {Path} due to context window limit",
                    linkedDoc.Path);
                continue;
            }

            contextBuilder.Append(docSection);
            includedLinkedDocs.Add(linkedDoc);
            usedTokens += docTokens;
        }
    }

    if (includedDocs.Count == 0)
    {
        contextBuilder.AppendLine("No relevant documents found for this query.");
    }

    _logger.LogDebug(
        "Built context with {PrimaryCount} primary and {LinkedCount} linked documents " +
        "using approximately {TokenCount} tokens",
        includedDocs.Count,
        includedLinkedDocs.Count,
        usedTokens);

    return (contextBuilder.ToString(), includedDocs, includedLinkedDocs);
}

private static string FormatLinkedDocumentSection(LinkedDocumentResult doc)
{
    var sb = new StringBuilder();
    sb.AppendLine($"--- Linked Document: {doc.Title} ---");
    sb.AppendLine($"Path: {doc.Path}");
    sb.AppendLine($"Linked from: {doc.LinkedFrom}");
    sb.AppendLine($"Link depth: {doc.LinkDepth}");

    if (!string.IsNullOrEmpty(doc.PromotionLevel) && doc.PromotionLevel != "standard")
    {
        sb.AppendLine($"Priority: {doc.PromotionLevel}");
    }

    sb.AppendLine();
    sb.AppendLine(doc.Content);
    sb.AppendLine();

    return sb.ToString();
}
```

### Extended RagGenerationOptions

```csharp
/// <summary>
/// Options for RAG generation including link following.
/// </summary>
public class RagGenerationOptions
{
    /// <summary>
    /// Maximum tokens for the context window.
    /// </summary>
    public int MaxContextTokens { get; set; } = 24000;

    /// <summary>
    /// Tokens reserved for the response generation.
    /// </summary>
    public int ReservedResponseTokens { get; set; } = 2000;

    /// <summary>
    /// Custom system prompt override (null uses default).
    /// </summary>
    public string? SystemPromptOverride { get; set; }

    /// <summary>
    /// Whether to include linked documents in context.
    /// </summary>
    public bool IncludeLinkedDocs { get; set; } = true;

    /// <summary>
    /// Maximum link depth to follow (0 disables, default from config: 2).
    /// </summary>
    public int MaxLinkDepth { get; set; } = 2;

    /// <summary>
    /// Maximum number of linked documents to include (default from config: 5).
    /// </summary>
    public int MaxLinkedDocs { get; set; } = 5;
}
```

### Service Registration

Add to `Extensions/ServiceCollectionExtensions.cs`:

```csharp
/// <summary>
/// Registers link traversal services for RAG context expansion.
/// </summary>
public static IServiceCollection AddLinkTraversalServices(this IServiceCollection services)
{
    services.AddSingleton<ILinkRelevanceScorer, DepthDecayLinkRelevanceScorer>();
    services.AddScoped<ILinkTraversalService, BreadthFirstLinkTraversalService>();

    return services;
}
```

---

## Configuration Schema

The following configuration settings control link following behavior:

```json
{
  "link_resolution": {
    "max_depth": 2
  },
  "retrieval": {
    "max_linked_docs": 5
  }
}
```

### Configuration Mapping

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `link_resolution.max_depth` | int | 2 | Maximum BFS depth for link traversal |
| `retrieval.max_linked_docs` | int | 5 | Maximum linked documents in RAG context |

---

## Performance Considerations

### Depth vs Performance Trade-offs

| Max Depth | Typical Doc Count | Avg Traversal Time | Context Size Impact |
|-----------|-------------------|-------------------|---------------------|
| 1 | 3-8 docs | <10ms | Minimal |
| 2 (default) | 10-25 docs | 10-50ms | Moderate |
| 3 | 30-100 docs | 50-200ms | Significant |
| 4+ | 100+ docs | 200ms+ | May hit limits |

### Optimization Strategies

1. **Early Termination**: Stop BFS when `max_linked_docs` is reached
2. **Batch DB Queries**: Single query for all linked documents
3. **Memory Caching**: QuikGraph maintains link graph in memory
4. **Parallel Retrieval**: Document content fetched in parallel (future optimization)
5. **Index on RelativePath**: Ensure DB index exists for path lookups

### Metrics to Track

```csharp
// Recommended metrics
- link_traversal_duration_ms (histogram)
- link_traversal_depth_reached (histogram)
- linked_documents_found (counter)
- linked_documents_included_in_context (counter)
- missing_linked_documents (counter)
```

---

## Dependencies

### Depends On

- **Phase 016**: QuikGraph Integration - Provides `IDocumentLinkGraph` for traversal
- **Phase 015**: Markdown Parser - Link extraction for graph building
- **Phase 032**: RAG Generation Service - Integration point for linked docs
- **Phase 065**: RAG Pipeline Core - Base RAG infrastructure

### Blocks

- **Phase XXX**: Advanced link scoring (semantic similarity of linked docs)
- **Phase XXX**: Bidirectional link following (documents that link TO primary results)
- Integration testing with full RAG pipeline

---

## Verification Steps

After completing this phase, verify:

1. **Service registration**:
   ```bash
   dotnet build src/CompoundDocs.McpServer/
   # Should compile without errors
   ```

2. **Unit tests pass**:
   ```bash
   dotnet test tests/CompoundDocs.McpServer.Tests/ --filter "LinkTraversal"
   ```

3. **Configuration respects defaults**:
   ```csharp
   [Fact]
   public async Task GetLinkedDocumentsAsync_UsesConfigDefaults()
   {
       // Arrange
       var config = new ProjectConfiguration
       {
           LinkResolution = new LinkResolutionOptions { MaxDepth = 3 },
           Retrieval = new RetrievalOptions { MaxLinkedDocs = 10 }
       };
       var service = CreateService(config);

       // Act
       var results = await service.GetLinkedDocumentsAsync(
           new[] { "source.md" });

       // Assert - verify config values were used
   }
   ```

4. **BFS traversal correctness**:
   ```csharp
   [Fact]
   public async Task GetLinkedDocumentsAsync_RespectsMaxDepth()
   {
       // Arrange - create chain: A -> B -> C -> D
       _linkGraph.UpdateDocumentLinks("A.md", new[] { "B.md" });
       _linkGraph.UpdateDocumentLinks("B.md", new[] { "C.md" });
       _linkGraph.UpdateDocumentLinks("C.md", new[] { "D.md" });

       // Act - max depth 2
       var results = await _service.GetLinkedDocumentsAsync(
           new[] { "A.md" }, maxDepth: 2, maxLinkedDocs: 10);

       // Assert - should include B (depth 1) and C (depth 2), not D
       Assert.Equal(2, results.Count);
       Assert.Contains(results, r => r.Path == "B.md" && r.LinkDepth == 1);
       Assert.Contains(results, r => r.Path == "C.md" && r.LinkDepth == 2);
   }
   ```

5. **Circular reference handling**:
   ```csharp
   [Fact]
   public async Task GetLinkedDocumentsAsync_HandlesCircularReferences()
   {
       // Arrange - create cycle: A -> B -> C -> A
       _linkGraph.UpdateDocumentLinks("A.md", new[] { "B.md" });
       _linkGraph.UpdateDocumentLinks("B.md", new[] { "C.md" });
       _linkGraph.UpdateDocumentLinks("C.md", new[] { "A.md" });

       // Act
       var results = await _service.GetLinkedDocumentsAsync(
           new[] { "A.md" }, maxDepth: 5, maxLinkedDocs: 10);

       // Assert - should not loop infinitely, A not re-added
       Assert.Equal(2, results.Count); // B and C only
       Assert.DoesNotContain(results, r => r.Path == "A.md");
   }
   ```

6. **Relevance scoring**:
   ```csharp
   [Fact]
   public void ComputeScore_DecaysWithDepth()
   {
       var scorer = new DepthDecayLinkRelevanceScorer();

       var depth1Score = scorer.ComputeScore(1, "standard", 1);
       var depth2Score = scorer.ComputeScore(2, "standard", 1);
       var depth3Score = scorer.ComputeScore(3, "standard", 1);

       Assert.True(depth1Score > depth2Score);
       Assert.True(depth2Score > depth3Score);
   }

   [Fact]
   public void ComputeScore_BoostsCriticalDocs()
   {
       var scorer = new DepthDecayLinkRelevanceScorer();

       var standardScore = scorer.ComputeScore(1, "standard", 1);
       var criticalScore = scorer.ComputeScore(1, "critical", 1);

       Assert.True(criticalScore > standardScore);
   }
   ```

---

## Files Created/Modified

| File | Action | Description |
|------|--------|-------------|
| `src/CompoundDocs.McpServer/Services/ILinkTraversalService.cs` | Create | Interface definition |
| `src/CompoundDocs.McpServer/Services/BreadthFirstLinkTraversalService.cs` | Create | BFS implementation |
| `src/CompoundDocs.McpServer/Services/ILinkRelevanceScorer.cs` | Create | Scoring interface |
| `src/CompoundDocs.McpServer/Services/DepthDecayLinkRelevanceScorer.cs` | Create | Scoring implementation |
| `src/CompoundDocs.McpServer/Models/LinkedDocumentResult.cs` | Create | Result model |
| `src/CompoundDocs.McpServer/Models/RagGenerationOptions.cs` | Modify | Add link following options |
| `src/CompoundDocs.McpServer/Services/SemanticKernelRagGenerationService.cs` | Modify | Integrate link following |
| `src/CompoundDocs.McpServer/Extensions/ServiceCollectionExtensions.cs` | Modify | Register services |
| `tests/CompoundDocs.McpServer.Tests/Services/LinkTraversalServiceTests.cs` | Create | Unit tests |
| `tests/CompoundDocs.McpServer.Tests/Services/LinkRelevanceScorerTests.cs` | Create | Scoring tests |

---

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| Deep traversal causes timeouts | Configurable max_depth, early termination, timeout handling |
| Large link graphs consume memory | QuikGraph efficient for expected scale (<10K docs) |
| Circular references cause infinite loops | Visited set tracking, tested with cycle scenarios |
| Missing documents break traversal | Graceful skip with warning log, continue processing |
| Context window exceeded | Token-aware context builder, linked docs added last |
| DB query performance | Batch retrieval, index on relative_path column |
| Score computation overhead | Simple math operations, negligible impact |

---

## Notes

- The BFS algorithm ensures documents closer to primary results are discovered first
- Link depth 1 means directly linked from a primary result; depth 2 means linked from a depth-1 document
- Source documents (primary RAG results) are never included in linked document results
- Documents linked from multiple primary results are discovered once but may receive score boosts
- The scoring algorithm is intentionally simple; semantic similarity scoring deferred to future enhancement
- Consider adding "reverse links" (documents that link TO primary results) as a future enhancement
- Metrics should be added in the observability phase to track traversal performance in production
