# Phase 066: Circular Reference Detection

> **Status**: NOT_STARTED
> **Effort Estimate**: 3-4 hours
> **Category**: Document Processing
> **Prerequisites**: Phase 065 (Link Following), Phase 016 (QuikGraph Integration)

---

## Spec References

This phase implements circular reference detection defined in:

- **spec/mcp-server.md** - [Circular Reference Handling](../spec/mcp-server.md#circular-reference-handling) (lines 285-290)
- **spec/mcp-server.md** - [Link Resolution](../spec/mcp-server.md#link-resolution) (lines 251-284)
- **research/dotnet-graph-libraries.md** - QuikGraph cycle detection algorithms

---

## Objectives

1. Implement cycle detection algorithm in the document link graph
2. Add maximum depth limiting for link traversal to prevent infinite loops
3. Create warning logging system for circular references
4. Ensure graceful handling that prevents infinite recursion
5. Build circular reference reporting for diagnostics

---

## Acceptance Criteria

### Cycle Detection Algorithm
- [ ] `DetectCyclesInGraph()` identifies all strongly connected components (SCCs) with more than one vertex
- [ ] `GetCyclePathsContaining(relativePath)` returns specific cycle paths involving a document
- [ ] Detection uses Tarjan's or Kosaraju's algorithm via QuikGraph's `StronglyConnectedComponents()`
- [ ] Cycle detection runs during document indexing to provide early warnings
- [ ] Algorithm handles disconnected subgraphs correctly

### Maximum Depth Limiting
- [ ] `MaxTraversalDepth` configuration option (default: 10) enforced during all traversals
- [ ] Depth counter passed through recursive/iterative traversal methods
- [ ] Traversal terminates gracefully when max depth reached (no exception)
- [ ] Depth limit logged at DEBUG level when triggered
- [ ] Separate from `link_resolution.max_depth` which is for RAG context (configurable 2)

### Warning Logging for Circular References
- [ ] Circular reference detection logs at WARNING level
- [ ] Log message includes full cycle path: `"Circular reference detected: A.md -> B.md -> C.md -> A.md"`
- [ ] Logging occurs once per unique cycle (deduplicated)
- [ ] Cycle detection happens during document sync, not during every query
- [ ] Option to escalate to ERROR level via configuration

### Graceful Handling (No Infinite Loops)
- [ ] Visited node tracking prevents re-processing during traversal
- [ ] `HashSet<string>` tracks visited documents in every traversal method
- [ ] Link following skips already-visited documents silently
- [ ] No exceptions thrown for circular references (warning only)
- [ ] Unit tests verify termination within reasonable time for cyclic graphs

### Circular Reference Reporting
- [ ] `CircularReferenceReport` record/class captures cycle information
- [ ] Report includes: cycle paths, documents involved, detection timestamp
- [ ] `IDocumentLinkGraph.GetCircularReferenceReport()` returns current cycle state
- [ ] Report available via diagnostic endpoint or logging on demand
- [ ] Report cleared and rebuilt on full reconciliation

---

## Implementation Notes

### Cycle Detection Service

```csharp
/// <summary>
/// Detects and reports circular references in the document link graph.
/// </summary>
public interface ICircularReferenceDetector
{
    /// <summary>
    /// Detects all cycles in the current document graph.
    /// </summary>
    CircularReferenceReport DetectAllCycles();

    /// <summary>
    /// Checks if adding a link would create a cycle.
    /// </summary>
    bool WouldCreateCycle(string sourceDocument, string targetDocument);

    /// <summary>
    /// Gets all cycles that include the specified document.
    /// </summary>
    IReadOnlyList<CyclePath> GetCyclesContaining(string relativePath);
}

/// <summary>
/// Represents a detected circular reference path.
/// </summary>
public record CyclePath(IReadOnlyList<string> Documents)
{
    public override string ToString() =>
        string.Join(" -> ", Documents) + " -> " + Documents[0];
}

/// <summary>
/// Complete report of circular references in the document graph.
/// </summary>
public record CircularReferenceReport(
    IReadOnlyList<CyclePath> Cycles,
    DateTimeOffset DetectedAt,
    int TotalDocumentsInCycles)
{
    public bool HasCycles => Cycles.Count > 0;
}
```

### Implementation Using QuikGraph

```csharp
using QuikGraph;
using QuikGraph.Algorithms;
using Microsoft.Extensions.Logging;

public class CircularReferenceDetector : ICircularReferenceDetector
{
    private readonly IDocumentLinkGraph _linkGraph;
    private readonly ILogger<CircularReferenceDetector> _logger;
    private CircularReferenceReport? _cachedReport;

    public CircularReferenceDetector(
        IDocumentLinkGraph linkGraph,
        ILogger<CircularReferenceDetector> logger)
    {
        _linkGraph = linkGraph;
        _logger = logger;
    }

    public CircularReferenceReport DetectAllCycles()
    {
        var cycles = _linkGraph.DetectCycles();
        var cyclePaths = cycles
            .Select(c => new CyclePath(c))
            .ToList();

        // Log warnings for each detected cycle
        foreach (var cycle in cyclePaths)
        {
            _logger.LogWarning(
                "Circular reference detected: {CyclePath}",
                cycle.ToString());
        }

        var totalDocsInCycles = cyclePaths
            .SelectMany(c => c.Documents)
            .Distinct()
            .Count();

        _cachedReport = new CircularReferenceReport(
            cyclePaths,
            DateTimeOffset.UtcNow,
            totalDocsInCycles);

        return _cachedReport;
    }

    public bool WouldCreateCycle(string sourceDocument, string targetDocument)
    {
        return _linkGraph.WouldCreateCycle(sourceDocument, targetDocument);
    }

    public IReadOnlyList<CyclePath> GetCyclesContaining(string relativePath)
    {
        var report = _cachedReport ?? DetectAllCycles();
        return report.Cycles
            .Where(c => c.Documents.Contains(relativePath))
            .ToList();
    }
}
```

### Safe Link Traversal with Depth Limiting

```csharp
public class SafeLinkTraverser
{
    private readonly IDocumentLinkGraph _linkGraph;
    private readonly ILogger<SafeLinkTraverser> _logger;
    private readonly int _maxTraversalDepth;

    public SafeLinkTraverser(
        IDocumentLinkGraph linkGraph,
        IOptions<LinkResolutionOptions> options,
        ILogger<SafeLinkTraverser> logger)
    {
        _linkGraph = linkGraph;
        _logger = logger;
        _maxTraversalDepth = options.Value.MaxTraversalDepth;
    }

    /// <summary>
    /// Traverses linked documents with cycle and depth protection.
    /// </summary>
    public IReadOnlyList<string> TraverseLinks(
        string startDocument,
        int maxDepth,
        int maxDocuments)
    {
        // Enforce maximum traversal depth
        var effectiveDepth = Math.Min(maxDepth, _maxTraversalDepth);
        if (maxDepth > _maxTraversalDepth)
        {
            _logger.LogDebug(
                "Requested depth {RequestedDepth} exceeds max traversal depth {MaxDepth}, limiting to {MaxDepth}",
                maxDepth, _maxTraversalDepth, _maxTraversalDepth);
        }

        var result = new List<string>();
        var visited = new HashSet<string> { startDocument };
        var currentLevel = new Queue<string>();
        currentLevel.Enqueue(startDocument);

        for (int depth = 0; depth < effectiveDepth && result.Count < maxDocuments; depth++)
        {
            var nextLevel = new Queue<string>();

            while (currentLevel.Count > 0 && result.Count < maxDocuments)
            {
                var current = currentLevel.Dequeue();
                var linkedDocs = _linkGraph.GetLinkedDocuments(current, 1, int.MaxValue);

                foreach (var linkedDoc in linkedDocs)
                {
                    // Skip already visited (handles cycles)
                    if (!visited.Add(linkedDoc))
                    {
                        continue;
                    }

                    result.Add(linkedDoc);
                    nextLevel.Enqueue(linkedDoc);

                    if (result.Count >= maxDocuments)
                    {
                        break;
                    }
                }
            }

            currentLevel = nextLevel;
        }

        return result;
    }
}
```

### Configuration Options

```csharp
public class LinkResolutionOptions
{
    /// <summary>
    /// Maximum depth for RAG context link following. Default: 2
    /// </summary>
    public int MaxDepth { get; set; } = 2;

    /// <summary>
    /// Maximum linked documents to include in RAG context. Default: 5
    /// </summary>
    public int MaxLinkedDocs { get; set; } = 5;

    /// <summary>
    /// Hard maximum traversal depth to prevent runaway recursion. Default: 10
    /// </summary>
    public int MaxTraversalDepth { get; set; } = 10;

    /// <summary>
    /// Log level for circular reference warnings. Default: Warning
    /// </summary>
    public LogLevel CircularReferenceLogLevel { get; set; } = LogLevel.Warning;
}
```

### Integration with Document Sync

```csharp
public class DocumentSyncHandler
{
    private readonly ICircularReferenceDetector _cycleDetector;
    private readonly ILogger<DocumentSyncHandler> _logger;

    public async Task OnDocumentIndexed(string relativePath, IReadOnlyList<string> links)
    {
        // Check for potential cycles before updating
        foreach (var link in links)
        {
            if (_cycleDetector.WouldCreateCycle(relativePath, link))
            {
                _logger.LogWarning(
                    "Adding link from {Source} to {Target} creates a circular reference",
                    relativePath, link);
            }
        }

        // Proceed with update (circular refs are allowed, just warned)
        await UpdateDocumentLinksAsync(relativePath, links);
    }

    public async Task OnFullReconciliation()
    {
        // Detect and report all cycles after full sync
        var report = _cycleDetector.DetectAllCycles();

        if (report.HasCycles)
        {
            _logger.LogWarning(
                "Document graph contains {CycleCount} circular reference(s) involving {DocCount} documents",
                report.Cycles.Count,
                report.TotalDocumentsInCycles);
        }
        else
        {
            _logger.LogInformation("Document graph is acyclic (no circular references)");
        }
    }
}
```

### Dependency Injection Registration

```csharp
// In DI configuration
services.AddSingleton<ICircularReferenceDetector, CircularReferenceDetector>();
services.AddSingleton<SafeLinkTraverser>();
services.Configure<LinkResolutionOptions>(configuration.GetSection("LinkResolution"));
```

---

## Dependencies

### Depends On
- Phase 016: QuikGraph Integration (provides `IDocumentLinkGraph` and cycle detection primitives)
- Phase 065: Link Following (link traversal infrastructure)
- Phase 018: Logging Infrastructure (logging framework)

### Blocks
- RAG query implementation (needs safe traversal for context building)
- Document diagnostics (reports on graph health)

---

## Verification Steps

After completing this phase, verify:

1. **Cycle detection works**: Unit tests detect simple and complex cycles
2. **Depth limiting enforced**: Traversal terminates at configured max depth
3. **No infinite loops**: Cyclic graph tests complete within time limits
4. **Warnings logged**: Circular references produce log warnings
5. **Report accurate**: `CircularReferenceReport` correctly identifies all cycles

---

## Unit Test Scenarios

```csharp
[Fact]
public void DetectAllCycles_FindsSimpleCycle()
{
    var graph = new DocumentLinkGraph();
    graph.UpdateDocumentLinks("A.md", new[] { "B.md" });
    graph.UpdateDocumentLinks("B.md", new[] { "C.md" });
    graph.UpdateDocumentLinks("C.md", new[] { "A.md" });

    var detector = new CircularReferenceDetector(graph, NullLogger<CircularReferenceDetector>.Instance);
    var report = detector.DetectAllCycles();

    Assert.True(report.HasCycles);
    Assert.Single(report.Cycles);
    Assert.Equal(3, report.TotalDocumentsInCycles);
}

[Fact]
public void DetectAllCycles_FindsMultipleCycles()
{
    var graph = new DocumentLinkGraph();
    // Cycle 1: A -> B -> A
    graph.UpdateDocumentLinks("A.md", new[] { "B.md" });
    graph.UpdateDocumentLinks("B.md", new[] { "A.md" });
    // Cycle 2: C -> D -> E -> C
    graph.UpdateDocumentLinks("C.md", new[] { "D.md" });
    graph.UpdateDocumentLinks("D.md", new[] { "E.md" });
    graph.UpdateDocumentLinks("E.md", new[] { "C.md" });

    var detector = new CircularReferenceDetector(graph, NullLogger<CircularReferenceDetector>.Instance);
    var report = detector.DetectAllCycles();

    Assert.True(report.HasCycles);
    Assert.Equal(2, report.Cycles.Count);
}

[Fact]
public void DetectAllCycles_ReturnsEmptyForAcyclicGraph()
{
    var graph = new DocumentLinkGraph();
    graph.UpdateDocumentLinks("A.md", new[] { "B.md" });
    graph.UpdateDocumentLinks("B.md", new[] { "C.md" });
    graph.UpdateDocumentLinks("A.md", new[] { "C.md" });

    var detector = new CircularReferenceDetector(graph, NullLogger<CircularReferenceDetector>.Instance);
    var report = detector.DetectAllCycles();

    Assert.False(report.HasCycles);
    Assert.Empty(report.Cycles);
}

[Fact]
public void WouldCreateCycle_ReturnsTrueWhenCycleWouldForm()
{
    var graph = new DocumentLinkGraph();
    graph.UpdateDocumentLinks("A.md", new[] { "B.md" });
    graph.UpdateDocumentLinks("B.md", new[] { "C.md" });

    var detector = new CircularReferenceDetector(graph, NullLogger<CircularReferenceDetector>.Instance);

    // C -> A would create A -> B -> C -> A cycle
    Assert.True(detector.WouldCreateCycle("C.md", "A.md"));
    // D -> A would not (D is not reachable from A)
    Assert.False(detector.WouldCreateCycle("D.md", "A.md"));
}

[Fact]
public void TraverseLinks_RespectsMaxDepth()
{
    var graph = new DocumentLinkGraph();
    graph.UpdateDocumentLinks("A.md", new[] { "B.md" });
    graph.UpdateDocumentLinks("B.md", new[] { "C.md" });
    graph.UpdateDocumentLinks("C.md", new[] { "D.md" });
    graph.UpdateDocumentLinks("D.md", new[] { "E.md" });

    var options = Options.Create(new LinkResolutionOptions { MaxTraversalDepth = 2 });
    var traverser = new SafeLinkTraverser(graph, options, NullLogger<SafeLinkTraverser>.Instance);

    // Request depth 10, but max is 2
    var result = traverser.TraverseLinks("A.md", 10, 100);

    Assert.Equal(2, result.Count); // B and C only
    Assert.Contains("B.md", result);
    Assert.Contains("C.md", result);
    Assert.DoesNotContain("D.md", result);
}

[Fact]
public void TraverseLinks_HandlesCircularReferencesGracefully()
{
    var graph = new DocumentLinkGraph();
    graph.UpdateDocumentLinks("A.md", new[] { "B.md" });
    graph.UpdateDocumentLinks("B.md", new[] { "C.md" });
    graph.UpdateDocumentLinks("C.md", new[] { "A.md" }); // Cycle back to A

    var options = Options.Create(new LinkResolutionOptions { MaxTraversalDepth = 10 });
    var traverser = new SafeLinkTraverser(graph, options, NullLogger<SafeLinkTraverser>.Instance);

    var stopwatch = Stopwatch.StartNew();
    var result = traverser.TraverseLinks("A.md", 5, 100);
    stopwatch.Stop();

    // Should complete quickly (not infinite loop)
    Assert.True(stopwatch.ElapsedMilliseconds < 1000);
    // Should find B and C (A is start, so not in result)
    Assert.Equal(2, result.Count);
}

[Fact]
public void GetCyclesContaining_ReturnsOnlyRelevantCycles()
{
    var graph = new DocumentLinkGraph();
    // Cycle 1: A -> B -> A
    graph.UpdateDocumentLinks("A.md", new[] { "B.md" });
    graph.UpdateDocumentLinks("B.md", new[] { "A.md" });
    // Cycle 2: C -> D -> C (separate)
    graph.UpdateDocumentLinks("C.md", new[] { "D.md" });
    graph.UpdateDocumentLinks("D.md", new[] { "C.md" });

    var detector = new CircularReferenceDetector(graph, NullLogger<CircularReferenceDetector>.Instance);

    var cyclesWithA = detector.GetCyclesContaining("A.md");
    var cyclesWithC = detector.GetCyclesContaining("C.md");
    var cyclesWithE = detector.GetCyclesContaining("E.md");

    Assert.Single(cyclesWithA);
    Assert.Single(cyclesWithC);
    Assert.Empty(cyclesWithE);
}

[Fact]
public void CyclePath_ToString_FormatsCorrectly()
{
    var cycle = new CyclePath(new[] { "A.md", "B.md", "C.md" });

    var result = cycle.ToString();

    Assert.Equal("A.md -> B.md -> C.md -> A.md", result);
}
```

---

## Performance Considerations

- **Cycle detection** uses Tarjan's algorithm which is O(V + E) where V is vertices (documents) and E is edges (links)
- **Visited tracking** adds O(1) lookup overhead per node but prevents exponential traversal
- **Cache cycle report** to avoid re-detection on every query; invalidate on document changes
- **Large graphs** (1000+ documents): cycle detection should complete in < 100ms

---

## Notes

- Circular references are allowed in the document graph; they are warned but not prevented
- The RAG system handles cycles by tracking visited documents during context building
- Self-links (A -> A) are treated as trivial cycles and logged at DEBUG level
- Consider adding metrics for cycle count to monitor documentation health over time
