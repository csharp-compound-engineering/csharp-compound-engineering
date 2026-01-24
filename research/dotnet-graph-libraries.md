# .NET/C# Graph Data Structure Libraries Research

## Executive Summary

This document provides a comprehensive analysis of .NET graph libraries with a focus on graph traversal, cycle detection, and especially **incremental cycle detection during edge insertion**. The key finding is that while several mature libraries exist for general graph operations, **no production-ready .NET library provides built-in incremental cycle detection**. However, QuikGraph emerges as the best choice for implementing a DAG enforcement pattern due to its comprehensive API and active maintenance.

---

## 1. Popular .NET Graph Libraries

### 1.1 QuikGraph (Recommended)

| Attribute | Value |
|-----------|-------|
| **NuGet Package** | `QuikGraph` |
| **Latest Version** | 2.5.0 |
| **License** | MIT |
| **GitHub** | [KeRNeLith/QuikGraph](https://github.com/KeRNeLith/QuikGraph) |
| **Documentation** | [kernelith.github.io/QuikGraph](https://kernelith.github.io/QuikGraph/) |
| **Platform Support** | .NET Standard 1.3+, .NET Core 1.0+, .NET Framework 3.5+ |

**Overview**: QuikGraph is the most mature and feature-complete graph library for .NET. Originally created by Jonathan "Peli" de Halleux in 2003 as QuickGraph, it has evolved through YC.QuickGraph into the current QuikGraph fork maintained by KeRNeLith. It provides generic directed/undirected graph data structures and a comprehensive algorithm suite.

**Installation**:
```bash
dotnet add package QuikGraph --version 2.5.0
```

Or in your `.csproj`:
```xml
<PackageReference Include="QuikGraph" Version="2.5.0" />
```

---

### 1.2 Microsoft.Msagl (Microsoft Automatic Graph Layout)

| Attribute | Value |
|-----------|-------|
| **NuGet Package** | `Microsoft.Msagl` |
| **Latest Version** | 1.1.6 |
| **License** | MIT |
| **GitHub** | [microsoft/automatic-graph-layout](https://github.com/microsoft/automatic-graph-layout) |
| **Focus** | Graph visualization and layout |

**Overview**: MSAGL is primarily a **graph layout and visualization library**, not a graph algorithm library. Created by Lev Nachmanson at Microsoft Research, it excels at producing hierarchical/layered layouts using the Sugiyama scheme. Use this if you need to visualize graphs, but not for cycle detection or traversal algorithms.

**Related Packages**:
- `Microsoft.Msagl.Drawing` - Drawing attributes, node/edge/graph classes
- `Microsoft.Msagl.GraphViewerGDI` - Windows Forms control
- `Microsoft.Msagl.WpfGraphControl` - WPF control

**Installation**:
```bash
dotnet add package Microsoft.Msagl --version 1.1.6
dotnet add package Microsoft.Msagl.Drawing --version 1.1.6
```

**Note**: Not suitable for cycle detection or graph algorithms. Use for rendering only.

---

### 1.3 Advanced.Algorithms

| Attribute | Value |
|-----------|-------|
| **NuGet Package** | `Advanced.Algorithms` |
| **Latest Version** | 0.0.504-beta |
| **License** | MIT |
| **GitHub** | [justcoding121/advanced-algorithms](https://github.com/justcoding121/Advanced-Algorithms) |
| **Status** | Beta (not production-ready) |

**Overview**: A collection of 100+ algorithms and data structures implemented in C#. While comprehensive, this is labeled by the author as "a curiosity-driven personal hobby" and remains in beta. It lacks built-in cycle detection but provides solid graph data structures.

**Graph Data Structures**:
- Adjacency List: Graph, WeightedGraph, DiGraph, WeightedDiGraph
- Adjacency Matrix: Graph, WeightedGraph, DiGraph, WeightedDiGraph

**Graph Algorithms**:
- Strongly Connected Components (Kosaraju, Tarjan)
- Shortest Path (Dijkstra with Fibonacci heap, Bellman-Ford, Floyd-Warshall, A*)
- Maximum Flow (Ford-Fulkerson, Edmonds-Karp, Push-Relabel)
- Bipartite Matching (Hopcroft-Karp)

**Installation**:
```bash
dotnet add package Advanced.Algorithms --version 0.0.504-beta
```

---

### 1.4 Dagger

| Attribute | Value |
|-----------|-------|
| **NuGet Package** | `Dagger` |
| **Latest Version** | 0.4.0 |
| **License** | MIT |
| **GitHub** | [ociaw/dagger](https://github.com/ociaw/dagger) |
| **Platform Support** | .NET Standard 2.0 |
| **Last Updated** | February 2019 |

**Overview**: A lightweight library specifically designed for **Directed Acyclic Graphs (DAGs)**. Provides cycle detection, path existence checking, and topological sorting. However, it has limited downloads (~8,900 total) and hasn't been updated since 2019.

**Features**:
- DAG-specific data structure
- Cycle detection
- Path existence checking
- Topological sorting

**Installation**:
```bash
dotnet add package Dagger --version 0.4.0
```

---

### 1.5 C-Sharp-Algorithms (GitHub Only)

| Attribute | Value |
|-----------|-------|
| **GitHub** | [aalhour/C-Sharp-Algorithms](https://github.com/aalhour/C-Sharp-Algorithms) |
| **License** | MIT |
| **Tests** | 623+ unit tests |

**Overview**: A comprehensive educational library with graph implementations and algorithms. Not available as a NuGet package - must be included as source code or project reference.

**Features**:
- Sparse/Dense graph representations
- Directed/Undirected (weighted and unweighted)
- **Cycle Detection** (built-in)
- **Topological Sort** (built-in)
- BFS/DFS traversal
- Dijkstra, Bellman-Ford

---

### 1.6 CycleDetection

| Attribute | Value |
|-----------|-------|
| **NuGet Package** | `CycleDetection` |
| **Latest Version** | 2.0.0 |
| **GitHub** | [danielrbradley/CycleDetection](https://github.com/danielrbradley/CycleDetection) |

**Overview**: A specialized library implementing **Tarjan's algorithm** for cycle detection. Useful for dependency sorting with cyclic reference handling, but limited to cycle detection only.

---

## 2. Feature Comparison Matrix

| Feature | QuikGraph | Advanced.Algorithms | Dagger | MSAGL |
|---------|-----------|---------------------|--------|-------|
| **Directed Graph** | Yes | Yes | Yes (DAG only) | Yes |
| **Undirected Graph** | Yes | Yes | No | Yes |
| **Weighted Edges** | Yes | Yes | No | Yes |
| **Cycle Detection** | Yes | No (built-in) | Yes | No |
| **Topological Sort** | Yes | No | Yes | No |
| **DFS/BFS** | Yes | Yes | Limited | No |
| **Shortest Path** | Yes | Yes | No | No |
| **Strongly Connected Components** | Yes | Yes | No | No |
| **Maximum Flow** | Yes | Yes | No | No |
| **Generic Types** | Yes | Yes | Yes | Yes |
| **Immutable Graphs** | Limited | No | No | No |
| **Active Maintenance** | Yes | Limited | No | Limited |
| **Production Ready** | Yes | No (Beta) | Yes | Yes |

---

## 3. Cycle Detection Capabilities

### 3.1 QuikGraph Cycle Detection

QuikGraph provides the `IsDirectedAcyclicGraph()` extension method that performs DFS-based cycle detection:

```csharp
using QuikGraph;
using QuikGraph.Algorithms;

var graph = new AdjacencyGraph<string, Edge<string>>();
graph.AddVerticesAndEdge(new Edge<string>("A", "B"));
graph.AddVerticesAndEdge(new Edge<string>("B", "C"));
graph.AddVerticesAndEdge(new Edge<string>("C", "A")); // Creates cycle

bool isDag = graph.IsDirectedAcyclicGraph(); // Returns false
```

**Internal Implementation**: Uses `DepthFirstSearchAlgorithm` with a `DagTester` that detects back edges. If any back edge is found during traversal, the graph contains a cycle.

**Exception Handling**: QuikGraph provides `NonAcyclicGraphException` that algorithms throw when they require an acyclic graph but encounter a cycle.

### 3.2 Topological Sort (Implies Cycle Detection)

Topological sorting inherently fails on cyclic graphs:

```csharp
IVertexListGraph<TVertex, TEdge> graph = ...;

try
{
    foreach (var vertex in graph.TopologicalSort())
    {
        Console.WriteLine(vertex);
    }
}
catch (NonAcyclicGraphException)
{
    Console.WriteLine("Graph contains a cycle - cannot topologically sort");
}
```

**Algorithms Available**:
- `TopologicalSortAlgorithm` - Standard DFS-based
- `SourceFirstTopologicalSortAlgorithm` - Sorts by increasing in-degree

### 3.3 Strongly Connected Components

SCCs can identify cyclic portions of a graph:

```csharp
var graph = new AdjacencyGraph<int, Edge<int>>();
// ... add vertices and edges ...

var components = new Dictionary<int, int>();
int componentCount = graph.StronglyConnectedComponents(components);

// If a component has more than 1 vertex, it contains a cycle
var cycleComponents = components
    .GroupBy(kv => kv.Value)
    .Where(g => g.Count() > 1)
    .ToList();
```

---

## 4. Incremental/Online Cycle Detection

### 4.1 The Problem

**Incremental cycle detection** means detecting whether adding a specific edge would create a cycle, ideally **before** committing the edge to the graph. This is crucial for:
- Task dependency systems (preventing circular dependencies)
- Build systems
- Workflow engines
- Any DAG enforcement scenario

### 4.2 Current Library Support

**No .NET library provides built-in incremental cycle detection.** The available options are:

1. **Full Check After Addition** (Simple but O(V+E) per insertion)
2. **Reachability Check Before Addition** (Better for sparse graphs)
3. **Academic Algorithms** (Complex to implement)

### 4.3 Simple Implementation: Check After Add

```csharp
using QuikGraph;
using QuikGraph.Algorithms;

public class SafeDAG<TVertex>
{
    private readonly AdjacencyGraph<TVertex, Edge<TVertex>> _graph;

    public SafeDAG()
    {
        _graph = new AdjacencyGraph<TVertex, Edge<TVertex>>();
    }

    public void AddVertex(TVertex vertex)
    {
        _graph.AddVertex(vertex);
    }

    /// <summary>
    /// Attempts to add an edge. Returns false if it would create a cycle.
    /// </summary>
    public bool TryAddEdge(TVertex source, TVertex target)
    {
        var edge = new Edge<TVertex>(source, target);

        // Add the edge
        _graph.AddVerticesAndEdge(edge);

        // Check if still a DAG
        if (_graph.IsDirectedAcyclicGraph())
        {
            return true; // Edge added successfully
        }

        // Remove the edge - it creates a cycle
        _graph.RemoveEdge(edge);
        return false;
    }

    public IEnumerable<TVertex> TopologicalSort()
    {
        return _graph.TopologicalSort();
    }
}
```

**Performance**: O(V + E) per edge insertion due to full DFS traversal.

### 4.4 Better Implementation: Reachability Check Before Add

Instead of adding then checking, check if target can reach source first:

```csharp
using QuikGraph;
using QuikGraph.Algorithms.Search;

public class OptimizedSafeDAG<TVertex>
{
    private readonly AdjacencyGraph<TVertex, Edge<TVertex>> _graph;

    public OptimizedSafeDAG()
    {
        _graph = new AdjacencyGraph<TVertex, Edge<TVertex>>();
    }

    public void AddVertex(TVertex vertex)
    {
        _graph.AddVertex(vertex);
    }

    /// <summary>
    /// Checks if adding edge (source -> target) would create a cycle.
    /// A cycle is created if target can already reach source.
    /// </summary>
    public bool WouldCreateCycle(TVertex source, TVertex target)
    {
        // If target can reach source, adding source->target creates a cycle
        if (!_graph.ContainsVertex(source) || !_graph.ContainsVertex(target))
            return false;

        // Use BFS/DFS from target to see if we can reach source
        var reachable = new HashSet<TVertex>();
        var bfs = new BreadthFirstSearchAlgorithm<TVertex, Edge<TVertex>>(_graph);

        bfs.SetRootVertex(target);
        bfs.DiscoverVertex += v => reachable.Add(v);
        bfs.Compute();

        return reachable.Contains(source);
    }

    public bool TryAddEdge(TVertex source, TVertex target)
    {
        if (WouldCreateCycle(source, target))
            return false;

        _graph.AddVerticesAndEdge(new Edge<TVertex>(source, target));
        return true;
    }
}
```

**Performance**: O(V + E) worst case, but typically much faster if the graph is sparse or the target vertex has limited reachability.

### 4.5 Advanced: Maintaining Topological Order

For better amortized performance, maintain a topological ordering and use it to quickly reject obvious cycles:

```csharp
public class TopologicalOrderDAG<TVertex> where TVertex : notnull
{
    private readonly AdjacencyGraph<TVertex, Edge<TVertex>> _graph;
    private readonly Dictionary<TVertex, int> _order;
    private int _nextOrder;

    public TopologicalOrderDAG()
    {
        _graph = new AdjacencyGraph<TVertex, Edge<TVertex>>();
        _order = new Dictionary<TVertex, int>();
        _nextOrder = 0;
    }

    public void AddVertex(TVertex vertex)
    {
        if (_graph.AddVertex(vertex))
        {
            _order[vertex] = _nextOrder++;
        }
    }

    public bool TryAddEdge(TVertex source, TVertex target)
    {
        // Ensure vertices exist
        AddVertex(source);
        AddVertex(target);

        // Quick check: if source already comes after target in order,
        // we need to verify no path exists from target to source
        if (_order[source] >= _order[target])
        {
            // Need full reachability check
            if (CanReach(target, source))
                return false;

            // Reorder affected vertices
            RecomputePartialOrder(source, target);
        }

        _graph.AddEdge(new Edge<TVertex>(source, target));
        return true;
    }

    private bool CanReach(TVertex from, TVertex to)
    {
        var visited = new HashSet<TVertex>();
        var stack = new Stack<TVertex>();
        stack.Push(from);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (current.Equals(to)) return true;
            if (visited.Add(current))
            {
                foreach (var edge in _graph.OutEdges(current))
                {
                    stack.Push(edge.Target);
                }
            }
        }
        return false;
    }

    private void RecomputePartialOrder(TVertex source, TVertex target)
    {
        // Simple approach: recompute full topological order
        // More sophisticated: only reorder affected subgraph
        var sorted = _graph.TopologicalSort().ToList();
        _order.Clear();
        for (int i = 0; i < sorted.Count; i++)
        {
            _order[sorted[i]] = i;
        }
        _nextOrder = sorted.Count;
    }
}
```

### 4.6 Academic Algorithms for Incremental Cycle Detection

For high-performance requirements with many insertions, consider implementing algorithms from academic research:

| Algorithm | Total Time | Best For | Reference |
|-----------|------------|----------|-----------|
| Haeupler et al. | O(m^(3/2)) | General sparse graphs | [TALG 2008](https://dl.acm.org/doi/10.1145/2071379.2071382) |
| Bender et al. (sparse) | O(min{m^(1/2), n^(2/3)} * m) | Sparse graphs | [TALG 2016](https://dl.acm.org/doi/10.1145/2756553) |
| Bender et al. (dense) | O(n^2 log n) | Dense graphs | [TALG 2016](https://dl.acm.org/doi/10.1145/2756553) |
| Bernstein & Chechik | O(m * sqrt(n) * log n) | General case | [SODA 2018](https://arxiv.org/abs/1810.03491) |

**Key Insight**: These algorithms maintain a "weak" or "pseudo" topological ordering that allows ties, and use it to limit the search space when an edge is inserted. The basic idea:
1. Maintain ordering numbers for vertices
2. When adding edge (u, v): if order[u] < order[v], no cycle possible
3. Otherwise, do a limited bidirectional search
4. Renumber affected vertices as needed

---

## 5. API Design and Usability

### 5.1 QuikGraph: Custom Node/Edge Types

```csharp
// Custom vertex type
public class TaskNode
{
    public string Id { get; set; }
    public string Name { get; set; }
    public TimeSpan Duration { get; set; }
}

// Custom edge type
public class DependencyEdge : Edge<TaskNode>
{
    public DependencyEdge(TaskNode source, TaskNode target)
        : base(source, target) { }

    public DependencyType Type { get; set; }
}

// Graph with custom types
var graph = new AdjacencyGraph<TaskNode, DependencyEdge>();
```

### 5.2 QuikGraph: Generic Type Support

QuikGraph is fully generic:

```csharp
// String vertices
var stringGraph = new AdjacencyGraph<string, Edge<string>>();

// Integer vertices
var intGraph = new AdjacencyGraph<int, Edge<int>>();

// Complex object vertices
var objectGraph = new AdjacencyGraph<MyClass, TaggedEdge<MyClass, string>>();
```

### 5.3 Graph Structure Types

**AdjacencyGraph**: Directed graph using adjacency lists. Efficient for sparse graphs where only out-edges need enumeration.

```csharp
var graph = new AdjacencyGraph<int, Edge<int>>();
```

**BidirectionalGraph**: Directed graph storing both in-edges and out-edges. Uses 2x memory but allows efficient enumeration in both directions.

```csharp
var graph = new BidirectionalGraph<int, Edge<int>>();
foreach (var e in graph.InEdges(vertex)) { }  // Efficient
foreach (var e in graph.OutEdges(vertex)) { } // Efficient
```

**UndirectedGraph**: Undirected graph structure.

```csharp
var graph = new UndirectedGraph<int, Edge<int>>();
```

### 5.4 Edge Types

```csharp
// Basic edge (class, reference type)
var edge1 = new Edge<int>(1, 2);

// Tagged edge with metadata
var edge2 = new TaggedEdge<int, string>(1, 2, "relationship");

// Struct edge (value type, better performance)
var edge3 = new SEdge<int>(1, 2);

// Equatable edge
var edge4 = new EquatableEdge<int>(1, 2);
```

### 5.5 LINQ Integration

QuikGraph works naturally with LINQ:

```csharp
// Filter vertices
var filtered = graph.Vertices.Where(v => v.StartsWith("A"));

// Count edges
var edgeCount = graph.Edges.Count();

// Find specific edges
var edgesToRemove = graph.Edges
    .Where(e => e.Source == vertex)
    .ToList();
```

### 5.6 Serialization

QuikGraph supports serialization through QuikGraph.Serialization:

```bash
dotnet add package QuikGraph.Serialization
```

```csharp
using QuikGraph.Serialization;

// GraphML serialization
graph.SerializeToGraphML<int, Edge<int>, AdjacencyGraph<int, Edge<int>>>("graph.graphml");

// Deserialization
var loaded = new AdjacencyGraph<int, Edge<int>>();
loaded.DeserializeFromGraphML("graph.graphml",
    id => int.Parse(id),
    (source, target, id) => new Edge<int>(source, target));
```

---

## 6. Performance Considerations

### 6.1 Time Complexity

| Operation | AdjacencyGraph | BidirectionalGraph |
|-----------|---------------|-------------------|
| Add Vertex | O(1) | O(1) |
| Add Edge | O(1) | O(1) |
| Remove Vertex | O(E) | O(degree) |
| Remove Edge | O(out-degree) | O(1) |
| Out-Edges | O(1) | O(1) |
| In-Edges | O(E) | O(1) |
| Contains Vertex | O(1) | O(1) |
| Contains Edge | O(out-degree) | O(out-degree) |

### 6.2 Algorithm Complexity

| Algorithm | Time Complexity | Space |
|-----------|----------------|-------|
| DFS/BFS | O(V + E) | O(V) |
| Topological Sort | O(V + E) | O(V) |
| IsDirectedAcyclicGraph | O(V + E) | O(V) |
| Strongly Connected Components | O(V + E) | O(V) |
| Dijkstra | O((V + E) log V) | O(V) |
| Bellman-Ford | O(V * E) | O(V) |
| A* | O(E) best, O(V^2) worst | O(V) |

### 6.3 Memory Efficiency

**AdjacencyGraph**: Most memory-efficient for directed graphs where only forward traversal is needed.
- Storage: O(V + E)

**BidirectionalGraph**: 2x edge storage for bidirectional traversal.
- Storage: O(V + 2E)

**Struct Edges**: Using `SEdge<T>` instead of `Edge<T>` reduces GC pressure and improves cache locality for large graphs.

### 6.4 Large Graph Handling

For graphs with 50,000+ elements:
- Use struct-based edges (`SEdge<T>`)
- Consider `ArrayAdjacencyGraph` for read-heavy workloads
- Use `CompressedSparseRowGraph` for static graphs with minimal mutation
- MSAGL can handle large graphs for layout with `MdsLayoutSettings` or `RankingLayoutSettings`

---

## 7. Code Examples

### 7.1 Creating a Directed Graph

```csharp
using QuikGraph;

// Method 1: Empty graph, add elements incrementally
var graph1 = new AdjacencyGraph<string, Edge<string>>();
graph1.AddVertex("A");
graph1.AddVertex("B");
graph1.AddVertex("C");
graph1.AddEdge(new Edge<string>("A", "B"));
graph1.AddEdge(new Edge<string>("B", "C"));

// Method 2: Add vertices implicitly with edges
var graph2 = new AdjacencyGraph<string, Edge<string>>();
graph2.AddVerticesAndEdge(new Edge<string>("A", "B"));
graph2.AddVerticesAndEdge(new Edge<string>("B", "C"));

// Method 3: From edge collection
var edges = new[]
{
    new Edge<string>("A", "B"),
    new Edge<string>("B", "C"),
    new Edge<string>("A", "C")
};
var graph3 = edges.ToAdjacencyGraph<string, Edge<string>>();

// Method 4: From dictionary (adjacency list representation)
var adjacencyList = new Dictionary<string, string[]>
{
    ["A"] = new[] { "B", "C" },
    ["B"] = new[] { "C" },
    ["C"] = Array.Empty<string>()
};
var graph4 = adjacencyList.ToAdjacencyGraph(
    kv => Array.ConvertAll(kv.Value, v => new Edge<string>(kv.Key, v))
);
```

### 7.2 Detecting Cycles

```csharp
using QuikGraph;
using QuikGraph.Algorithms;

var graph = new AdjacencyGraph<string, Edge<string>>();
graph.AddVerticesAndEdge(new Edge<string>("A", "B"));
graph.AddVerticesAndEdge(new Edge<string>("B", "C"));

Console.WriteLine(graph.IsDirectedAcyclicGraph()); // True

graph.AddVerticesAndEdge(new Edge<string>("C", "A")); // Creates cycle

Console.WriteLine(graph.IsDirectedAcyclicGraph()); // False
```

### 7.3 Implementing "Add Edge If No Cycle" Pattern

```csharp
using QuikGraph;
using QuikGraph.Algorithms;

public static class GraphExtensions
{
    /// <summary>
    /// Attempts to add an edge only if it doesn't create a cycle.
    /// Returns true if the edge was added, false if it would create a cycle.
    /// </summary>
    public static bool TryAddEdgeIfAcyclic<TVertex, TEdge>(
        this IMutableVertexAndEdgeListGraph<TVertex, TEdge> graph,
        TEdge edge)
        where TEdge : IEdge<TVertex>
    {
        // Ensure vertices exist
        graph.AddVertex(edge.Source);
        graph.AddVertex(edge.Target);

        // Add the edge
        graph.AddEdge(edge);

        // Check if still acyclic
        if (graph.IsDirectedAcyclicGraph())
        {
            return true;
        }

        // Remove the edge - it creates a cycle
        graph.RemoveEdge(edge);
        return false;
    }
}

// Usage
var graph = new AdjacencyGraph<string, Edge<string>>();
graph.AddVerticesAndEdge(new Edge<string>("A", "B"));
graph.AddVerticesAndEdge(new Edge<string>("B", "C"));

bool added1 = graph.TryAddEdgeIfAcyclic(new Edge<string>("C", "D")); // true
bool added2 = graph.TryAddEdgeIfAcyclic(new Edge<string>("D", "A")); // true
bool added3 = graph.TryAddEdgeIfAcyclic(new Edge<string>("C", "A")); // false - creates cycle
```

### 7.4 Complete DAG Wrapper Class

```csharp
using QuikGraph;
using QuikGraph.Algorithms;
using QuikGraph.Algorithms.Search;

/// <summary>
/// A Directed Acyclic Graph that prevents cycles on edge insertion.
/// </summary>
public class DirectedAcyclicGraph<TVertex> where TVertex : notnull
{
    private readonly AdjacencyGraph<TVertex, Edge<TVertex>> _graph;

    public DirectedAcyclicGraph()
    {
        _graph = new AdjacencyGraph<TVertex, Edge<TVertex>>();
    }

    public int VertexCount => _graph.VertexCount;
    public int EdgeCount => _graph.EdgeCount;
    public IEnumerable<TVertex> Vertices => _graph.Vertices;
    public IEnumerable<Edge<TVertex>> Edges => _graph.Edges;

    public bool AddVertex(TVertex vertex) => _graph.AddVertex(vertex);

    /// <summary>
    /// Adds an edge if it doesn't create a cycle.
    /// </summary>
    /// <returns>True if edge was added, false if it would create a cycle.</returns>
    public bool TryAddEdge(TVertex source, TVertex target)
    {
        // Check if adding this edge would create a cycle
        // A cycle is created if target can already reach source
        if (CanReach(target, source))
        {
            return false;
        }

        _graph.AddVertex(source);
        _graph.AddVertex(target);
        _graph.AddEdge(new Edge<TVertex>(source, target));
        return true;
    }

    /// <summary>
    /// Adds an edge, throwing if it would create a cycle.
    /// </summary>
    public void AddEdge(TVertex source, TVertex target)
    {
        if (!TryAddEdge(source, target))
        {
            throw new InvalidOperationException(
                $"Adding edge ({source} -> {target}) would create a cycle.");
        }
    }

    /// <summary>
    /// Checks if there's a path from source to target.
    /// </summary>
    public bool CanReach(TVertex source, TVertex target)
    {
        if (!_graph.ContainsVertex(source) || !_graph.ContainsVertex(target))
            return false;

        if (source.Equals(target))
            return true;

        var visited = new HashSet<TVertex>();
        var queue = new Queue<TVertex>();
        queue.Enqueue(source);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (current.Equals(target))
                return true;

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

    /// <summary>
    /// Returns vertices in topological order.
    /// </summary>
    public IEnumerable<TVertex> TopologicalSort()
    {
        return _graph.TopologicalSort();
    }

    /// <summary>
    /// Gets all vertices that the given vertex depends on (ancestors).
    /// </summary>
    public IEnumerable<TVertex> GetDependencies(TVertex vertex)
    {
        var dependencies = new HashSet<TVertex>();
        CollectDependencies(vertex, dependencies);
        return dependencies;
    }

    private void CollectDependencies(TVertex vertex, HashSet<TVertex> collected)
    {
        // For dependencies, we need to traverse incoming edges
        // With AdjacencyGraph, we need to search all edges
        foreach (var edge in _graph.Edges.Where(e => e.Target.Equals(vertex)))
        {
            if (collected.Add(edge.Source))
            {
                CollectDependencies(edge.Source, collected);
            }
        }
    }

    /// <summary>
    /// Gets all vertices that depend on the given vertex (descendants).
    /// </summary>
    public IEnumerable<TVertex> GetDependents(TVertex vertex)
    {
        var dependents = new HashSet<TVertex>();
        CollectDependents(vertex, dependents);
        return dependents;
    }

    private void CollectDependents(TVertex vertex, HashSet<TVertex> collected)
    {
        foreach (var edge in _graph.OutEdges(vertex))
        {
            if (collected.Add(edge.Target))
            {
                CollectDependents(edge.Target, collected);
            }
        }
    }

    public bool RemoveVertex(TVertex vertex) => _graph.RemoveVertex(vertex);
    public bool RemoveEdge(Edge<TVertex> edge) => _graph.RemoveEdge(edge);
}
```

### 7.5 Strongly Connected Components Example

```csharp
using QuikGraph;
using QuikGraph.Algorithms;

var graph = new AdjacencyGraph<string, Edge<string>>();

// Add edges creating multiple SCCs
graph.AddVerticesAndEdge(new Edge<string>("A", "B"));
graph.AddVerticesAndEdge(new Edge<string>("B", "C"));
graph.AddVerticesAndEdge(new Edge<string>("C", "A")); // SCC: A-B-C

graph.AddVerticesAndEdge(new Edge<string>("D", "E"));
graph.AddVerticesAndEdge(new Edge<string>("E", "D")); // SCC: D-E

graph.AddVerticesAndEdge(new Edge<string>("C", "D")); // Connects SCCs

var components = new Dictionary<string, int>();
int count = graph.StronglyConnectedComponents(components);

Console.WriteLine($"Found {count} strongly connected components:");
foreach (var group in components.GroupBy(kv => kv.Value))
{
    var vertices = string.Join(", ", group.Select(kv => kv.Key));
    Console.WriteLine($"  Component {group.Key}: [{vertices}]");
}
// Output:
// Found 3 strongly connected components:
//   Component 0: [A, B, C]
//   Component 1: [D, E]
//   Component 2: [] (or variations based on traversal order)
```

---

## 8. Recommendation

### For Cycle Detection on Edge Insertion: QuikGraph

**QuikGraph** is the recommended library for implementing cycle detection during edge insertion because:

1. **Mature and Production-Ready**: Active development, clean NuGet packaging, wide .NET version support
2. **Comprehensive Algorithm Suite**: DFS, BFS, topological sort, cycle detection, SCCs
3. **Flexible API**: Generic types, multiple graph structures, easy custom vertex/edge types
4. **`IsDirectedAcyclicGraph()` Method**: Built-in cycle detection via DFS back-edge detection
5. **Strong Community**: Well-documented wiki, active GitHub repository
6. **Modern C# Patterns**: Extension methods, LINQ integration, .NET Standard support

### Implementation Strategy

1. **Simple Case (< 1,000 edges)**: Use the basic "add then check" pattern
2. **Medium Scale (1,000 - 100,000 edges)**: Use reachability check before add
3. **Large Scale (> 100,000 edges)**: Consider implementing incremental algorithms from academic literature

### Packages to Install

```xml
<ItemGroup>
    <!-- Core graph library -->
    <PackageReference Include="QuikGraph" Version="2.5.0" />

    <!-- Optional: Serialization support -->
    <PackageReference Include="QuikGraph.Serialization" Version="2.5.0" />

    <!-- Optional: GraphViz rendering -->
    <PackageReference Include="QuikGraph.Graphviz" Version="2.5.0" />
</ItemGroup>
```

---

## Sources

- [QuikGraph NuGet Package](https://www.nuget.org/packages/QuikGraph)
- [QuikGraph GitHub Repository](https://github.com/KeRNeLith/QuikGraph)
- [QuikGraph Documentation](https://kernelith.github.io/QuikGraph/)
- [QuikGraph Wiki - Creating Graphs](https://github.com/KeRNeLith/QuikGraph/wiki/Creating-Graphs)
- [QuikGraph Wiki - Topological Sort](https://github.com/KeRNeLith/QuikGraph/wiki/Topological-Sort)
- [QuikGraph Wiki - Strongly Connected Components](https://github.com/KeRNeLith/QuikGraph/wiki/Strongly-Connected-Components)
- [QuickGraph.NET Cheatsheet](https://gist.github.com/Jbat1Jumper/95c77d216981e13952cf7f22e653d80d)
- [Microsoft.Msagl NuGet](https://www.nuget.org/packages/Microsoft.Msagl/)
- [Microsoft MSAGL GitHub](https://github.com/microsoft/automatic-graph-layout)
- [Advanced.Algorithms GitHub](https://github.com/justcoding121/Advanced-Algorithms)
- [C-Sharp-Algorithms GitHub](https://github.com/aalhour/C-Sharp-Algorithms)
- [Dagger NuGet Package](https://www.nuget.org/packages/Dagger/)
- [Dagger GitHub](https://github.com/ociaw/dagger)
- [CycleDetection NuGet](https://www.nuget.org/packages/CycleDetection)
- [Incremental Cycle Detection (Bernstein & Chechik)](https://arxiv.org/abs/1810.03491)
- [Incremental Topological Ordering (Bender et al.)](https://dl.acm.org/doi/10.1145/2756553)
- [Cycle Detection in C# - Section.io](https://www.section.io/engineering-education/graph-cycle-detection-csharp/)
