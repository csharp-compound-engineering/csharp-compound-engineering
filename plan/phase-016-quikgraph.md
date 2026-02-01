# Phase 016: QuikGraph Integration for Link Resolution

> **Status**: NOT_STARTED
> **Effort Estimate**: 4-6 hours
> **Category**: Infrastructure Setup
> **Prerequisites**: Phase 001 (Solution & Project Structure)

---

## Spec References

This phase implements the document link graph infrastructure defined in:

- **spec/mcp-server.md** - [Link Resolution](../spec/mcp-server.md#link-resolution) (lines 251-290)
- **research/dotnet-graph-libraries.md** - QuikGraph evaluation and implementation patterns

---

## Objectives

1. Add QuikGraph NuGet package to the solution
2. Create an in-memory document link graph data structure
3. Implement circular reference detection algorithm
4. Build graph update mechanism for document changes
5. Expose graph query methods for RAG link following

---

## Acceptance Criteria

### Package Integration
- [ ] `QuikGraph` package (version 2.5.0+) added to `Directory.Packages.props`
- [ ] Package reference added to `CompoundDocs.McpServer` project

### Document Link Graph Structure
- [ ] `IDocumentLinkGraph` interface created with required operations
- [ ] `DocumentLinkGraph` implementation using QuikGraph's `AdjacencyGraph<string, Edge<string>>`
- [ ] Vertices represent document relative paths (e.g., `"decisions/ADR-001.md"`)
- [ ] Edges represent links between documents (source -> target)
- [ ] Thread-safe operations using appropriate locking or concurrent data structures

### Circular Reference Detection
- [ ] `WouldCreateCycle(source, target)` method implemented using reachability check
- [ ] `DetectCycles()` method returns all circular reference paths
- [ ] Cycle detection logs warnings but does not prevent document indexing
- [ ] Unit tests cover various cycle scenarios (simple, complex, multi-node)

### Graph Update Operations
- [ ] `AddDocument(relativePath)` adds a vertex if not exists
- [ ] `RemoveDocument(relativePath)` removes vertex and all associated edges
- [ ] `UpdateDocumentLinks(relativePath, links)` atomically updates outgoing edges
- [ ] `RebuildFromDocuments(documents)` performs full graph reconstruction

### Graph Query Operations for RAG
- [ ] `GetLinkedDocuments(relativePath, maxDepth)` returns documents up to N hops away
- [ ] `GetReferencingDocuments(relativePath)` returns all documents that link TO this document
- [ ] Respects `link_resolution.max_depth` and `link_resolution.max_linked_docs` configuration
- [ ] Query methods handle missing vertices gracefully (return empty results)

---

## Implementation Notes

### Package Installation

Add to `Directory.Packages.props`:

```xml
<ItemGroup>
  <PackageVersion Include="QuikGraph" Version="2.5.0" />
</ItemGroup>
```

Add to `CompoundDocs.McpServer.csproj`:

```xml
<ItemGroup>
  <PackageReference Include="QuikGraph" />
</ItemGroup>
```

### Interface Design

```csharp
/// <summary>
/// Manages the in-memory graph of document links for cycle detection and RAG traversal.
/// </summary>
public interface IDocumentLinkGraph
{
    /// <summary>
    /// Adds a document vertex to the graph.
    /// </summary>
    void AddDocument(string relativePath);

    /// <summary>
    /// Removes a document and all its edges from the graph.
    /// </summary>
    void RemoveDocument(string relativePath);

    /// <summary>
    /// Updates all outgoing links from a document (replaces existing).
    /// </summary>
    void UpdateDocumentLinks(string relativePath, IEnumerable<string> linkedPaths);

    /// <summary>
    /// Checks if adding a link would create a circular reference.
    /// </summary>
    bool WouldCreateCycle(string sourceDocument, string targetDocument);

    /// <summary>
    /// Gets all documents linked from the given document, up to maxDepth hops.
    /// </summary>
    IReadOnlyList<string> GetLinkedDocuments(string relativePath, int maxDepth, int maxDocuments);

    /// <summary>
    /// Gets all documents that reference (link to) the given document.
    /// </summary>
    IReadOnlyList<string> GetReferencingDocuments(string relativePath);

    /// <summary>
    /// Rebuilds the entire graph from a collection of documents.
    /// </summary>
    void RebuildFromDocuments(IEnumerable<DocumentLinkInfo> documents);

    /// <summary>
    /// Detects all cycles in the graph and returns their paths.
    /// </summary>
    IReadOnlyList<IReadOnlyList<string>> DetectCycles();
}

/// <summary>
/// Document with its parsed outgoing links.
/// </summary>
public record DocumentLinkInfo(string RelativePath, IReadOnlyList<string> LinkedPaths);
```

### Graph Implementation

```csharp
using QuikGraph;
using QuikGraph.Algorithms;
using QuikGraph.Algorithms.Search;

public class DocumentLinkGraph : IDocumentLinkGraph
{
    private readonly BidirectionalGraph<string, Edge<string>> _graph;
    private readonly ReaderWriterLockSlim _lock;

    public DocumentLinkGraph()
    {
        // Use BidirectionalGraph for efficient in-edge queries (GetReferencingDocuments)
        _graph = new BidirectionalGraph<string, Edge<string>>();
        _lock = new ReaderWriterLockSlim();
    }

    public void AddDocument(string relativePath)
    {
        _lock.EnterWriteLock();
        try
        {
            _graph.AddVertex(relativePath);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void RemoveDocument(string relativePath)
    {
        _lock.EnterWriteLock();
        try
        {
            _graph.RemoveVertex(relativePath);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void UpdateDocumentLinks(string relativePath, IEnumerable<string> linkedPaths)
    {
        _lock.EnterWriteLock();
        try
        {
            // Ensure source vertex exists
            _graph.AddVertex(relativePath);

            // Remove all existing outgoing edges
            var existingEdges = _graph.OutEdges(relativePath).ToList();
            foreach (var edge in existingEdges)
            {
                _graph.RemoveEdge(edge);
            }

            // Add new edges
            foreach (var targetPath in linkedPaths)
            {
                _graph.AddVertex(targetPath); // Ensure target exists
                _graph.AddEdge(new Edge<string>(relativePath, targetPath));
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public bool WouldCreateCycle(string sourceDocument, string targetDocument)
    {
        _lock.EnterReadLock();
        try
        {
            // A cycle would be created if target can already reach source
            if (!_graph.ContainsVertex(sourceDocument) || !_graph.ContainsVertex(targetDocument))
                return false;

            return CanReach(targetDocument, sourceDocument);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    private bool CanReach(string from, string to)
    {
        // BFS from 'from' to check if we can reach 'to'
        var visited = new HashSet<string>();
        var queue = new Queue<string>();
        queue.Enqueue(from);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current == to) return true;

            if (visited.Add(current))
            {
                foreach (var edge in _graph.OutEdges(current))
                {
                    queue.Enqueue(edge.Target);
                }
            }
        }
        return false;
    }

    public IReadOnlyList<string> GetLinkedDocuments(string relativePath, int maxDepth, int maxDocuments)
    {
        _lock.EnterReadLock();
        try
        {
            if (!_graph.ContainsVertex(relativePath))
                return Array.Empty<string>();

            var result = new List<string>();
            var visited = new HashSet<string> { relativePath };
            var currentLevel = new Queue<string>();
            currentLevel.Enqueue(relativePath);

            for (int depth = 0; depth < maxDepth && result.Count < maxDocuments; depth++)
            {
                var nextLevel = new Queue<string>();

                while (currentLevel.Count > 0 && result.Count < maxDocuments)
                {
                    var current = currentLevel.Dequeue();

                    foreach (var edge in _graph.OutEdges(current))
                    {
                        if (visited.Add(edge.Target))
                        {
                            result.Add(edge.Target);
                            nextLevel.Enqueue(edge.Target);

                            if (result.Count >= maxDocuments)
                                break;
                        }
                    }
                }

                currentLevel = nextLevel;
            }

            return result;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public IReadOnlyList<string> GetReferencingDocuments(string relativePath)
    {
        _lock.EnterReadLock();
        try
        {
            if (!_graph.ContainsVertex(relativePath))
                return Array.Empty<string>();

            // BidirectionalGraph allows efficient in-edge enumeration
            return _graph.InEdges(relativePath)
                .Select(e => e.Source)
                .ToList();
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public void RebuildFromDocuments(IEnumerable<DocumentLinkInfo> documents)
    {
        _lock.EnterWriteLock();
        try
        {
            _graph.Clear();

            foreach (var doc in documents)
            {
                _graph.AddVertex(doc.RelativePath);
            }

            foreach (var doc in documents)
            {
                foreach (var linkedPath in doc.LinkedPaths)
                {
                    _graph.AddVertex(linkedPath); // Ensure target exists
                    _graph.AddEdge(new Edge<string>(doc.RelativePath, linkedPath));
                }
            }
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public IReadOnlyList<IReadOnlyList<string>> DetectCycles()
    {
        _lock.EnterReadLock();
        try
        {
            var cycles = new List<IReadOnlyList<string>>();

            // Use SCC algorithm to find cyclic components
            var components = new Dictionary<string, int>();
            int componentCount = _graph.StronglyConnectedComponents(components);

            // Group vertices by component
            var componentGroups = components
                .GroupBy(kv => kv.Value)
                .Where(g => g.Count() > 1) // Only multi-vertex components contain cycles
                .ToList();

            foreach (var group in componentGroups)
            {
                cycles.Add(group.Select(kv => kv.Key).ToList());
            }

            return cycles;
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }
}
```

### Dependency Injection Registration

```csharp
// In DI configuration
services.AddSingleton<IDocumentLinkGraph, DocumentLinkGraph>();
```

### Integration with File Watcher

The `DocumentLinkGraph` should be updated when documents change:

```csharp
// On document indexed/updated
var links = markdownParser.ExtractLinks(documentContent);
documentLinkGraph.UpdateDocumentLinks(relativePath, links);

// On document deleted
documentLinkGraph.RemoveDocument(relativePath);

// On reconciliation (startup)
var allDocs = await repository.GetAllDocumentsAsync(ct);
var linkInfos = allDocs.Select(d => new DocumentLinkInfo(
    d.RelativePath,
    markdownParser.ExtractLinks(d.Content)
));
documentLinkGraph.RebuildFromDocuments(linkInfos);
```

### Configuration Integration

Link resolution settings from `config.json`:

```csharp
public class LinkResolutionOptions
{
    public int MaxDepth { get; set; } = 2;
    public int MaxLinkedDocs { get; set; } = 5;
}

// Usage in RAG tool
var linkedDocs = documentLinkGraph.GetLinkedDocuments(
    sourcePath,
    options.MaxDepth,
    options.MaxLinkedDocs);
```

---

## Dependencies

### Depends On
- Phase 001: Solution & Project Structure (solution and project files must exist)

### Blocks
- RAG query implementation (needs link graph for context expansion)
- File watcher integration (updates graph on document changes)
- Document indexing pipeline (populates graph from parsed links)

---

## Verification Steps

After completing this phase, verify:

1. **Package installed**: `dotnet restore` succeeds with QuikGraph
2. **Graph operations work**: Unit tests pass for all CRUD operations
3. **Cycle detection works**: Tests verify cycles are detected correctly
4. **Thread safety**: Concurrent access tests pass without deadlocks or corruption
5. **Performance**: Graph with 1000+ documents responds within 100ms for queries

---

## Unit Test Scenarios

```csharp
[Fact]
public void AddDocument_AddsVertex()
{
    var graph = new DocumentLinkGraph();
    graph.AddDocument("doc1.md");
    // Verify vertex exists
}

[Fact]
public void UpdateDocumentLinks_ReplacesExistingLinks()
{
    var graph = new DocumentLinkGraph();
    graph.UpdateDocumentLinks("doc1.md", new[] { "doc2.md", "doc3.md" });
    graph.UpdateDocumentLinks("doc1.md", new[] { "doc4.md" });

    var linked = graph.GetLinkedDocuments("doc1.md", 1, 10);
    Assert.Single(linked);
    Assert.Contains("doc4.md", linked);
}

[Fact]
public void WouldCreateCycle_DetectsSimpleCycle()
{
    var graph = new DocumentLinkGraph();
    graph.UpdateDocumentLinks("A.md", new[] { "B.md" });
    graph.UpdateDocumentLinks("B.md", new[] { "C.md" });

    Assert.False(graph.WouldCreateCycle("C.md", "A.md")); // C->A doesn't exist yet
    graph.UpdateDocumentLinks("C.md", new[] { "A.md" });
    Assert.True(graph.WouldCreateCycle("A.md", "C.md")); // Now it would create cycle
}

[Fact]
public void GetLinkedDocuments_RespectsMaxDepth()
{
    var graph = new DocumentLinkGraph();
    graph.UpdateDocumentLinks("A.md", new[] { "B.md" });
    graph.UpdateDocumentLinks("B.md", new[] { "C.md" });
    graph.UpdateDocumentLinks("C.md", new[] { "D.md" });

    var depth1 = graph.GetLinkedDocuments("A.md", 1, 10);
    Assert.Single(depth1);

    var depth2 = graph.GetLinkedDocuments("A.md", 2, 10);
    Assert.Equal(2, depth2.Count);
}

[Fact]
public void GetReferencingDocuments_ReturnsIncomingLinks()
{
    var graph = new DocumentLinkGraph();
    graph.UpdateDocumentLinks("A.md", new[] { "C.md" });
    graph.UpdateDocumentLinks("B.md", new[] { "C.md" });

    var refs = graph.GetReferencingDocuments("C.md");
    Assert.Equal(2, refs.Count);
    Assert.Contains("A.md", refs);
    Assert.Contains("B.md", refs);
}

[Fact]
public void DetectCycles_FindsAllCycles()
{
    var graph = new DocumentLinkGraph();
    graph.UpdateDocumentLinks("A.md", new[] { "B.md" });
    graph.UpdateDocumentLinks("B.md", new[] { "C.md" });
    graph.UpdateDocumentLinks("C.md", new[] { "A.md" }); // Cycle: A -> B -> C -> A

    var cycles = graph.DetectCycles();
    Assert.Single(cycles);
    Assert.Equal(3, cycles[0].Count);
}
```

---

## Notes

- QuikGraph's `BidirectionalGraph` is chosen over `AdjacencyGraph` for efficient in-edge queries needed by `GetReferencingDocuments`
- Thread safety is achieved via `ReaderWriterLockSlim` for read/write separation
- The graph is rebuilt on server startup during reconciliation to ensure consistency
- Cycles are detected and logged but do not prevent document processing; the RAG system handles them by tracking visited documents during traversal
- Consider adding metrics/telemetry for graph size and query performance in production
