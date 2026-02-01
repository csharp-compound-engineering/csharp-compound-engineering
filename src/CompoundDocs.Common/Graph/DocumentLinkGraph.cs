using QuikGraph;
using QuikGraph.Algorithms;

namespace CompoundDocs.Common.Graph;

/// <summary>
/// Defines the types of relationships between documents.
/// </summary>
public enum DocumentRelationshipType
{
    /// <summary>
    /// Generic reference link between documents.
    /// </summary>
    References = 0,

    /// <summary>
    /// Parent-child hierarchical relationship (source is parent of target).
    /// </summary>
    Parent = 1,

    /// <summary>
    /// Child-parent hierarchical relationship (source is child of target).
    /// </summary>
    Child = 2,

    /// <summary>
    /// Documents that are related but not hierarchically.
    /// </summary>
    Related = 3,

    /// <summary>
    /// Source document supersedes target document.
    /// </summary>
    Supersedes = 4,

    /// <summary>
    /// Source document depends on target document.
    /// </summary>
    DependsOn = 5
}

/// <summary>
/// Represents a typed edge between two documents with a relationship type.
/// </summary>
public sealed class TypedDocumentEdge : Edge<string>
{
    /// <summary>
    /// Gets the relationship type between source and target documents.
    /// </summary>
    public DocumentRelationshipType RelationshipType { get; }

    /// <summary>
    /// Creates a new typed document edge.
    /// </summary>
    /// <param name="source">The source document path.</param>
    /// <param name="target">The target document path.</param>
    /// <param name="relationshipType">The relationship type.</param>
    public TypedDocumentEdge(string source, string target, DocumentRelationshipType relationshipType = DocumentRelationshipType.References)
        : base(source, target)
    {
        RelationshipType = relationshipType;
    }
}

/// <summary>
/// Represents a document relationship with its type.
/// </summary>
public sealed class DocumentRelationship
{
    /// <summary>
    /// Gets the source document path.
    /// </summary>
    public required string SourceDocument { get; init; }

    /// <summary>
    /// Gets the target document path.
    /// </summary>
    public required string TargetDocument { get; init; }

    /// <summary>
    /// Gets the relationship type.
    /// </summary>
    public required DocumentRelationshipType RelationshipType { get; init; }
}

/// <summary>
/// Manages document link relationships using QuikGraph.
/// Supports typed relationships for knowledge graph traversal.
/// </summary>
public sealed class DocumentLinkGraph
{
    private readonly AdjacencyGraph<string, Edge<string>> _graph = new();
    private readonly AdjacencyGraph<string, TypedDocumentEdge> _typedGraph = new();
    private readonly object _lock = new();

    /// <summary>
    /// Adds a document as a vertex.
    /// </summary>
    public void AddDocument(string documentPath)
    {
        lock (_lock)
        {
            _graph.AddVertex(documentPath);
        }
    }

    /// <summary>
    /// Adds a link between two documents.
    /// </summary>
    public void AddLink(string sourceDocument, string targetDocument)
    {
        lock (_lock)
        {
            _graph.AddVerticesAndEdge(new Edge<string>(sourceDocument, targetDocument));
        }
    }

    /// <summary>
    /// Removes a document and all its links.
    /// </summary>
    public void RemoveDocument(string documentPath)
    {
        lock (_lock)
        {
            _graph.RemoveVertex(documentPath);
        }
    }

    /// <summary>
    /// Clears all links for a document (used before re-indexing).
    /// </summary>
    public void ClearLinksFrom(string documentPath)
    {
        lock (_lock)
        {
            var edges = _graph.OutEdges(documentPath).ToList();
            foreach (var edge in edges)
            {
                _graph.RemoveEdge(edge);
            }
        }
    }

    /// <summary>
    /// Gets all documents linked from the specified document.
    /// </summary>
    public IReadOnlyList<string> GetLinkedDocuments(string documentPath)
    {
        lock (_lock)
        {
            if (!_graph.ContainsVertex(documentPath))
                return [];

            return _graph.OutEdges(documentPath)
                .Select(e => e.Target)
                .ToList();
        }
    }

    /// <summary>
    /// Gets all documents that link to the specified document.
    /// </summary>
    public IReadOnlyList<string> GetIncomingLinks(string documentPath)
    {
        lock (_lock)
        {
            if (!_graph.ContainsVertex(documentPath))
                return [];

            return _graph.Edges
                .Where(e => e.Target == documentPath)
                .Select(e => e.Source)
                .ToList();
        }
    }

    /// <summary>
    /// Gets linked documents up to a specified depth.
    /// </summary>
    public IReadOnlyList<string> GetLinkedDocumentsWithDepth(
        string documentPath,
        int maxDepth,
        int maxDocuments = int.MaxValue)
    {
        var visited = new HashSet<string> { documentPath };
        var result = new List<string>();
        var queue = new Queue<(string Doc, int Depth)>();

        lock (_lock)
        {
            if (!_graph.ContainsVertex(documentPath))
                return result;

            // Start with immediate links
            foreach (var edge in _graph.OutEdges(documentPath))
            {
                if (!visited.Contains(edge.Target))
                {
                    queue.Enqueue((edge.Target, 1));
                    visited.Add(edge.Target);
                }
            }

            while (queue.Count > 0 && result.Count < maxDocuments)
            {
                var (doc, depth) = queue.Dequeue();
                result.Add(doc);

                if (depth < maxDepth)
                {
                    foreach (var edge in _graph.OutEdges(doc))
                    {
                        if (!visited.Contains(edge.Target))
                        {
                            queue.Enqueue((edge.Target, depth + 1));
                            visited.Add(edge.Target);
                        }
                    }
                }
            }
        }

        return result.Take(maxDocuments).ToList();
    }

    /// <summary>
    /// Checks if the graph is acyclic (no circular references).
    /// </summary>
    public bool IsAcyclic()
    {
        lock (_lock)
        {
            return _graph.IsDirectedAcyclicGraph();
        }
    }

    /// <summary>
    /// Detects circular references starting from a document.
    /// </summary>
    public IReadOnlyList<string>? FindCycle(string documentPath)
    {
        lock (_lock)
        {
            if (!_graph.ContainsVertex(documentPath))
                return null;

            var visited = new HashSet<string>();
            var path = new List<string>();

            if (DfsDetectCycle(documentPath, visited, path))
            {
                return path;
            }

            return null;
        }
    }

    private bool DfsDetectCycle(string current, HashSet<string> visited, List<string> path)
    {
        if (path.Contains(current))
        {
            path.Add(current); // Complete the cycle
            return true;
        }

        if (visited.Contains(current))
            return false;

        visited.Add(current);
        path.Add(current);

        foreach (var edge in _graph.OutEdges(current))
        {
            if (DfsDetectCycle(edge.Target, visited, path))
                return true;
        }

        path.Remove(current);
        return false;
    }

    /// <summary>
    /// Gets the total number of documents in the graph.
    /// </summary>
    public int DocumentCount
    {
        get
        {
            lock (_lock)
            {
                return _graph.VertexCount;
            }
        }
    }

    /// <summary>
    /// Gets the total number of links in the graph.
    /// </summary>
    public int LinkCount
    {
        get
        {
            lock (_lock)
            {
                return _graph.EdgeCount;
            }
        }
    }

    #region Typed Relationship Methods

    /// <summary>
    /// Adds a typed link between two documents.
    /// </summary>
    /// <param name="sourceDocument">The source document path.</param>
    /// <param name="targetDocument">The target document path.</param>
    /// <param name="relationshipType">The relationship type.</param>
    public void AddTypedLink(
        string sourceDocument,
        string targetDocument,
        DocumentRelationshipType relationshipType)
    {
        lock (_lock)
        {
            // Add to untyped graph for backward compatibility
            _graph.AddVerticesAndEdge(new Edge<string>(sourceDocument, targetDocument));

            // Add to typed graph
            _typedGraph.AddVerticesAndEdge(new TypedDocumentEdge(
                sourceDocument,
                targetDocument,
                relationshipType));
        }
    }

    /// <summary>
    /// Gets all typed relationships originating from a document.
    /// </summary>
    /// <param name="documentPath">The document path.</param>
    /// <returns>A list of document relationships.</returns>
    public IReadOnlyList<DocumentRelationship> GetTypedRelationships(string documentPath)
    {
        lock (_lock)
        {
            if (!_typedGraph.ContainsVertex(documentPath))
                return [];

            return _typedGraph.OutEdges(documentPath)
                .Select(e => new DocumentRelationship
                {
                    SourceDocument = e.Source,
                    TargetDocument = e.Target,
                    RelationshipType = e.RelationshipType
                })
                .ToList();
        }
    }

    /// <summary>
    /// Gets all typed relationships pointing to a document.
    /// </summary>
    /// <param name="documentPath">The document path.</param>
    /// <returns>A list of incoming document relationships.</returns>
    public IReadOnlyList<DocumentRelationship> GetIncomingTypedRelationships(string documentPath)
    {
        lock (_lock)
        {
            if (!_typedGraph.ContainsVertex(documentPath))
                return [];

            return _typedGraph.Edges
                .Where(e => e.Target == documentPath)
                .Select(e => new DocumentRelationship
                {
                    SourceDocument = e.Source,
                    TargetDocument = e.Target,
                    RelationshipType = e.RelationshipType
                })
                .ToList();
        }
    }

    /// <summary>
    /// Gets documents related by a specific relationship type.
    /// </summary>
    /// <param name="documentPath">The document path.</param>
    /// <param name="relationshipType">The relationship type to filter by.</param>
    /// <returns>A list of document paths with the specified relationship.</returns>
    public IReadOnlyList<string> GetDocumentsByRelationshipType(
        string documentPath,
        DocumentRelationshipType relationshipType)
    {
        lock (_lock)
        {
            if (!_typedGraph.ContainsVertex(documentPath))
                return [];

            return _typedGraph.OutEdges(documentPath)
                .Where(e => e.RelationshipType == relationshipType)
                .Select(e => e.Target)
                .ToList();
        }
    }

    /// <summary>
    /// Finds all related documents by traversing the knowledge graph.
    /// Uses breadth-first search to find documents within the specified depth.
    /// </summary>
    /// <param name="documentPath">The starting document path.</param>
    /// <param name="maxDepth">Maximum traversal depth.</param>
    /// <param name="relationshipTypes">Optional filter for specific relationship types. If null, all types are included.</param>
    /// <param name="maxDocuments">Maximum number of documents to return.</param>
    /// <returns>A list of related documents with their relationships.</returns>
    public IReadOnlyList<DocumentRelationship> FindRelatedDocuments(
        string documentPath,
        int maxDepth,
        IEnumerable<DocumentRelationshipType>? relationshipTypes = null,
        int maxDocuments = int.MaxValue)
    {
        var visited = new HashSet<string> { documentPath };
        var result = new List<DocumentRelationship>();
        var queue = new Queue<(string Doc, int Depth)>();
        var typeFilter = relationshipTypes?.ToHashSet();

        lock (_lock)
        {
            if (!_typedGraph.ContainsVertex(documentPath))
                return result;

            // Start with immediate links
            foreach (var edge in _typedGraph.OutEdges(documentPath))
            {
                if (typeFilter != null && !typeFilter.Contains(edge.RelationshipType))
                    continue;

                if (!visited.Contains(edge.Target))
                {
                    queue.Enqueue((edge.Target, 1));
                    visited.Add(edge.Target);
                    result.Add(new DocumentRelationship
                    {
                        SourceDocument = edge.Source,
                        TargetDocument = edge.Target,
                        RelationshipType = edge.RelationshipType
                    });
                }
            }

            while (queue.Count > 0 && result.Count < maxDocuments)
            {
                var (doc, depth) = queue.Dequeue();

                if (depth < maxDepth)
                {
                    foreach (var edge in _typedGraph.OutEdges(doc))
                    {
                        if (typeFilter != null && !typeFilter.Contains(edge.RelationshipType))
                            continue;

                        if (!visited.Contains(edge.Target))
                        {
                            queue.Enqueue((edge.Target, depth + 1));
                            visited.Add(edge.Target);
                            result.Add(new DocumentRelationship
                            {
                                SourceDocument = edge.Source,
                                TargetDocument = edge.Target,
                                RelationshipType = edge.RelationshipType
                            });

                            if (result.Count >= maxDocuments)
                                break;
                        }
                    }
                }
            }
        }

        return result.Take(maxDocuments).ToList();
    }

    /// <summary>
    /// Gets child documents (where source is the parent).
    /// </summary>
    /// <param name="documentPath">The parent document path.</param>
    /// <returns>A list of child document paths.</returns>
    public IReadOnlyList<string> GetChildDocuments(string documentPath)
    {
        return GetDocumentsByRelationshipType(documentPath, DocumentRelationshipType.Parent);
    }

    /// <summary>
    /// Gets parent documents (where target is the child).
    /// </summary>
    /// <param name="documentPath">The child document path.</param>
    /// <returns>A list of parent document paths.</returns>
    public IReadOnlyList<string> GetParentDocuments(string documentPath)
    {
        lock (_lock)
        {
            if (!_typedGraph.ContainsVertex(documentPath))
                return [];

            return _typedGraph.Edges
                .Where(e => e.Target == documentPath && e.RelationshipType == DocumentRelationshipType.Parent)
                .Select(e => e.Source)
                .ToList();
        }
    }

    /// <summary>
    /// Gets documents that this document depends on.
    /// </summary>
    /// <param name="documentPath">The document path.</param>
    /// <returns>A list of dependency document paths.</returns>
    public IReadOnlyList<string> GetDependencies(string documentPath)
    {
        return GetDocumentsByRelationshipType(documentPath, DocumentRelationshipType.DependsOn);
    }

    /// <summary>
    /// Gets documents that depend on this document.
    /// </summary>
    /// <param name="documentPath">The document path.</param>
    /// <returns>A list of dependent document paths.</returns>
    public IReadOnlyList<string> GetDependents(string documentPath)
    {
        lock (_lock)
        {
            if (!_typedGraph.ContainsVertex(documentPath))
                return [];

            return _typedGraph.Edges
                .Where(e => e.Target == documentPath && e.RelationshipType == DocumentRelationshipType.DependsOn)
                .Select(e => e.Source)
                .ToList();
        }
    }

    /// <summary>
    /// Clears all typed links from a document (used before re-indexing).
    /// </summary>
    /// <param name="documentPath">The document path.</param>
    public void ClearTypedLinksFrom(string documentPath)
    {
        lock (_lock)
        {
            var edges = _typedGraph.OutEdges(documentPath).ToList();
            foreach (var edge in edges)
            {
                _typedGraph.RemoveEdge(edge);
            }
        }
    }

    /// <summary>
    /// Gets the count of typed relationships in the graph.
    /// </summary>
    public int TypedRelationshipCount
    {
        get
        {
            lock (_lock)
            {
                return _typedGraph.EdgeCount;
            }
        }
    }

    /// <summary>
    /// Gets a summary of relationship counts by type.
    /// </summary>
    /// <returns>A dictionary of relationship type to count.</returns>
    public IReadOnlyDictionary<DocumentRelationshipType, int> GetRelationshipTypeCounts()
    {
        lock (_lock)
        {
            return _typedGraph.Edges
                .GroupBy(e => e.RelationshipType)
                .ToDictionary(g => g.Key, g => g.Count());
        }
    }

    #endregion
}
