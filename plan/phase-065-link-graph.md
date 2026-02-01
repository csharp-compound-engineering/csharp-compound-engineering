# Phase 065: Link Graph Building

> **Status**: PLANNED
> **Category**: Document Processing
> **Estimated Effort**: M
> **Prerequisites**: Phase 064 (Link Extraction Service), Phase 016 (QuikGraph Integration)

---

## Spec References

- [mcp-server.md - Link Resolution](../spec/mcp-server.md#link-resolution) (lines 251-290)
- [mcp-server/file-watcher.md - Event Processing](../spec/mcp-server/file-watcher.md#event-processing)
- [research/dotnet-graph-libraries.md](../research/dotnet-graph-libraries.md) - QuikGraph evaluation

---

## Objectives

1. Build the in-memory link graph during document processing operations
2. Integrate graph updates with file watcher events (create, modify, delete)
3. Implement efficient incremental graph updates for single document changes
4. Add full graph rebuild capability for startup reconciliation
5. Handle link target validation (existing vs non-existing documents)
6. Implement circular reference warning system during graph updates

---

## Acceptance Criteria

### Graph Construction on Document Index

- [ ] When a document is indexed, parse its links and add to graph
- [ ] Document vertex is added before edges are created
- [ ] Outgoing edges are created for all valid internal links
- [ ] Invalid link targets (external URLs, malformed paths) are skipped
- [ ] Links to non-existent documents create placeholder vertices

### Graph Updates on Document Modification

- [ ] Modified document triggers link re-parsing
- [ ] Existing edges from the document are removed before re-adding
- [ ] `UpdateDocumentLinks` is called atomically (remove all + add new)
- [ ] Graph state remains consistent during update operation
- [ ] Cycle detection runs after each update and logs warnings

### Graph Cleanup on Document Deletion

- [ ] Deleted document vertex is removed from graph
- [ ] All incoming edges (from other documents) are removed
- [ ] All outgoing edges (to other documents) are removed
- [ ] Orphaned placeholder vertices are cleaned up

### Full Graph Rebuild on Startup

- [ ] `RebuildFromDocuments` clears and reconstructs entire graph
- [ ] All documents from database are loaded with their links
- [ ] Graph is built in dependency order where possible
- [ ] Rebuild completes within acceptable time for 1000+ documents
- [ ] Cycle detection runs after rebuild and logs all cycles found

### Integration with File Watcher Service

- [ ] `IDocumentLinkGraph` is injected into file watcher service
- [ ] Graph updates occur after successful database operations
- [ ] Graph operations do not block file watcher event processing
- [ ] Errors in graph operations are logged but do not fail document operations

---

## Implementation Notes

### 1. Graph Builder Service Interface

Create a service that coordinates link extraction and graph updates:

```csharp
// src/CompoundDocs.McpServer/Services/ILinkGraphBuilder.cs
namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Coordinates link extraction and graph updates during document operations.
/// </summary>
public interface ILinkGraphBuilder
{
    /// <summary>
    /// Updates the graph for a newly indexed or modified document.
    /// Extracts links from content and updates graph edges.
    /// </summary>
    /// <param name="relativePath">Document path (graph vertex identifier).</param>
    /// <param name="markdownContent">Document content for link extraction.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing extracted links and any cycles detected.</returns>
    Task<LinkGraphUpdateResult> UpdateDocumentLinksAsync(
        string relativePath,
        string markdownContent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a document and its links from the graph.
    /// </summary>
    /// <param name="relativePath">Document path to remove.</param>
    void RemoveDocument(string relativePath);

    /// <summary>
    /// Rebuilds the entire graph from a collection of documents.
    /// Used during startup reconciliation.
    /// </summary>
    /// <param name="documents">All documents with their content.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing total edges and cycles found.</returns>
    Task<LinkGraphRebuildResult> RebuildGraphAsync(
        IEnumerable<DocumentWithContent> documents,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks the current graph for cycles without modifying it.
    /// </summary>
    /// <returns>List of cycle paths found in the graph.</returns>
    IReadOnlyList<IReadOnlyList<string>> DetectCycles();
}

/// <summary>
/// Document with content for graph rebuilding.
/// </summary>
public record DocumentWithContent(string RelativePath, string Content);

/// <summary>
/// Result of a single document link graph update.
/// </summary>
public record LinkGraphUpdateResult(
    IReadOnlyList<string> ExtractedLinks,
    IReadOnlyList<string> InvalidLinks,
    bool CreatesNewCycle,
    IReadOnlyList<string>? CyclePath);

/// <summary>
/// Result of full graph rebuild operation.
/// </summary>
public record LinkGraphRebuildResult(
    int TotalVertices,
    int TotalEdges,
    int CyclesFound,
    TimeSpan Duration);
```

### 2. Link Graph Builder Implementation

```csharp
// src/CompoundDocs.McpServer/Services/LinkGraphBuilder.cs
using Microsoft.Extensions.Logging;

namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Builds and maintains the document link graph during document operations.
/// </summary>
public class LinkGraphBuilder : ILinkGraphBuilder
{
    private readonly IDocumentLinkGraph _linkGraph;
    private readonly ILinkExtractor _linkExtractor;
    private readonly ILogger<LinkGraphBuilder> _logger;

    public LinkGraphBuilder(
        IDocumentLinkGraph linkGraph,
        ILinkExtractor linkExtractor,
        ILogger<LinkGraphBuilder> logger)
    {
        _linkGraph = linkGraph ?? throw new ArgumentNullException(nameof(linkGraph));
        _linkExtractor = linkExtractor ?? throw new ArgumentNullException(nameof(linkExtractor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<LinkGraphUpdateResult> UpdateDocumentLinksAsync(
        string relativePath,
        string markdownContent,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(relativePath);
        ArgumentNullException.ThrowIfNull(markdownContent);

        // Extract links from markdown content
        var extractionResult = _linkExtractor.ExtractLinks(markdownContent, relativePath);

        var validLinks = new List<string>();
        var invalidLinks = new List<string>();

        foreach (var link in extractionResult.Links)
        {
            if (IsValidInternalLink(link))
            {
                validLinks.Add(link.ResolvedPath);
            }
            else
            {
                invalidLinks.Add(link.OriginalTarget);
            }
        }

        _logger.LogDebug(
            "Extracted {ValidCount} valid and {InvalidCount} invalid links from {Path}",
            validLinks.Count,
            invalidLinks.Count,
            relativePath);

        // Check if any new links would create cycles before updating
        bool wouldCreateCycle = false;
        List<string>? cyclePath = null;

        foreach (var targetPath in validLinks)
        {
            if (_linkGraph.WouldCreateCycle(relativePath, targetPath))
            {
                wouldCreateCycle = true;
                cyclePath = new List<string> { relativePath, targetPath };
                _logger.LogWarning(
                    "Adding link from {Source} to {Target} creates a circular reference",
                    relativePath,
                    targetPath);
                break;
            }
        }

        // Update graph regardless of cycles (cycles are allowed, just warned)
        _linkGraph.UpdateDocumentLinks(relativePath, validLinks);

        return Task.FromResult(new LinkGraphUpdateResult(
            validLinks,
            invalidLinks,
            wouldCreateCycle,
            cyclePath));
    }

    public void RemoveDocument(string relativePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(relativePath);

        _logger.LogDebug("Removing document {Path} from link graph", relativePath);
        _linkGraph.RemoveDocument(relativePath);
    }

    public Task<LinkGraphRebuildResult> RebuildGraphAsync(
        IEnumerable<DocumentWithContent> documents,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documents);

        var startTime = DateTime.UtcNow;
        var documentList = documents.ToList();

        _logger.LogInformation(
            "Rebuilding link graph from {Count} documents",
            documentList.Count);

        // Extract links from all documents
        var linkInfos = new List<DocumentLinkInfo>();

        foreach (var doc in documentList)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var extractionResult = _linkExtractor.ExtractLinks(doc.Content, doc.RelativePath);
            var validLinks = extractionResult.Links
                .Where(IsValidInternalLink)
                .Select(l => l.ResolvedPath)
                .ToList();

            linkInfos.Add(new DocumentLinkInfo(doc.RelativePath, validLinks));
        }

        // Rebuild the graph atomically
        _linkGraph.RebuildFromDocuments(linkInfos);

        // Detect cycles in the rebuilt graph
        var cycles = _linkGraph.DetectCycles();

        if (cycles.Count > 0)
        {
            _logger.LogWarning(
                "Link graph contains {CycleCount} circular reference groups",
                cycles.Count);

            foreach (var cycle in cycles)
            {
                _logger.LogWarning(
                    "Circular reference detected: {Cycle}",
                    string.Join(" -> ", cycle));
            }
        }

        var duration = DateTime.UtcNow - startTime;
        var totalEdges = linkInfos.Sum(li => li.LinkedPaths.Count);

        _logger.LogInformation(
            "Link graph rebuilt: {Vertices} vertices, {Edges} edges, {Cycles} cycles in {Duration:F2}s",
            documentList.Count,
            totalEdges,
            cycles.Count,
            duration.TotalSeconds);

        return Task.FromResult(new LinkGraphRebuildResult(
            documentList.Count,
            totalEdges,
            cycles.Count,
            duration));
    }

    public IReadOnlyList<IReadOnlyList<string>> DetectCycles()
    {
        return _linkGraph.DetectCycles();
    }

    /// <summary>
    /// Determines if a link is a valid internal document link.
    /// </summary>
    private static bool IsValidInternalLink(ExtractedLink link)
    {
        // Skip external URLs
        if (link.LinkType == LinkType.External)
            return false;

        // Skip anchor-only links
        if (link.LinkType == LinkType.Anchor)
            return false;

        // Skip links that couldn't be resolved
        if (string.IsNullOrEmpty(link.ResolvedPath))
            return false;

        // Only include markdown files
        return link.ResolvedPath.EndsWith(".md", StringComparison.OrdinalIgnoreCase);
    }
}
```

### 3. Integration with Document Indexing Pipeline

The `LinkGraphBuilder` should be called from the document indexing service:

```csharp
// In DocumentIndexingService (partial - integration point)
public async Task IndexDocumentAsync(
    string relativePath,
    string content,
    TenantContext tenant,
    CancellationToken ct)
{
    // ... existing validation and embedding generation ...

    // After successful database upsert, update link graph
    try
    {
        var linkResult = await _linkGraphBuilder.UpdateDocumentLinksAsync(
            relativePath,
            content,
            ct);

        if (linkResult.CreatesNewCycle)
        {
            _logger.LogWarning(
                "Document {Path} creates circular reference: {Cycle}",
                relativePath,
                string.Join(" -> ", linkResult.CyclePath!));
        }
    }
    catch (Exception ex)
    {
        // Graph errors should not fail the indexing operation
        _logger.LogError(ex,
            "Failed to update link graph for {Path}, document indexed successfully",
            relativePath);
    }
}
```

### 4. Integration with File Watcher Delete Handler

```csharp
// In FileWatcherService delete handler
private async Task HandleFileDeletedAsync(string relativePath, CancellationToken ct)
{
    // ... existing database delete operation ...

    // After successful database delete, remove from link graph
    try
    {
        _linkGraphBuilder.RemoveDocument(relativePath);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex,
            "Failed to remove {Path} from link graph",
            relativePath);
    }
}
```

### 5. Integration with Reconciliation Service

```csharp
// In ReconciliationService (startup)
public async Task ReconcileAsync(CancellationToken ct)
{
    // ... existing reconciliation logic ...

    // After reconciliation, rebuild the link graph
    var allDocuments = await _repository.GetAllDocumentsAsync(tenant, ct);
    var documentsWithContent = allDocuments
        .Select(d => new DocumentWithContent(d.RelativePath, d.Content))
        .ToList();

    var rebuildResult = await _linkGraphBuilder.RebuildGraphAsync(documentsWithContent, ct);

    _logger.LogInformation(
        "Reconciliation complete. Link graph: {Vertices} docs, {Edges} links, {Cycles} cycles",
        rebuildResult.TotalVertices,
        rebuildResult.TotalEdges,
        rebuildResult.CyclesFound);
}
```

### 6. Graph Persistence Strategy

The link graph is **not persisted** to the database. It is rebuilt on each server startup during reconciliation.

**Rationale**:
- Graph rebuild is fast (<1 second for typical document counts)
- Eliminates database schema complexity for graph edges
- Ensures graph always reflects current document state
- Avoids stale graph data after manual database modifications

**Trade-offs**:
- Slightly longer startup time for large repositories
- Link extraction runs for all documents on startup

### 7. Performance Considerations

| Document Count | Expected Rebuild Time |
|----------------|----------------------|
| < 100 | < 100ms |
| 100-500 | 100-500ms |
| 500-1000 | 500ms-1s |
| > 1000 | 1-3s |

**Optimization Strategies**:
- Link extraction can be parallelized across documents
- Graph vertex addition can be batched
- Use connection pooling for database queries during rebuild

### 8. Error Handling

| Error Type | Handling |
|------------|----------|
| Link extraction failure | Log warning, skip document's links |
| Graph operation failure | Log error, continue processing |
| Cycle detected | Log warning, allow the link anyway |
| Malformed link path | Log debug, skip the link |

---

## Dependencies

### Depends On

- **Phase 016**: QuikGraph Integration - `IDocumentLinkGraph` interface and implementation
- **Phase 064**: Link Extraction Service - `ILinkExtractor` for parsing markdown links

### Blocks

- **Phase 066+**: RAG Query Link Following - Uses graph to expand context
- **Phase 067+**: `semantic_search` tool - May use graph for related documents
- **Phase 070+**: Document relationship visualization (if planned)

---

## Testing Verification

### Unit Tests

```csharp
// tests/CompoundDocs.Tests/Services/LinkGraphBuilderTests.cs
using CompoundDocs.McpServer.Services;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace CompoundDocs.Tests.Services;

public class LinkGraphBuilderTests
{
    private readonly IDocumentLinkGraph _mockGraph;
    private readonly ILinkExtractor _mockExtractor;
    private readonly ILogger<LinkGraphBuilder> _mockLogger;
    private readonly LinkGraphBuilder _sut;

    public LinkGraphBuilderTests()
    {
        _mockGraph = Substitute.For<IDocumentLinkGraph>();
        _mockExtractor = Substitute.For<ILinkExtractor>();
        _mockLogger = Substitute.For<ILogger<LinkGraphBuilder>>();
        _sut = new LinkGraphBuilder(_mockGraph, _mockExtractor, _mockLogger);
    }

    [Fact]
    public async Task UpdateDocumentLinksAsync_ExtractsLinksAndUpdatesGraph()
    {
        // Arrange
        var links = new List<ExtractedLink>
        {
            new("other.md", "other.md", LinkType.Relative),
            new("docs/readme.md", "docs/readme.md", LinkType.Relative)
        };
        _mockExtractor.ExtractLinks(Arg.Any<string>(), Arg.Any<string>())
            .Returns(new LinkExtractionResult(links));

        // Act
        var result = await _sut.UpdateDocumentLinksAsync("test.md", "# Test\n[link](other.md)");

        // Assert
        Assert.Equal(2, result.ExtractedLinks.Count);
        _mockGraph.Received(1).UpdateDocumentLinks(
            "test.md",
            Arg.Is<IEnumerable<string>>(x => x.Count() == 2));
    }

    [Fact]
    public async Task UpdateDocumentLinksAsync_FiltersExternalLinks()
    {
        // Arrange
        var links = new List<ExtractedLink>
        {
            new("other.md", "other.md", LinkType.Relative),
            new("https://example.com", null, LinkType.External)
        };
        _mockExtractor.ExtractLinks(Arg.Any<string>(), Arg.Any<string>())
            .Returns(new LinkExtractionResult(links));

        // Act
        var result = await _sut.UpdateDocumentLinksAsync("test.md", "content");

        // Assert
        Assert.Single(result.ExtractedLinks);
        Assert.Single(result.InvalidLinks);
    }

    [Fact]
    public async Task UpdateDocumentLinksAsync_DetectsNewCycles()
    {
        // Arrange
        var links = new List<ExtractedLink>
        {
            new("other.md", "other.md", LinkType.Relative)
        };
        _mockExtractor.ExtractLinks(Arg.Any<string>(), Arg.Any<string>())
            .Returns(new LinkExtractionResult(links));
        _mockGraph.WouldCreateCycle("test.md", "other.md").Returns(true);

        // Act
        var result = await _sut.UpdateDocumentLinksAsync("test.md", "content");

        // Assert
        Assert.True(result.CreatesNewCycle);
        Assert.NotNull(result.CyclePath);
    }

    [Fact]
    public void RemoveDocument_CallsGraphRemove()
    {
        // Act
        _sut.RemoveDocument("test.md");

        // Assert
        _mockGraph.Received(1).RemoveDocument("test.md");
    }

    [Fact]
    public async Task RebuildGraphAsync_ProcessesAllDocuments()
    {
        // Arrange
        var docs = new List<DocumentWithContent>
        {
            new("a.md", "# A\n[link](b.md)"),
            new("b.md", "# B\n[link](c.md)"),
            new("c.md", "# C")
        };

        _mockExtractor.ExtractLinks(Arg.Any<string>(), Arg.Any<string>())
            .Returns(x => new LinkExtractionResult(new List<ExtractedLink>()));
        _mockGraph.DetectCycles().Returns(new List<IReadOnlyList<string>>());

        // Act
        var result = await _sut.RebuildGraphAsync(docs);

        // Assert
        Assert.Equal(3, result.TotalVertices);
        _mockGraph.Received(1).RebuildFromDocuments(
            Arg.Is<IEnumerable<DocumentLinkInfo>>(x => x.Count() == 3));
    }

    [Fact]
    public async Task RebuildGraphAsync_ReportsCyclesFound()
    {
        // Arrange
        var docs = new List<DocumentWithContent> { new("a.md", "content") };
        _mockExtractor.ExtractLinks(Arg.Any<string>(), Arg.Any<string>())
            .Returns(new LinkExtractionResult(new List<ExtractedLink>()));
        _mockGraph.DetectCycles().Returns(new List<IReadOnlyList<string>>
        {
            new List<string> { "a.md", "b.md", "a.md" }
        });

        // Act
        var result = await _sut.RebuildGraphAsync(docs);

        // Assert
        Assert.Equal(1, result.CyclesFound);
    }
}
```

### Integration Tests

```csharp
// tests/CompoundDocs.Tests/Integration/LinkGraphBuilderIntegrationTests.cs
namespace CompoundDocs.Tests.Integration;

public class LinkGraphBuilderIntegrationTests
{
    [Fact]
    public async Task FullWorkflow_IndexModifyDelete_MaintainsGraphConsistency()
    {
        // Arrange
        var graph = new DocumentLinkGraph();
        var extractor = new MarkdownLinkExtractor();
        var logger = NullLogger<LinkGraphBuilder>.Instance;
        var builder = new LinkGraphBuilder(graph, extractor, logger);

        // Act - Index document A with link to B
        await builder.UpdateDocumentLinksAsync("a.md", "# A\n[link](b.md)");

        // Assert - Graph has edge A -> B
        var linkedFromA = graph.GetLinkedDocuments("a.md", 1, 10);
        Assert.Contains("b.md", linkedFromA);

        // Act - Index document B with link to C
        await builder.UpdateDocumentLinksAsync("b.md", "# B\n[link](c.md)");

        // Assert - Graph has chain A -> B -> C
        var depth2FromA = graph.GetLinkedDocuments("a.md", 2, 10);
        Assert.Contains("b.md", depth2FromA);
        Assert.Contains("c.md", depth2FromA);

        // Act - Modify A to link to C directly
        await builder.UpdateDocumentLinksAsync("a.md", "# A\n[link](c.md)");

        // Assert - A now links to C, not B
        var newLinkedFromA = graph.GetLinkedDocuments("a.md", 1, 10);
        Assert.Contains("c.md", newLinkedFromA);
        Assert.DoesNotContain("b.md", newLinkedFromA);

        // Act - Delete B
        builder.RemoveDocument("b.md");

        // Assert - B is gone but A -> C remains
        var stillLinkedFromA = graph.GetLinkedDocuments("a.md", 1, 10);
        Assert.Contains("c.md", stillLinkedFromA);
    }

    [Fact]
    public async Task CircularReference_IsDetectedAndWarned()
    {
        // Arrange
        var graph = new DocumentLinkGraph();
        var extractor = new MarkdownLinkExtractor();
        var logger = NullLogger<LinkGraphBuilder>.Instance;
        var builder = new LinkGraphBuilder(graph, extractor, logger);

        // Act - Create cycle: A -> B -> C -> A
        await builder.UpdateDocumentLinksAsync("a.md", "[link](b.md)");
        await builder.UpdateDocumentLinksAsync("b.md", "[link](c.md)");
        var result = await builder.UpdateDocumentLinksAsync("c.md", "[link](a.md)");

        // Assert
        Assert.True(result.CreatesNewCycle);

        var cycles = builder.DetectCycles();
        Assert.Single(cycles);
        Assert.Equal(3, cycles[0].Count);
    }
}
```

---

## Files Created/Modified

| File | Action | Description |
|------|--------|-------------|
| `src/CompoundDocs.McpServer/Services/ILinkGraphBuilder.cs` | Create | Service interface |
| `src/CompoundDocs.McpServer/Services/LinkGraphBuilder.cs` | Create | Implementation |
| `src/CompoundDocs.McpServer/Services/LinkGraphBuilderResults.cs` | Create | Result record types |
| `src/CompoundDocs.McpServer/DependencyInjection/ServiceCollectionExtensions.cs` | Modify | Add DI registration |
| `tests/CompoundDocs.Tests/Services/LinkGraphBuilderTests.cs` | Create | Unit tests |
| `tests/CompoundDocs.Tests/Integration/LinkGraphBuilderIntegrationTests.cs` | Create | Integration tests |

---

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| Graph rebuild too slow | Parallelize link extraction across documents |
| Graph inconsistency on crash | Rebuild from database on next startup |
| Circular references cause infinite loops | BFS with visited set prevents revisiting |
| Large graphs consume memory | Vertices are just strings; edges are lightweight |
| Link extraction errors | Isolate failures to single document, continue others |

---

## Notes

- The link graph is ephemeral - rebuilt on each server activation
- Circular references are allowed but logged as warnings
- Graph operations are intentionally non-blocking for document indexing
- The `IDocumentLinkGraph` from Phase 016 provides thread-safe operations
- Link extraction depends on the `ILinkExtractor` from Phase 064
- RAG queries (Phase 066+) will use `GetLinkedDocuments` to expand context
