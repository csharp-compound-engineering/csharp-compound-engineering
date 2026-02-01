# Phase 051: RAG Retrieval Service

> **Status**: NOT_STARTED
> **Effort Estimate**: 8-10 hours
> **Category**: Database & Storage
> **Prerequisites**: Phase 050 (Vector Store Integration), Phase 032 (Document Repository)

---

## Spec References

This phase implements the RAG retrieval infrastructure defined in:

- **spec/mcp-server/tools.md** - `rag_query` tool (Tool #1): relevance threshold, link depth, critical doc injection
- **spec/configuration.md** - Retrieval settings: `retrieval.min_relevance_score`, `retrieval.max_results`, `retrieval.max_linked_docs`, `link_resolution.max_depth`
- **spec/mcp-server/database-schema.md** - Document and DocumentChunk models with promotion_level field
- **research/semantic-kernel-ollama-rag-research.md** - RAG pipeline patterns with Semantic Kernel

---

## Objectives

1. Define `IRAGRetrievalService` interface for document retrieval operations
2. Implement document retrieval with configurable relevance threshold (default 0.7)
3. Implement link depth following for related docs (default max_depth: 2)
4. Implement critical document injection (prepend critical docs regardless of relevance)
5. Implement relevance boosting for important/critical promotion levels
6. Create context assembly service for RAG generation pipeline
7. Support both whole-document and chunk-based retrieval

---

## Acceptance Criteria

### IRAGRetrievalService Interface

- [ ] `IRAGRetrievalService` interface defined in `CompoundDocs.McpServer.Abstractions`
- [ ] `RetrieveRelevantDocumentsAsync` method with query embedding and options
- [ ] `RetrieveWithLinkedDocsAsync` method that follows document links
- [ ] `AssembleRAGContextAsync` method for complete context building
- [ ] All methods respect tenant context isolation

### Document Retrieval with Relevance Threshold

- [ ] Default `min_relevance_score` of 0.7 for RAG queries
- [ ] Configurable via `RetrievalOptions` (project config or tool parameter)
- [ ] Tool parameter takes precedence over project config
- [ ] Results filtered by relevance score after vector search
- [ ] Returns `RetrievalResult` with documents and relevance metadata

### Link Depth Following

- [ ] `IDocumentLinkResolver` interface for extracting links from documents
- [ ] Markdown link parsing using Markdig AST (from Phase 015)
- [ ] Recursive link resolution up to `link_resolution.max_depth` (default: 2)
- [ ] Cycle detection to prevent infinite loops in circular references
- [ ] `max_linked_docs` limit (default: 5) to bound context size
- [ ] Linked docs returned with `linked_from` attribution

### Critical Document Injection

- [ ] Critical docs (promotion_level = "critical") prepended to context
- [ ] Controlled by `include_critical` parameter (default: true)
- [ ] Critical docs included regardless of relevance score
- [ ] Critical docs retrieved from current tenant context only
- [ ] Deduplicated if critical doc also matches query

### Relevance Boosting

- [ ] `IRelevanceBooster` interface for promotion-based score adjustment
- [ ] Boost factors: critical (+0.15), important (+0.10), standard (no boost)
- [ ] Boosted scores capped at 1.0
- [ ] Boosting applied after initial retrieval, before ranking
- [ ] Configurable boost factors via options pattern

### Context Assembly

- [ ] `IRAGContextAssembler` interface for building RAG prompts
- [ ] Assembles context from: critical docs + relevant docs + linked docs
- [ ] Context ordering: critical first, then by relevance score descending
- [ ] Character count tracking for context size management
- [ ] Source attribution for each context segment

### Chunk Support

- [ ] Chunk retrieval when documents are chunked (>500 lines)
- [ ] Chunk results aggregated to parent document level
- [ ] Best-matching chunk used for relevance score
- [ ] Full document content assembled from chunks for RAG

---

## Implementation Notes

### IRAGRetrievalService Interface

```csharp
namespace CompoundDocs.McpServer.Abstractions;

/// <summary>
/// Service for retrieving documents relevant to a RAG query.
/// Handles relevance filtering, link following, and critical doc injection.
/// </summary>
public interface IRAGRetrievalService
{
    /// <summary>
    /// Retrieves documents relevant to the query embedding.
    /// </summary>
    /// <param name="queryEmbedding">The embedding vector for the query.</param>
    /// <param name="options">Retrieval options (thresholds, limits, filters).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Retrieved documents with relevance scores.</returns>
    Task<RetrievalResult> RetrieveRelevantDocumentsAsync(
        ReadOnlyMemory<float> queryEmbedding,
        RetrievalOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves documents and follows their links to related documents.
    /// </summary>
    /// <param name="queryEmbedding">The embedding vector for the query.</param>
    /// <param name="options">Retrieval options including link depth.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Retrieved documents plus linked documents.</returns>
    Task<RetrievalResultWithLinks> RetrieveWithLinkedDocsAsync(
        ReadOnlyMemory<float> queryEmbedding,
        RetrievalOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Assembles complete RAG context including critical docs, relevant docs, and linked docs.
    /// </summary>
    /// <param name="queryEmbedding">The embedding vector for the query.</param>
    /// <param name="options">Retrieval options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Assembled context ready for RAG generation.</returns>
    Task<RAGContext> AssembleRAGContextAsync(
        ReadOnlyMemory<float> queryEmbedding,
        RetrievalOptions options,
        CancellationToken cancellationToken = default);
}
```

### RetrievalOptions Class

```csharp
namespace CompoundDocs.McpServer.Models;

/// <summary>
/// Options for RAG document retrieval.
/// </summary>
public record RetrievalOptions
{
    /// <summary>
    /// Minimum relevance score (cosine similarity) for inclusion.
    /// Default: 0.7 for RAG queries.
    /// </summary>
    public float MinRelevanceScore { get; init; } = 0.7f;

    /// <summary>
    /// Maximum number of direct results (excluding linked docs).
    /// Default: 3.
    /// </summary>
    public int MaxResults { get; init; } = 3;

    /// <summary>
    /// Maximum number of linked documents to include.
    /// Default: 5.
    /// </summary>
    public int MaxLinkedDocs { get; init; } = 5;

    /// <summary>
    /// Maximum depth to follow document links.
    /// Default: 2 (immediate links + their links).
    /// </summary>
    public int MaxLinkDepth { get; init; } = 2;

    /// <summary>
    /// Whether to include critical docs regardless of relevance.
    /// Default: true.
    /// </summary>
    public bool IncludeCritical { get; init; } = true;

    /// <summary>
    /// Minimum promotion level filter.
    /// Default: standard (include all levels).
    /// </summary>
    public PromotionLevel MinPromotionLevel { get; init; } = PromotionLevel.Standard;

    /// <summary>
    /// Filter to specific doc-types. Null means all types.
    /// </summary>
    public IReadOnlyList<string>? DocTypes { get; init; }

    /// <summary>
    /// Whether to apply relevance boosting based on promotion level.
    /// Default: true.
    /// </summary>
    public bool ApplyRelevanceBoosting { get; init; } = true;
}

/// <summary>
/// Document promotion levels for visibility and boosting.
/// </summary>
public enum PromotionLevel
{
    Standard = 0,
    Important = 1,
    Critical = 2
}
```

### RetrievalResult Types

```csharp
namespace CompoundDocs.McpServer.Models;

/// <summary>
/// Result of document retrieval with relevance scores.
/// </summary>
public record RetrievalResult
{
    /// <summary>
    /// Retrieved documents ordered by boosted relevance score.
    /// </summary>
    public required IReadOnlyList<RetrievedDocument> Documents { get; init; }

    /// <summary>
    /// Total number of documents that matched before limit was applied.
    /// </summary>
    public int TotalMatches { get; init; }
}

/// <summary>
/// A document retrieved for RAG context.
/// </summary>
public record RetrievedDocument
{
    /// <summary>
    /// Relative path to the document within csharp-compounding-docs/.
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// Document title from frontmatter.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Document summary if available.
    /// </summary>
    public string? Summary { get; init; }

    /// <summary>
    /// Document content for RAG context.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Character count of the content.
    /// </summary>
    public int CharCount { get; init; }

    /// <summary>
    /// Raw relevance score from vector search (0.0-1.0).
    /// </summary>
    public float RawRelevanceScore { get; init; }

    /// <summary>
    /// Relevance score after promotion-based boosting.
    /// </summary>
    public float BoostedRelevanceScore { get; init; }

    /// <summary>
    /// Document type (problem, insight, codebase, etc.).
    /// </summary>
    public required string DocType { get; init; }

    /// <summary>
    /// Promotion level of the document.
    /// </summary>
    public PromotionLevel PromotionLevel { get; init; }

    /// <summary>
    /// Document date from frontmatter.
    /// </summary>
    public DateOnly? Date { get; init; }
}

/// <summary>
/// Result including linked documents.
/// </summary>
public record RetrievalResultWithLinks : RetrievalResult
{
    /// <summary>
    /// Documents linked from the retrieved documents.
    /// </summary>
    public required IReadOnlyList<LinkedDocument> LinkedDocuments { get; init; }
}

/// <summary>
/// A document that was linked from a retrieved document.
/// </summary>
public record LinkedDocument : RetrievedDocument
{
    /// <summary>
    /// Path of the document that linked to this one.
    /// </summary>
    public required string LinkedFrom { get; init; }

    /// <summary>
    /// Depth at which this link was discovered (1 = direct link).
    /// </summary>
    public int LinkDepth { get; init; }
}
```

### RAGContext Assembly

```csharp
namespace CompoundDocs.McpServer.Models;

/// <summary>
/// Assembled context for RAG generation.
/// </summary>
public record RAGContext
{
    /// <summary>
    /// Critical documents (prepended regardless of relevance).
    /// </summary>
    public required IReadOnlyList<RetrievedDocument> CriticalDocuments { get; init; }

    /// <summary>
    /// Documents matching the query by relevance.
    /// </summary>
    public required IReadOnlyList<RetrievedDocument> RelevantDocuments { get; init; }

    /// <summary>
    /// Documents linked from relevant documents.
    /// </summary>
    public required IReadOnlyList<LinkedDocument> LinkedDocuments { get; init; }

    /// <summary>
    /// Total character count of all context documents.
    /// </summary>
    public int TotalCharCount { get; init; }

    /// <summary>
    /// Formatted context string for RAG prompt.
    /// </summary>
    public required string FormattedContext { get; init; }

    /// <summary>
    /// All source documents for attribution.
    /// </summary>
    public IEnumerable<RetrievedDocument> AllSources =>
        CriticalDocuments
            .Concat(RelevantDocuments)
            .Concat(LinkedDocuments)
            .DistinctBy(d => d.Path);
}
```

### RAGRetrievalService Implementation

```csharp
namespace CompoundDocs.McpServer.Services;

public sealed class RAGRetrievalService : IRAGRetrievalService
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IDocumentLinkResolver _linkResolver;
    private readonly IRelevanceBooster _relevanceBooster;
    private readonly IRAGContextAssembler _contextAssembler;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<RAGRetrievalService> _logger;

    public RAGRetrievalService(
        IDocumentRepository documentRepository,
        IDocumentLinkResolver linkResolver,
        IRelevanceBooster relevanceBooster,
        IRAGContextAssembler contextAssembler,
        ITenantContext tenantContext,
        ILogger<RAGRetrievalService> logger)
    {
        _documentRepository = documentRepository;
        _linkResolver = linkResolver;
        _relevanceBooster = relevanceBooster;
        _contextAssembler = contextAssembler;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<RetrievalResult> RetrieveRelevantDocumentsAsync(
        ReadOnlyMemory<float> queryEmbedding,
        RetrievalOptions options,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Retrieving documents with min_relevance={MinRelevance}, max_results={MaxResults}",
            options.MinRelevanceScore,
            options.MaxResults);

        // Search vector store with tenant filter
        var searchResults = await _documentRepository.SearchAsync(
            queryEmbedding,
            options.MaxResults * 2, // Over-fetch for post-filtering
            BuildTenantFilter(options),
            cancellationToken);

        // Filter by relevance threshold
        var filteredResults = searchResults
            .Where(r => r.Score >= options.MinRelevanceScore)
            .ToList();

        // Apply relevance boosting
        var boostedResults = options.ApplyRelevanceBoosting
            ? _relevanceBooster.ApplyBoosts(filteredResults)
            : filteredResults.Select(r => (r.Document, r.Score, r.Score)).ToList();

        // Order by boosted score and take top N
        var topResults = boostedResults
            .OrderByDescending(r => r.BoostedScore)
            .Take(options.MaxResults)
            .ToList();

        var documents = topResults.Select(r => MapToRetrievedDocument(r)).ToList();

        _logger.LogInformation(
            "Retrieved {Count} documents from {TotalMatches} matches",
            documents.Count,
            filteredResults.Count);

        return new RetrievalResult
        {
            Documents = documents,
            TotalMatches = filteredResults.Count
        };
    }

    public async Task<RetrievalResultWithLinks> RetrieveWithLinkedDocsAsync(
        ReadOnlyMemory<float> queryEmbedding,
        RetrievalOptions options,
        CancellationToken cancellationToken = default)
    {
        // Get direct matches first
        var directResult = await RetrieveRelevantDocumentsAsync(
            queryEmbedding, options, cancellationToken);

        if (options.MaxLinkDepth <= 0 || options.MaxLinkedDocs <= 0)
        {
            return new RetrievalResultWithLinks
            {
                Documents = directResult.Documents,
                TotalMatches = directResult.TotalMatches,
                LinkedDocuments = []
            };
        }

        // Resolve linked documents with cycle detection
        var linkedDocs = await _linkResolver.ResolveLinksAsync(
            directResult.Documents,
            options.MaxLinkDepth,
            options.MaxLinkedDocs,
            cancellationToken);

        // Deduplicate against direct matches
        var directPaths = directResult.Documents.Select(d => d.Path).ToHashSet();
        var uniqueLinkedDocs = linkedDocs
            .Where(d => !directPaths.Contains(d.Path))
            .ToList();

        _logger.LogInformation(
            "Resolved {LinkedCount} unique linked documents",
            uniqueLinkedDocs.Count);

        return new RetrievalResultWithLinks
        {
            Documents = directResult.Documents,
            TotalMatches = directResult.TotalMatches,
            LinkedDocuments = uniqueLinkedDocs
        };
    }

    public async Task<RAGContext> AssembleRAGContextAsync(
        ReadOnlyMemory<float> queryEmbedding,
        RetrievalOptions options,
        CancellationToken cancellationToken = default)
    {
        // Get critical documents first (if enabled)
        IReadOnlyList<RetrievedDocument> criticalDocs = [];
        if (options.IncludeCritical)
        {
            criticalDocs = await RetrieveCriticalDocumentsAsync(
                options.DocTypes, cancellationToken);
        }

        // Get relevant documents with links
        var retrievalResult = await RetrieveWithLinkedDocsAsync(
            queryEmbedding, options, cancellationToken);

        // Deduplicate critical docs from relevant docs
        var criticalPaths = criticalDocs.Select(d => d.Path).ToHashSet();
        var relevantDocs = retrievalResult.Documents
            .Where(d => !criticalPaths.Contains(d.Path))
            .ToList();

        // Assemble formatted context
        var context = _contextAssembler.AssembleContext(
            criticalDocs,
            relevantDocs,
            retrievalResult.LinkedDocuments);

        return context;
    }

    private async Task<IReadOnlyList<RetrievedDocument>> RetrieveCriticalDocumentsAsync(
        IReadOnlyList<string>? docTypes,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug("Retrieving critical documents");

        var criticalDocs = await _documentRepository.GetByPromotionLevelAsync(
            PromotionLevel.Critical,
            docTypes,
            cancellationToken);

        _logger.LogInformation(
            "Retrieved {Count} critical documents",
            criticalDocs.Count);

        return criticalDocs.Select(d => MapToRetrievedDocument(d, 1.0f)).ToList();
    }

    private VectorSearchFilter BuildTenantFilter(RetrievalOptions options)
    {
        var filter = new VectorSearchFilter()
            .EqualTo("project_name", _tenantContext.ProjectName)
            .EqualTo("branch_name", _tenantContext.BranchName)
            .EqualTo("path_hash", _tenantContext.PathHash);

        // Add promotion level filter if specified
        if (options.MinPromotionLevel > PromotionLevel.Standard)
        {
            filter = filter.GreaterThanOrEqualTo(
                "promotion_level",
                options.MinPromotionLevel.ToString().ToLowerInvariant());
        }

        // Add doc-type filter if specified
        if (options.DocTypes is { Count: > 0 })
        {
            filter = filter.In("doc_type", options.DocTypes);
        }

        return filter;
    }

    private static RetrievedDocument MapToRetrievedDocument(
        (CompoundDocument Document, float RawScore, float BoostedScore) result)
    {
        return new RetrievedDocument
        {
            Path = result.Document.RelativePath,
            Title = result.Document.Title,
            Summary = result.Document.Summary,
            Content = result.Document.Content,
            CharCount = result.Document.CharCount,
            RawRelevanceScore = result.RawScore,
            BoostedRelevanceScore = result.BoostedScore,
            DocType = result.Document.DocType,
            PromotionLevel = Enum.Parse<PromotionLevel>(result.Document.PromotionLevel, ignoreCase: true),
            Date = result.Document.Date
        };
    }
}
```

### IRelevanceBooster Interface and Implementation

```csharp
namespace CompoundDocs.McpServer.Abstractions;

/// <summary>
/// Applies relevance score boosting based on document promotion level.
/// </summary>
public interface IRelevanceBooster
{
    /// <summary>
    /// Applies promotion-based boosts to search results.
    /// </summary>
    IReadOnlyList<(CompoundDocument Document, float RawScore, float BoostedScore)> ApplyBoosts(
        IReadOnlyList<(CompoundDocument Document, float Score)> results);
}

namespace CompoundDocs.McpServer.Services;

public sealed class RelevanceBooster : IRelevanceBooster
{
    private readonly RelevanceBoostOptions _options;

    public RelevanceBooster(IOptions<RelevanceBoostOptions> options)
    {
        _options = options.Value;
    }

    public IReadOnlyList<(CompoundDocument Document, float RawScore, float BoostedScore)> ApplyBoosts(
        IReadOnlyList<(CompoundDocument Document, float Score)> results)
    {
        return results.Select(r =>
        {
            var boost = GetBoostFactor(r.Document.PromotionLevel);
            var boostedScore = Math.Min(1.0f, r.Score + boost);
            return (r.Document, r.Score, boostedScore);
        }).ToList();
    }

    private float GetBoostFactor(string promotionLevel)
    {
        return promotionLevel.ToLowerInvariant() switch
        {
            "critical" => _options.CriticalBoost,
            "important" => _options.ImportantBoost,
            _ => 0f
        };
    }
}

/// <summary>
/// Options for relevance boosting.
/// </summary>
public class RelevanceBoostOptions
{
    public const string SectionName = "Retrieval:Boosting";

    /// <summary>
    /// Boost factor for critical documents. Default: 0.15
    /// </summary>
    public float CriticalBoost { get; set; } = 0.15f;

    /// <summary>
    /// Boost factor for important documents. Default: 0.10
    /// </summary>
    public float ImportantBoost { get; set; } = 0.10f;
}
```

### IDocumentLinkResolver Interface and Implementation

```csharp
namespace CompoundDocs.McpServer.Abstractions;

/// <summary>
/// Resolves document links for related document discovery.
/// </summary>
public interface IDocumentLinkResolver
{
    /// <summary>
    /// Extracts document links from content.
    /// </summary>
    IReadOnlyList<string> ExtractLinks(string markdownContent);

    /// <summary>
    /// Resolves linked documents recursively with cycle detection.
    /// </summary>
    Task<IReadOnlyList<LinkedDocument>> ResolveLinksAsync(
        IReadOnlyList<RetrievedDocument> sourceDocuments,
        int maxDepth,
        int maxLinkedDocs,
        CancellationToken cancellationToken = default);
}

namespace CompoundDocs.McpServer.Services;

public sealed class DocumentLinkResolver : IDocumentLinkResolver
{
    private readonly IDocumentRepository _documentRepository;
    private readonly IMarkdownParser _markdownParser;
    private readonly ILogger<DocumentLinkResolver> _logger;

    // Regex for relative markdown links to compounding docs
    private static readonly Regex LinkPattern = new(
        @"\[(?<text>[^\]]*)\]\((?<path>\.?\.?\/csharp-compounding-docs\/[^\)]+)\)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public DocumentLinkResolver(
        IDocumentRepository documentRepository,
        IMarkdownParser markdownParser,
        ILogger<DocumentLinkResolver> logger)
    {
        _documentRepository = documentRepository;
        _markdownParser = markdownParser;
        _logger = logger;
    }

    public IReadOnlyList<string> ExtractLinks(string markdownContent)
    {
        var links = new List<string>();

        foreach (Match match in LinkPattern.Matches(markdownContent))
        {
            var path = match.Groups["path"].Value;
            // Normalize to relative path within csharp-compounding-docs/
            var normalizedPath = NormalizePath(path);
            if (!string.IsNullOrEmpty(normalizedPath))
            {
                links.Add(normalizedPath);
            }
        }

        return links.Distinct().ToList();
    }

    public async Task<IReadOnlyList<LinkedDocument>> ResolveLinksAsync(
        IReadOnlyList<RetrievedDocument> sourceDocuments,
        int maxDepth,
        int maxLinkedDocs,
        CancellationToken cancellationToken = default)
    {
        var visited = sourceDocuments.Select(d => d.Path).ToHashSet();
        var linkedDocs = new List<LinkedDocument>();
        var currentLevel = sourceDocuments.ToList();

        for (int depth = 1; depth <= maxDepth && linkedDocs.Count < maxLinkedDocs; depth++)
        {
            var nextLevel = new List<(string Path, string LinkedFrom)>();

            foreach (var doc in currentLevel)
            {
                var links = ExtractLinks(doc.Content);

                foreach (var link in links)
                {
                    if (!visited.Contains(link))
                    {
                        visited.Add(link);
                        nextLevel.Add((link, doc.Path));
                    }
                }
            }

            if (nextLevel.Count == 0) break;

            // Fetch linked documents
            var linkedDocContents = await _documentRepository.GetByPathsAsync(
                nextLevel.Select(l => l.Path).Take(maxLinkedDocs - linkedDocs.Count),
                cancellationToken);

            foreach (var linked in linkedDocContents)
            {
                var linkInfo = nextLevel.First(l => l.Path == linked.RelativePath);
                linkedDocs.Add(new LinkedDocument
                {
                    Path = linked.RelativePath,
                    Title = linked.Title,
                    Summary = linked.Summary,
                    Content = linked.Content,
                    CharCount = linked.CharCount,
                    RawRelevanceScore = 0, // Linked docs don't have direct relevance
                    BoostedRelevanceScore = 0,
                    DocType = linked.DocType,
                    PromotionLevel = Enum.Parse<PromotionLevel>(linked.PromotionLevel, ignoreCase: true),
                    Date = linked.Date,
                    LinkedFrom = linkInfo.LinkedFrom,
                    LinkDepth = depth
                });

                if (linkedDocs.Count >= maxLinkedDocs) break;
            }

            // Prepare for next iteration
            currentLevel = linkedDocs
                .Where(d => d.LinkDepth == depth)
                .Cast<RetrievedDocument>()
                .ToList();
        }

        _logger.LogDebug(
            "Resolved {Count} linked documents up to depth {MaxDepth}",
            linkedDocs.Count, maxDepth);

        return linkedDocs;
    }

    private static string NormalizePath(string path)
    {
        // Remove leading ./ or ../
        var normalized = path
            .Replace("\\", "/")
            .TrimStart('.', '/');

        // Ensure it starts with csharp-compounding-docs/
        if (normalized.StartsWith("csharp-compounding-docs/"))
        {
            return normalized["csharp-compounding-docs/".Length..];
        }

        return string.Empty;
    }
}
```

### IRAGContextAssembler Implementation

```csharp
namespace CompoundDocs.McpServer.Services;

public sealed class RAGContextAssembler : IRAGContextAssembler
{
    public RAGContext AssembleContext(
        IReadOnlyList<RetrievedDocument> criticalDocs,
        IReadOnlyList<RetrievedDocument> relevantDocs,
        IReadOnlyList<LinkedDocument> linkedDocs)
    {
        var sb = new StringBuilder();
        var totalCharCount = 0;

        // Critical documents first
        if (criticalDocs.Count > 0)
        {
            sb.AppendLine("## Critical Context (Always Relevant)");
            sb.AppendLine();
            foreach (var doc in criticalDocs)
            {
                AppendDocument(sb, doc, ref totalCharCount);
            }
        }

        // Relevant documents
        if (relevantDocs.Count > 0)
        {
            sb.AppendLine("## Retrieved Context (By Relevance)");
            sb.AppendLine();
            foreach (var doc in relevantDocs.OrderByDescending(d => d.BoostedRelevanceScore))
            {
                AppendDocument(sb, doc, ref totalCharCount);
            }
        }

        // Linked documents
        if (linkedDocs.Count > 0)
        {
            sb.AppendLine("## Related Context (Linked Documents)");
            sb.AppendLine();
            foreach (var doc in linkedDocs.OrderBy(d => d.LinkDepth))
            {
                AppendDocument(sb, doc, ref totalCharCount, doc.LinkedFrom);
            }
        }

        return new RAGContext
        {
            CriticalDocuments = criticalDocs,
            RelevantDocuments = relevantDocs,
            LinkedDocuments = linkedDocs,
            TotalCharCount = totalCharCount,
            FormattedContext = sb.ToString()
        };
    }

    private static void AppendDocument(
        StringBuilder sb,
        RetrievedDocument doc,
        ref int totalCharCount,
        string? linkedFrom = null)
    {
        sb.AppendLine($"### {doc.Title}");
        sb.AppendLine($"- **Path**: {doc.Path}");
        sb.AppendLine($"- **Type**: {doc.DocType}");

        if (doc.BoostedRelevanceScore > 0)
        {
            sb.AppendLine($"- **Relevance**: {doc.BoostedRelevanceScore:F2}");
        }

        if (!string.IsNullOrEmpty(linkedFrom))
        {
            sb.AppendLine($"- **Linked from**: {linkedFrom}");
        }

        sb.AppendLine();
        sb.AppendLine(doc.Content);
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        totalCharCount += doc.CharCount;
    }
}
```

### Service Registration

```csharp
public static class RAGRetrievalServiceCollectionExtensions
{
    public static IServiceCollection AddRAGRetrievalServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Options
        services.AddOptions<RelevanceBoostOptions>()
            .Bind(configuration.GetSection(RelevanceBoostOptions.SectionName))
            .ValidateDataAnnotations();

        // Core services
        services.AddScoped<IRAGRetrievalService, RAGRetrievalService>();
        services.AddSingleton<IRelevanceBooster, RelevanceBooster>();
        services.AddSingleton<IDocumentLinkResolver, DocumentLinkResolver>();
        services.AddSingleton<IRAGContextAssembler, RAGContextAssembler>();

        return services;
    }
}
```

---

## Dependencies

### Depends On

- **Phase 050**: Vector Store Integration - Provides `IDocumentRepository` with vector search
- **Phase 032**: Document Repository - Base repository patterns
- **Phase 038**: Multi-Tenant Context Service - `ITenantContext` for isolation
- **Phase 015**: Markdown Parser - Markdig for link extraction
- **Phase 029**: Embedding Service - Query embedding generation

### Blocks

- **Phase 052+**: RAG Generation Service - Consumes assembled context
- **MCP `rag_query` Tool**: Direct consumer of `IRAGRetrievalService`

---

## Verification Steps

After completing this phase, verify:

1. **Relevance filtering**: Documents below threshold are excluded
2. **Promotion boosting**: Critical/important docs score higher
3. **Critical injection**: Critical docs always included (when enabled)
4. **Link following**: Links resolved up to max depth with cycle detection
5. **Tenant isolation**: Only documents from current tenant returned
6. **Context assembly**: Formatted context includes all sources with attribution

### Unit Tests

```csharp
[Fact]
public async Task RetrieveRelevantDocuments_FiltersbyRelevanceThreshold()
{
    // Arrange
    var mockRepo = new Mock<IDocumentRepository>();
    mockRepo.Setup(r => r.SearchAsync(It.IsAny<ReadOnlyMemory<float>>(), It.IsAny<int>(), It.IsAny<VectorSearchFilter>(), default))
        .ReturnsAsync([
            (CreateDoc("high-match"), 0.85f),
            (CreateDoc("medium-match"), 0.72f),
            (CreateDoc("low-match"), 0.45f)
        ]);

    var service = CreateService(mockRepo.Object);
    var options = new RetrievalOptions { MinRelevanceScore = 0.7f };

    // Act
    var result = await service.RetrieveRelevantDocumentsAsync(
        new float[1024], options);

    // Assert
    Assert.Equal(2, result.Documents.Count);
    Assert.All(result.Documents, d => Assert.True(d.RawRelevanceScore >= 0.7f));
}

[Fact]
public void RelevanceBooster_AppliesCorrectBoosts()
{
    // Arrange
    var options = Options.Create(new RelevanceBoostOptions
    {
        CriticalBoost = 0.15f,
        ImportantBoost = 0.10f
    });
    var booster = new RelevanceBooster(options);

    var results = new List<(CompoundDocument, float)>
    {
        (CreateDoc(promotionLevel: "critical"), 0.70f),
        (CreateDoc(promotionLevel: "important"), 0.75f),
        (CreateDoc(promotionLevel: "standard"), 0.80f)
    };

    // Act
    var boosted = booster.ApplyBoosts(results);

    // Assert
    Assert.Equal(0.85f, boosted[0].BoostedScore); // 0.70 + 0.15
    Assert.Equal(0.85f, boosted[1].BoostedScore); // 0.75 + 0.10
    Assert.Equal(0.80f, boosted[2].BoostedScore); // no boost
}

[Fact]
public void DocumentLinkResolver_ExtractsRelativeLinks()
{
    // Arrange
    var resolver = new DocumentLinkResolver(
        Mock.Of<IDocumentRepository>(),
        Mock.Of<IMarkdownParser>(),
        Mock.Of<ILogger<DocumentLinkResolver>>());

    var content = """
        See [related problem](./csharp-compounding-docs/problems/db-issue.md) for context.
        Also check [tooling](../csharp-compounding-docs/tools/npgsql.md).
        External [link](https://example.com) should be ignored.
        """;

    // Act
    var links = resolver.ExtractLinks(content);

    // Assert
    Assert.Equal(2, links.Count);
    Assert.Contains("problems/db-issue.md", links);
    Assert.Contains("tools/npgsql.md", links);
}

[Fact]
public async Task ResolveLinks_DetectsCycles()
{
    // Arrange - A links to B, B links to A
    var docA = CreateRetrievedDoc("a.md", "[B](./csharp-compounding-docs/b.md)");
    var docB = CreateDoc("b.md", "[A](./csharp-compounding-docs/a.md)");

    var mockRepo = new Mock<IDocumentRepository>();
    mockRepo.Setup(r => r.GetByPathsAsync(It.IsAny<IEnumerable<string>>(), default))
        .ReturnsAsync([docB]);

    var resolver = new DocumentLinkResolver(
        mockRepo.Object,
        Mock.Of<IMarkdownParser>(),
        Mock.Of<ILogger<DocumentLinkResolver>>());

    // Act
    var linked = await resolver.ResolveLinksAsync([docA], maxDepth: 5, maxLinkedDocs: 10);

    // Assert - Should not infinite loop, should only have B once
    Assert.Single(linked);
    Assert.Equal("b.md", linked[0].Path);
}
```

### Integration Tests

```csharp
[Trait("Category", "Integration")]
public class RAGRetrievalIntegrationTests : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task AssembleRAGContext_FullPipeline()
    {
        // Arrange - seed test documents
        await SeedTestDocuments();

        var embeddingService = _fixture.GetService<IEmbeddingService>();
        var retrievalService = _fixture.GetService<IRAGRetrievalService>();

        var queryEmbedding = await embeddingService.GenerateEmbeddingAsync(
            "database connection pool exhaustion");

        // Act
        var context = await retrievalService.AssembleRAGContextAsync(
            queryEmbedding,
            new RetrievalOptions
            {
                MinRelevanceScore = 0.5f,
                MaxResults = 3,
                MaxLinkedDocs = 5,
                IncludeCritical = true
            });

        // Assert
        Assert.NotEmpty(context.FormattedContext);
        Assert.True(context.TotalCharCount > 0);
        Assert.All(context.AllSources, s => Assert.NotNull(s.Path));
    }
}
```

---

## Files Created/Modified

| File | Action | Description |
|------|--------|-------------|
| `src/CompoundDocs.McpServer/Abstractions/IRAGRetrievalService.cs` | Create | Main retrieval interface |
| `src/CompoundDocs.McpServer/Abstractions/IRelevanceBooster.cs` | Create | Boosting interface |
| `src/CompoundDocs.McpServer/Abstractions/IDocumentLinkResolver.cs` | Create | Link resolution interface |
| `src/CompoundDocs.McpServer/Abstractions/IRAGContextAssembler.cs` | Create | Context assembly interface |
| `src/CompoundDocs.McpServer/Models/RetrievalOptions.cs` | Create | Options record |
| `src/CompoundDocs.McpServer/Models/RetrievalResult.cs` | Create | Result records |
| `src/CompoundDocs.McpServer/Models/RAGContext.cs` | Create | Context record |
| `src/CompoundDocs.McpServer/Options/RelevanceBoostOptions.cs` | Create | Boost configuration |
| `src/CompoundDocs.McpServer/Services/RAGRetrievalService.cs` | Create | Main implementation |
| `src/CompoundDocs.McpServer/Services/RelevanceBooster.cs` | Create | Boost implementation |
| `src/CompoundDocs.McpServer/Services/DocumentLinkResolver.cs` | Create | Link resolver |
| `src/CompoundDocs.McpServer/Services/RAGContextAssembler.cs` | Create | Context assembler |
| `src/CompoundDocs.McpServer/Extensions/RAGRetrievalServiceCollectionExtensions.cs` | Create | DI registration |
| `tests/CompoundDocs.Tests/Services/RAGRetrievalServiceTests.cs` | Create | Unit tests |
| `tests/CompoundDocs.Tests/Services/RelevanceBoosterTests.cs` | Create | Boost tests |
| `tests/CompoundDocs.Tests/Services/DocumentLinkResolverTests.cs` | Create | Link tests |
| `tests/CompoundDocs.IntegrationTests/RAGRetrievalIntegrationTests.cs` | Create | Integration tests |

---

## Configuration Reference

### appsettings.json

```json
{
  "Retrieval": {
    "MinRelevanceScore": 0.7,
    "MaxResults": 3,
    "MaxLinkedDocs": 5,
    "Boosting": {
      "CriticalBoost": 0.15,
      "ImportantBoost": 0.10
    }
  },
  "LinkResolution": {
    "MaxDepth": 2
  }
}
```

### Project Config Override

```json
{
  "project_name": "my-app",
  "retrieval": {
    "min_relevance_score": 0.8,
    "max_results": 5,
    "max_linked_docs": 3
  },
  "link_resolution": {
    "max_depth": 1
  }
}
```

---

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| Link cycles causing infinite recursion | Visited set tracks all processed paths |
| Too many linked docs bloating context | `MaxLinkedDocs` hard limit (default: 5) |
| Slow link resolution for deep graphs | Depth limit (default: 2) and early termination |
| Relevance threshold too aggressive | Configurable with sensible default (0.7) |
| Critical docs overwhelming context | Deduplicated against relevant results |
| Chunked documents incomplete | Aggregate chunks to parent document for context |

---

## Notes

- The `rag_query` MCP tool will consume `IRAGRetrievalService.AssembleRAGContextAsync()` directly
- Link resolution uses markdown link patterns, not frontmatter-based relationships
- Relevance boosting is subtle (0.10-0.15) to preserve organic ranking while surfacing promoted docs
- Context assembly preserves source attribution for the RAG response
- Chunk support requires the document repository to provide content aggregation
