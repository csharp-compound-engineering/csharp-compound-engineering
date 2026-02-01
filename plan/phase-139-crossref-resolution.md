# Phase 139: Cross-Reference Resolution in RAG

> **Status**: NOT_STARTED
> **Effort Estimate**: 8-10 hours
> **Category**: Document Processing
> **Prerequisites**: Phase 067 (Link Depth Following for RAG), Phase 051 (RAG Retrieval Service)

---

## Spec References

This phase implements cross-reference resolution during RAG retrieval as defined in:

- **spec/skills/skill-patterns.md** - [Cross-References in Skills](../spec/skills/skill-patterns.md#cross-references-in-skills) (lines 129-156)
- **spec/mcp-server.md** - [Link Resolution](../spec/mcp-server.md#link-resolution) (lines 251-292)
- **spec/mcp-server/tools.md** - [RAG Query Tool](../spec/mcp-server/tools.md#1-rag-query-tool) response with `linked_docs` field

---

## Objectives

1. Implement markdown link resolution during RAG document retrieval
2. Build `related_docs` frontmatter field following for document relationships
3. Create link content inclusion strategy for RAG context building
4. Implement configurable depth limiting for link following traversal
5. Create link resolution caching layer for performance optimization
6. Handle resolution errors gracefully (missing docs, circular refs, broken links)

---

## Acceptance Criteria

### Markdown Link Resolution

- [ ] `IMarkdownLinkExtractor` interface defined for extracting links from markdown content
- [ ] Markdig AST traversal extracts all relative markdown links (`[text](../path/doc.md)`)
- [ ] Links normalized to relative paths within `csharp-compounding-docs/`
- [ ] External links (http/https) filtered out from resolution
- [ ] Anchor links (`#section`) within same document handled correctly
- [ ] Cross-document anchor links (`../doc.md#section`) resolve to document path

### Related Docs Field Following

- [ ] `IRelatedDocsResolver` interface for frontmatter `related_docs` field extraction
- [ ] YAML frontmatter parsing extracts `related_docs` array field
- [ ] Related docs validated against existing document paths
- [ ] Related docs contribute to link graph alongside markdown links
- [ ] Related docs field format: `["problems/issue-1.md", "insights/pattern.md"]`
- [ ] Missing related docs logged as warnings, not errors

### Link Content Inclusion in Context

- [ ] `ILinkContentInclusionStrategy` interface for determining what content to include
- [ ] Full document content included by default for depth-1 links
- [ ] Summary-only mode available for depth-2+ links (configurable)
- [ ] Content truncation for very large linked documents (>10000 chars)
- [ ] Linked document metadata always included (path, title, char_count, linked_from)
- [ ] Token budget awareness for context window management

### Depth Limiting for Link Following

- [ ] `max_depth` configuration from project config (`link_resolution.max_depth`, default: 2)
- [ ] `max_linked_docs` configuration respected (`link_resolution.max_linked_docs`, default: 5)
- [ ] Depth 0 disables all link following
- [ ] Depth 1 follows direct links only
- [ ] Depth 2 follows links of linked documents (default behavior)
- [ ] Tool parameter `link_depth` can override project config per-request
- [ ] Early termination when `max_linked_docs` limit reached at any depth

### Link Resolution Caching

- [ ] `ILinkResolutionCache` interface for caching resolved link metadata
- [ ] In-memory cache using `IMemoryCache` from Microsoft.Extensions.Caching.Memory
- [ ] Cache key: document path + content hash (invalidates on document change)
- [ ] Cache stores: extracted links, related_docs field, link targets existence
- [ ] Cache TTL configurable (default: 5 minutes for session-scoped caching)
- [ ] Cache invalidation on file watcher document change events
- [ ] Cache statistics exposed for monitoring (hit rate, size, evictions)

### Error Handling

- [ ] Missing linked documents return `null` content with path in `linked_docs` response
- [ ] Circular references detected and broken (visited set tracking)
- [ ] Broken links (non-existent targets) logged and skipped gracefully
- [ ] Malformed markdown links logged with document path for debugging
- [ ] Resolution timeout handling for pathological link graphs (configurable, default: 5s)

---

## Implementation Notes

### IMarkdownLinkExtractor Interface

Create `Services/IMarkdownLinkExtractor.cs`:

```csharp
namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Extracts markdown links from document content using Markdig AST parsing.
/// </summary>
public interface IMarkdownLinkExtractor
{
    /// <summary>
    /// Extracts all relative markdown links from document content.
    /// </summary>
    /// <param name="markdownContent">The raw markdown content.</param>
    /// <param name="sourceDocumentPath">Path of the source document (for relative path resolution).</param>
    /// <returns>List of normalized relative paths to linked documents.</returns>
    IReadOnlyList<ExtractedLink> ExtractLinks(string markdownContent, string sourceDocumentPath);

    /// <summary>
    /// Validates if a link target exists in the document collection.
    /// </summary>
    /// <param name="targetPath">The normalized target path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the target document exists.</returns>
    Task<bool> ValidateLinkTargetAsync(string targetPath, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents an extracted link from markdown content.
/// </summary>
public record ExtractedLink(
    /// <summary>Normalized relative path to the target document.</summary>
    string TargetPath,
    /// <summary>Display text of the link.</summary>
    string LinkText,
    /// <summary>Original link URL as it appeared in the markdown.</summary>
    string OriginalUrl,
    /// <summary>Optional anchor within the target document.</summary>
    string? Anchor,
    /// <summary>Line number where the link was found (for debugging).</summary>
    int LineNumber);
```

### MarkdigLinkExtractor Implementation

Create `Services/MarkdigLinkExtractor.cs`:

```csharp
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Microsoft.Extensions.Logging;

namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Markdig-based implementation for extracting links from markdown documents.
/// </summary>
public sealed class MarkdigLinkExtractor : IMarkdownLinkExtractor
{
    private readonly IDocumentRepository _documentRepository;
    private readonly ILogger<MarkdigLinkExtractor> _logger;
    private readonly MarkdownPipeline _pipeline;

    // Pattern to identify links to compounding docs
    private const string CompoundingDocsPrefix = "csharp-compounding-docs/";

    public MarkdigLinkExtractor(
        IDocumentRepository documentRepository,
        ILogger<MarkdigLinkExtractor> logger)
    {
        _documentRepository = documentRepository;
        _logger = logger;
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .Build();
    }

    public IReadOnlyList<ExtractedLink> ExtractLinks(string markdownContent, string sourceDocumentPath)
    {
        if (string.IsNullOrWhiteSpace(markdownContent))
        {
            return Array.Empty<ExtractedLink>();
        }

        var document = Markdown.Parse(markdownContent, _pipeline);
        var links = new List<ExtractedLink>();
        var sourceDirectory = Path.GetDirectoryName(sourceDocumentPath) ?? string.Empty;

        foreach (var linkInline in document.Descendants<LinkInline>())
        {
            if (string.IsNullOrEmpty(linkInline.Url))
            {
                continue;
            }

            var url = linkInline.Url;

            // Skip external links
            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Skip pure anchor links within same document
            if (url.StartsWith("#"))
            {
                continue;
            }

            // Extract anchor if present
            string? anchor = null;
            var anchorIndex = url.IndexOf('#');
            if (anchorIndex >= 0)
            {
                anchor = url[(anchorIndex + 1)..];
                url = url[..anchorIndex];
            }

            // Resolve relative path
            var resolvedPath = ResolveRelativePath(url, sourceDirectory);
            if (string.IsNullOrEmpty(resolvedPath))
            {
                _logger.LogDebug(
                    "Skipping link '{Url}' in {Source} - not a compounding docs link",
                    linkInline.Url, sourceDocumentPath);
                continue;
            }

            // Get link text
            var linkText = GetLinkText(linkInline);

            // Get line number for debugging
            var lineNumber = linkInline.Line + 1; // Markdig uses 0-based

            links.Add(new ExtractedLink(
                TargetPath: resolvedPath,
                LinkText: linkText,
                OriginalUrl: linkInline.Url,
                Anchor: anchor,
                LineNumber: lineNumber));
        }

        _logger.LogDebug(
            "Extracted {Count} internal links from {Source}",
            links.Count, sourceDocumentPath);

        return links.DistinctBy(l => l.TargetPath).ToList();
    }

    public async Task<bool> ValidateLinkTargetAsync(
        string targetPath,
        CancellationToken cancellationToken = default)
    {
        var document = await _documentRepository.GetByPathAsync(targetPath, cancellationToken);
        return document is not null;
    }

    private static string? ResolveRelativePath(string url, string sourceDirectory)
    {
        // Handle different path formats
        var normalizedUrl = url.Replace("\\", "/").TrimStart('.', '/');

        // If it explicitly references csharp-compounding-docs
        if (normalizedUrl.StartsWith(CompoundingDocsPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return normalizedUrl[CompoundingDocsPrefix.Length..];
        }

        // Handle relative paths like ../problems/issue.md
        if (url.StartsWith("../") || url.StartsWith("./"))
        {
            var combined = Path.Combine(sourceDirectory, url);
            var normalized = Path.GetFullPath(combined)
                .Replace("\\", "/");

            // Extract the relative path within csharp-compounding-docs
            var docsIndex = normalized.IndexOf(CompoundingDocsPrefix, StringComparison.OrdinalIgnoreCase);
            if (docsIndex >= 0)
            {
                return normalized[(docsIndex + CompoundingDocsPrefix.Length)..];
            }
        }

        // If it's a simple relative path (problems/issue.md)
        if (!url.Contains("://") && url.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }

        return null;
    }

    private static string GetLinkText(LinkInline linkInline)
    {
        var textBuilder = new System.Text.StringBuilder();
        foreach (var child in linkInline)
        {
            if (child is LiteralInline literal)
            {
                textBuilder.Append(literal.Content);
            }
        }
        return textBuilder.ToString();
    }
}
```

### IRelatedDocsResolver Interface

Create `Services/IRelatedDocsResolver.cs`:

```csharp
namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Resolves related documents from YAML frontmatter relationships.
/// </summary>
public interface IRelatedDocsResolver
{
    /// <summary>
    /// Extracts related document paths from YAML frontmatter.
    /// </summary>
    /// <param name="frontmatter">Parsed frontmatter dictionary.</param>
    /// <returns>List of related document paths.</returns>
    IReadOnlyList<string> ExtractRelatedDocs(IDictionary<string, object?> frontmatter);

    /// <summary>
    /// Resolves related documents from a document's frontmatter.
    /// </summary>
    /// <param name="document">The source document.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of resolved related documents.</returns>
    Task<IReadOnlyList<RelatedDocumentInfo>> ResolveRelatedDocsAsync(
        CompoundDocument document,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Information about a related document from frontmatter.
/// </summary>
public record RelatedDocumentInfo(
    string Path,
    string? Title,
    bool Exists,
    string RelationshipType); // "related_docs", "see_also", etc.
```

### RelatedDocsResolver Implementation

Create `Services/RelatedDocsResolver.cs`:

```csharp
using Microsoft.Extensions.Logging;

namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Resolves related_docs frontmatter field to actual documents.
/// </summary>
public sealed class RelatedDocsResolver : IRelatedDocsResolver
{
    private readonly IDocumentRepository _documentRepository;
    private readonly ILogger<RelatedDocsResolver> _logger;

    // Supported frontmatter field names for related docs
    private static readonly string[] RelatedDocsFields = ["related_docs", "see_also", "related"];

    public RelatedDocsResolver(
        IDocumentRepository documentRepository,
        ILogger<RelatedDocsResolver> logger)
    {
        _documentRepository = documentRepository;
        _logger = logger;
    }

    public IReadOnlyList<string> ExtractRelatedDocs(IDictionary<string, object?> frontmatter)
    {
        var relatedPaths = new List<string>();

        foreach (var fieldName in RelatedDocsFields)
        {
            if (!frontmatter.TryGetValue(fieldName, out var value) || value is null)
            {
                continue;
            }

            // Handle array of paths
            if (value is IEnumerable<object> arrayValue)
            {
                foreach (var item in arrayValue)
                {
                    if (item is string path && !string.IsNullOrWhiteSpace(path))
                    {
                        relatedPaths.Add(NormalizePath(path));
                    }
                }
            }
            // Handle single string value
            else if (value is string singlePath && !string.IsNullOrWhiteSpace(singlePath))
            {
                relatedPaths.Add(NormalizePath(singlePath));
            }
        }

        return relatedPaths.Distinct().ToList();
    }

    public async Task<IReadOnlyList<RelatedDocumentInfo>> ResolveRelatedDocsAsync(
        CompoundDocument document,
        CancellationToken cancellationToken = default)
    {
        if (document.Frontmatter is null || document.Frontmatter.Count == 0)
        {
            return Array.Empty<RelatedDocumentInfo>();
        }

        var relatedPaths = ExtractRelatedDocs(document.Frontmatter);
        if (relatedPaths.Count == 0)
        {
            return Array.Empty<RelatedDocumentInfo>();
        }

        var results = new List<RelatedDocumentInfo>();

        foreach (var path in relatedPaths)
        {
            var relatedDoc = await _documentRepository.GetByPathAsync(path, cancellationToken);

            if (relatedDoc is null)
            {
                _logger.LogWarning(
                    "Related document '{Path}' referenced in {Source} does not exist",
                    path, document.RelativePath);

                results.Add(new RelatedDocumentInfo(
                    Path: path,
                    Title: null,
                    Exists: false,
                    RelationshipType: "related_docs"));
            }
            else
            {
                results.Add(new RelatedDocumentInfo(
                    Path: relatedDoc.RelativePath,
                    Title: relatedDoc.Title,
                    Exists: true,
                    RelationshipType: "related_docs"));
            }
        }

        _logger.LogDebug(
            "Resolved {Count} related docs from {Source}, {ExistingCount} exist",
            results.Count,
            document.RelativePath,
            results.Count(r => r.Exists));

        return results;
    }

    private static string NormalizePath(string path)
    {
        return path
            .Replace("\\", "/")
            .TrimStart('.', '/')
            .Replace("csharp-compounding-docs/", "");
    }
}
```

### ILinkResolutionCache Interface

Create `Services/ILinkResolutionCache.cs`:

```csharp
namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Caches link resolution results for performance optimization.
/// </summary>
public interface ILinkResolutionCache
{
    /// <summary>
    /// Gets cached link extraction results for a document.
    /// </summary>
    /// <param name="documentPath">The document path.</param>
    /// <param name="contentHash">Hash of the document content for cache validation.</param>
    /// <returns>Cached links if valid, null if cache miss or stale.</returns>
    CachedLinkResolution? GetCachedLinks(string documentPath, string contentHash);

    /// <summary>
    /// Caches link extraction results for a document.
    /// </summary>
    /// <param name="documentPath">The document path.</param>
    /// <param name="contentHash">Hash of the document content.</param>
    /// <param name="resolution">The resolution results to cache.</param>
    void CacheLinks(string documentPath, string contentHash, CachedLinkResolution resolution);

    /// <summary>
    /// Invalidates cache for a specific document.
    /// </summary>
    /// <param name="documentPath">The document path to invalidate.</param>
    void Invalidate(string documentPath);

    /// <summary>
    /// Clears all cached resolutions.
    /// </summary>
    void Clear();

    /// <summary>
    /// Gets cache statistics for monitoring.
    /// </summary>
    LinkResolutionCacheStats GetStatistics();
}

/// <summary>
/// Cached link resolution data.
/// </summary>
public record CachedLinkResolution(
    IReadOnlyList<ExtractedLink> MarkdownLinks,
    IReadOnlyList<string> RelatedDocPaths,
    IReadOnlyDictionary<string, bool> LinkTargetExistence,
    DateTimeOffset CachedAt);

/// <summary>
/// Cache statistics for monitoring.
/// </summary>
public record LinkResolutionCacheStats(
    int TotalEntries,
    long HitCount,
    long MissCount,
    double HitRate,
    long EvictionCount);
```

### MemoryLinkResolutionCache Implementation

Create `Services/MemoryLinkResolutionCache.cs`:

```csharp
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CompoundDocs.McpServer.Services;

/// <summary>
/// In-memory cache implementation for link resolution results.
/// </summary>
public sealed class MemoryLinkResolutionCache : ILinkResolutionCache, IDisposable
{
    private readonly IMemoryCache _cache;
    private readonly LinkResolutionCacheOptions _options;
    private readonly ILogger<MemoryLinkResolutionCache> _logger;

    // Statistics tracking
    private long _hitCount;
    private long _missCount;
    private long _evictionCount;
    private int _entryCount;

    private const string CacheKeyPrefix = "LinkRes:";

    public MemoryLinkResolutionCache(
        IMemoryCache cache,
        IOptions<LinkResolutionCacheOptions> options,
        ILogger<MemoryLinkResolutionCache> logger)
    {
        _cache = cache;
        _options = options.Value;
        _logger = logger;
    }

    public CachedLinkResolution? GetCachedLinks(string documentPath, string contentHash)
    {
        var cacheKey = BuildCacheKey(documentPath, contentHash);

        if (_cache.TryGetValue(cacheKey, out CachedLinkResolution? cached) && cached is not null)
        {
            Interlocked.Increment(ref _hitCount);
            _logger.LogTrace("Cache hit for {DocumentPath}", documentPath);
            return cached;
        }

        Interlocked.Increment(ref _missCount);
        _logger.LogTrace("Cache miss for {DocumentPath}", documentPath);
        return null;
    }

    public void CacheLinks(string documentPath, string contentHash, CachedLinkResolution resolution)
    {
        var cacheKey = BuildCacheKey(documentPath, contentHash);

        var cacheOptions = new MemoryCacheEntryOptions()
            .SetAbsoluteExpiration(TimeSpan.FromMinutes(_options.CacheTtlMinutes))
            .SetSlidingExpiration(TimeSpan.FromMinutes(_options.SlidingExpirationMinutes))
            .SetSize(1)
            .RegisterPostEvictionCallback((key, value, reason, state) =>
            {
                Interlocked.Increment(ref _evictionCount);
                Interlocked.Decrement(ref _entryCount);
                _logger.LogTrace("Cache entry evicted: {Key}, Reason: {Reason}", key, reason);
            });

        _cache.Set(cacheKey, resolution, cacheOptions);
        Interlocked.Increment(ref _entryCount);

        _logger.LogDebug(
            "Cached {LinkCount} links for {DocumentPath}",
            resolution.MarkdownLinks.Count + resolution.RelatedDocPaths.Count,
            documentPath);
    }

    public void Invalidate(string documentPath)
    {
        // We can't invalidate by prefix with IMemoryCache, so we use a pattern
        // where we always include content hash in the key
        // The old entry will naturally expire when content changes (hash mismatch)
        _logger.LogDebug("Invalidation requested for {DocumentPath}", documentPath);
    }

    public void Clear()
    {
        // IMemoryCache doesn't support clear, but we can compact aggressively
        if (_cache is MemoryCache memoryCache)
        {
            memoryCache.Compact(1.0); // Remove all entries
        }

        _entryCount = 0;
        _logger.LogInformation("Link resolution cache cleared");
    }

    public LinkResolutionCacheStats GetStatistics()
    {
        var total = _hitCount + _missCount;
        var hitRate = total > 0 ? (double)_hitCount / total : 0.0;

        return new LinkResolutionCacheStats(
            TotalEntries: _entryCount,
            HitCount: _hitCount,
            MissCount: _missCount,
            HitRate: hitRate,
            EvictionCount: _evictionCount);
    }

    public void Dispose()
    {
        // IMemoryCache is disposed by DI container
    }

    private static string BuildCacheKey(string documentPath, string contentHash)
    {
        return $"{CacheKeyPrefix}{documentPath}:{contentHash[..8]}";
    }
}

/// <summary>
/// Configuration options for link resolution caching.
/// </summary>
public class LinkResolutionCacheOptions
{
    public const string SectionName = "LinkResolution:Cache";

    /// <summary>
    /// Absolute expiration time in minutes. Default: 5.
    /// </summary>
    public int CacheTtlMinutes { get; set; } = 5;

    /// <summary>
    /// Sliding expiration time in minutes. Default: 2.
    /// </summary>
    public int SlidingExpirationMinutes { get; set; } = 2;

    /// <summary>
    /// Maximum number of entries in cache. Default: 1000.
    /// </summary>
    public int MaxEntries { get; set; } = 1000;
}
```

### ICrossReferenceResolutionService Interface

Create `Services/ICrossReferenceResolutionService.cs`:

```csharp
namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Orchestrates cross-reference resolution during RAG retrieval.
/// Combines markdown link extraction, related_docs following, and caching.
/// </summary>
public interface ICrossReferenceResolutionService
{
    /// <summary>
    /// Resolves all cross-references for a set of source documents.
    /// </summary>
    /// <param name="sourceDocuments">Documents to resolve references from.</param>
    /// <param name="options">Resolution options (depth, limits).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Resolved cross-references with content.</returns>
    Task<CrossReferenceResolutionResult> ResolveReferencesAsync(
        IReadOnlyList<RetrievedDocument> sourceDocuments,
        CrossReferenceResolutionOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates cross-references in a single document (for indexing/validation).
    /// </summary>
    /// <param name="document">The document to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Validation results with any broken links.</returns>
    Task<CrossReferenceValidationResult> ValidateReferencesAsync(
        CompoundDocument document,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Options for cross-reference resolution.
/// </summary>
public record CrossReferenceResolutionOptions
{
    /// <summary>
    /// Maximum depth to follow links. Default: 2.
    /// </summary>
    public int MaxDepth { get; init; } = 2;

    /// <summary>
    /// Maximum total linked documents to include. Default: 5.
    /// </summary>
    public int MaxLinkedDocs { get; init; } = 5;

    /// <summary>
    /// Whether to include full content for linked docs. Default: true for depth 1.
    /// </summary>
    public bool IncludeFullContent { get; init; } = true;

    /// <summary>
    /// Maximum content length per linked document. Default: 10000 chars.
    /// </summary>
    public int MaxContentLengthPerDoc { get; init; } = 10000;

    /// <summary>
    /// Resolution timeout. Default: 5 seconds.
    /// </summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Whether to use caching. Default: true.
    /// </summary>
    public bool UseCache { get; init; } = true;
}

/// <summary>
/// Result of cross-reference resolution.
/// </summary>
public record CrossReferenceResolutionResult
{
    /// <summary>
    /// Successfully resolved linked documents.
    /// </summary>
    public required IReadOnlyList<ResolvedCrossReference> ResolvedReferences { get; init; }

    /// <summary>
    /// Links that could not be resolved (missing targets).
    /// </summary>
    public required IReadOnlyList<UnresolvedCrossReference> UnresolvedReferences { get; init; }

    /// <summary>
    /// Total processing time.
    /// </summary>
    public TimeSpan ProcessingTime { get; init; }

    /// <summary>
    /// Whether the cache was used.
    /// </summary>
    public bool CacheUsed { get; init; }

    /// <summary>
    /// Number of cache hits during resolution.
    /// </summary>
    public int CacheHits { get; init; }
}

/// <summary>
/// A successfully resolved cross-reference.
/// </summary>
public record ResolvedCrossReference(
    string TargetPath,
    string Title,
    string? Summary,
    string? Content,
    int CharCount,
    string SourcePath,
    int Depth,
    CrossReferenceType ReferenceType);

/// <summary>
/// A cross-reference that could not be resolved.
/// </summary>
public record UnresolvedCrossReference(
    string TargetPath,
    string SourcePath,
    string Reason,
    CrossReferenceType ReferenceType);

/// <summary>
/// Type of cross-reference.
/// </summary>
public enum CrossReferenceType
{
    /// <summary>Markdown link in content.</summary>
    MarkdownLink,
    /// <summary>Related docs from frontmatter.</summary>
    RelatedDocs
}

/// <summary>
/// Result of cross-reference validation.
/// </summary>
public record CrossReferenceValidationResult(
    string DocumentPath,
    IReadOnlyList<ExtractedLink> ValidLinks,
    IReadOnlyList<ExtractedLink> BrokenLinks,
    IReadOnlyList<string> ValidRelatedDocs,
    IReadOnlyList<string> BrokenRelatedDocs);
```

### CrossReferenceResolutionService Implementation

Create `Services/CrossReferenceResolutionService.cs`:

```csharp
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Orchestrates cross-reference resolution combining link extraction,
/// related docs following, caching, and depth-limited traversal.
/// </summary>
public sealed class CrossReferenceResolutionService : ICrossReferenceResolutionService
{
    private readonly IMarkdownLinkExtractor _linkExtractor;
    private readonly IRelatedDocsResolver _relatedDocsResolver;
    private readonly ILinkResolutionCache _cache;
    private readonly IDocumentRepository _documentRepository;
    private readonly IOptions<ProjectConfiguration> _projectConfig;
    private readonly ILogger<CrossReferenceResolutionService> _logger;

    public CrossReferenceResolutionService(
        IMarkdownLinkExtractor linkExtractor,
        IRelatedDocsResolver relatedDocsResolver,
        ILinkResolutionCache cache,
        IDocumentRepository documentRepository,
        IOptions<ProjectConfiguration> projectConfig,
        ILogger<CrossReferenceResolutionService> logger)
    {
        _linkExtractor = linkExtractor;
        _relatedDocsResolver = relatedDocsResolver;
        _cache = cache;
        _documentRepository = documentRepository;
        _projectConfig = projectConfig;
        _logger = logger;
    }

    public async Task<CrossReferenceResolutionResult> ResolveReferencesAsync(
        IReadOnlyList<RetrievedDocument> sourceDocuments,
        CrossReferenceResolutionOptions options,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        if (options.MaxDepth <= 0 || options.MaxLinkedDocs <= 0)
        {
            _logger.LogDebug("Cross-reference resolution disabled (depth={Depth}, maxDocs={MaxDocs})",
                options.MaxDepth, options.MaxLinkedDocs);

            return new CrossReferenceResolutionResult
            {
                ResolvedReferences = Array.Empty<ResolvedCrossReference>(),
                UnresolvedReferences = Array.Empty<UnresolvedCrossReference>(),
                ProcessingTime = stopwatch.Elapsed,
                CacheUsed = false,
                CacheHits = 0
            };
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(options.Timeout);

        try
        {
            return await ResolveWithBfsAsync(sourceDocuments, options, timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Cross-reference resolution timed out after {Timeout}", options.Timeout);

            return new CrossReferenceResolutionResult
            {
                ResolvedReferences = Array.Empty<ResolvedCrossReference>(),
                UnresolvedReferences = Array.Empty<UnresolvedCrossReference>(),
                ProcessingTime = options.Timeout,
                CacheUsed = options.UseCache,
                CacheHits = 0
            };
        }
    }

    private async Task<CrossReferenceResolutionResult> ResolveWithBfsAsync(
        IReadOnlyList<RetrievedDocument> sourceDocuments,
        CrossReferenceResolutionOptions options,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var resolved = new List<ResolvedCrossReference>();
        var unresolved = new List<UnresolvedCrossReference>();
        var visited = new HashSet<string>(sourceDocuments.Select(d => d.Path));
        var cacheHits = 0;

        // BFS traversal
        var currentLevel = sourceDocuments.ToList();

        for (int depth = 1; depth <= options.MaxDepth && resolved.Count < options.MaxLinkedDocs; depth++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var nextLevel = new List<RetrievedDocument>();
            var referencesToResolve = new List<(string TargetPath, string SourcePath, CrossReferenceType Type)>();

            // Extract all references from current level documents
            foreach (var doc in currentLevel)
            {
                var (links, relatedDocs, cacheHit) = await ExtractReferencesAsync(
                    doc, options.UseCache, cancellationToken);

                if (cacheHit) cacheHits++;

                // Add markdown links
                foreach (var link in links)
                {
                    if (visited.Add(link.TargetPath))
                    {
                        referencesToResolve.Add((link.TargetPath, doc.Path, CrossReferenceType.MarkdownLink));
                    }
                }

                // Add related docs
                foreach (var relatedPath in relatedDocs)
                {
                    if (visited.Add(relatedPath))
                    {
                        referencesToResolve.Add((relatedPath, doc.Path, CrossReferenceType.RelatedDocs));
                    }
                }
            }

            if (referencesToResolve.Count == 0)
            {
                break;
            }

            // Batch resolve targets
            var targetPaths = referencesToResolve
                .Select(r => r.TargetPath)
                .Take(options.MaxLinkedDocs - resolved.Count)
                .ToList();

            var targetDocs = await _documentRepository.GetDocumentsByPathsAsync(
                targetPaths, cancellationToken);

            var targetDocsByPath = targetDocs.ToDictionary(d => d.RelativePath);

            // Process resolved and unresolved
            foreach (var (targetPath, sourcePath, refType) in referencesToResolve)
            {
                if (resolved.Count >= options.MaxLinkedDocs)
                {
                    break;
                }

                if (targetDocsByPath.TryGetValue(targetPath, out var targetDoc))
                {
                    var content = options.IncludeFullContent && depth == 1
                        ? TruncateContent(targetDoc.Content, options.MaxContentLengthPerDoc)
                        : null;

                    resolved.Add(new ResolvedCrossReference(
                        TargetPath: targetDoc.RelativePath,
                        Title: targetDoc.Title,
                        Summary: targetDoc.Summary,
                        Content: content,
                        CharCount: targetDoc.CharCount,
                        SourcePath: sourcePath,
                        Depth: depth,
                        ReferenceType: refType));

                    // Add to next level for further traversal
                    nextLevel.Add(new RetrievedDocument
                    {
                        Path = targetDoc.RelativePath,
                        Title = targetDoc.Title,
                        Content = targetDoc.Content,
                        CharCount = targetDoc.CharCount,
                        DocType = targetDoc.DocType,
                        RawRelevanceScore = 0,
                        BoostedRelevanceScore = 0,
                        PromotionLevel = Enum.Parse<PromotionLevel>(targetDoc.PromotionLevel, true)
                    });
                }
                else
                {
                    unresolved.Add(new UnresolvedCrossReference(
                        TargetPath: targetPath,
                        SourcePath: sourcePath,
                        Reason: "Document not found",
                        ReferenceType: refType));
                }
            }

            currentLevel = nextLevel;
        }

        stopwatch.Stop();

        _logger.LogInformation(
            "Resolved {ResolvedCount} cross-references ({UnresolvedCount} unresolved) " +
            "from {SourceCount} sources in {ElapsedMs}ms (cache hits: {CacheHits})",
            resolved.Count,
            unresolved.Count,
            sourceDocuments.Count,
            stopwatch.ElapsedMilliseconds,
            cacheHits);

        return new CrossReferenceResolutionResult
        {
            ResolvedReferences = resolved,
            UnresolvedReferences = unresolved,
            ProcessingTime = stopwatch.Elapsed,
            CacheUsed = options.UseCache,
            CacheHits = cacheHits
        };
    }

    private async Task<(IReadOnlyList<ExtractedLink> Links, IReadOnlyList<string> RelatedDocs, bool CacheHit)>
        ExtractReferencesAsync(
            RetrievedDocument document,
            bool useCache,
            CancellationToken cancellationToken)
    {
        var contentHash = ComputeContentHash(document.Content);

        // Check cache
        if (useCache)
        {
            var cached = _cache.GetCachedLinks(document.Path, contentHash);
            if (cached is not null)
            {
                return (cached.MarkdownLinks, cached.RelatedDocPaths, true);
            }
        }

        // Extract fresh
        var links = _linkExtractor.ExtractLinks(document.Content, document.Path);

        // Get related docs from repository (need full document with frontmatter)
        var fullDoc = await _documentRepository.GetByPathAsync(document.Path, cancellationToken);
        var relatedDocs = fullDoc?.Frontmatter is not null
            ? _relatedDocsResolver.ExtractRelatedDocs(fullDoc.Frontmatter)
            : Array.Empty<string>();

        // Cache results
        if (useCache)
        {
            var linkExistence = new Dictionary<string, bool>();
            foreach (var link in links)
            {
                linkExistence[link.TargetPath] = await _linkExtractor.ValidateLinkTargetAsync(
                    link.TargetPath, cancellationToken);
            }

            _cache.CacheLinks(document.Path, contentHash, new CachedLinkResolution(
                MarkdownLinks: links,
                RelatedDocPaths: relatedDocs,
                LinkTargetExistence: linkExistence,
                CachedAt: DateTimeOffset.UtcNow));
        }

        return (links, relatedDocs, false);
    }

    public async Task<CrossReferenceValidationResult> ValidateReferencesAsync(
        CompoundDocument document,
        CancellationToken cancellationToken = default)
    {
        var links = _linkExtractor.ExtractLinks(document.Content, document.RelativePath);
        var relatedDocs = document.Frontmatter is not null
            ? _relatedDocsResolver.ExtractRelatedDocs(document.Frontmatter)
            : Array.Empty<string>();

        var validLinks = new List<ExtractedLink>();
        var brokenLinks = new List<ExtractedLink>();

        foreach (var link in links)
        {
            var exists = await _linkExtractor.ValidateLinkTargetAsync(link.TargetPath, cancellationToken);
            if (exists)
            {
                validLinks.Add(link);
            }
            else
            {
                brokenLinks.Add(link);
            }
        }

        var validRelated = new List<string>();
        var brokenRelated = new List<string>();

        foreach (var path in relatedDocs)
        {
            var doc = await _documentRepository.GetByPathAsync(path, cancellationToken);
            if (doc is not null)
            {
                validRelated.Add(path);
            }
            else
            {
                brokenRelated.Add(path);
            }
        }

        return new CrossReferenceValidationResult(
            DocumentPath: document.RelativePath,
            ValidLinks: validLinks,
            BrokenLinks: brokenLinks,
            ValidRelatedDocs: validRelated,
            BrokenRelatedDocs: brokenRelated);
    }

    private static string ComputeContentHash(string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes);
    }

    private static string? TruncateContent(string content, int maxLength)
    {
        if (string.IsNullOrEmpty(content))
        {
            return null;
        }

        if (content.Length <= maxLength)
        {
            return content;
        }

        // Truncate at a paragraph boundary if possible
        var truncated = content[..maxLength];
        var lastParagraph = truncated.LastIndexOf("\n\n", StringComparison.Ordinal);
        if (lastParagraph > maxLength / 2)
        {
            return truncated[..lastParagraph] + "\n\n[Content truncated...]";
        }

        return truncated + "\n\n[Content truncated...]";
    }
}
```

### Service Registration

Add to `Extensions/ServiceCollectionExtensions.cs`:

```csharp
/// <summary>
/// Registers cross-reference resolution services.
/// </summary>
public static IServiceCollection AddCrossReferenceResolutionServices(
    this IServiceCollection services,
    IConfiguration configuration)
{
    // Cache configuration
    services.AddOptions<LinkResolutionCacheOptions>()
        .Bind(configuration.GetSection(LinkResolutionCacheOptions.SectionName))
        .ValidateDataAnnotations();

    // Memory cache
    services.AddMemoryCache(options =>
    {
        options.SizeLimit = configuration
            .GetValue<int>("LinkResolution:Cache:MaxEntries", 1000);
    });

    // Core services
    services.AddSingleton<IMarkdownLinkExtractor, MarkdigLinkExtractor>();
    services.AddSingleton<IRelatedDocsResolver, RelatedDocsResolver>();
    services.AddSingleton<ILinkResolutionCache, MemoryLinkResolutionCache>();
    services.AddScoped<ICrossReferenceResolutionService, CrossReferenceResolutionService>();

    return services;
}
```

### Integration with RAG Pipeline

Update `SemanticKernelRagGenerationService.cs` to use cross-reference resolution:

```csharp
// In the GenerateResponseAsync method, replace link traversal with cross-reference resolution:

public async Task<RagResponse> GenerateResponseAsync(
    string query,
    IReadOnlyList<RetrievedDocument> documents,
    RagGenerationOptions? options = null,
    CancellationToken cancellationToken = default)
{
    options ??= new RagGenerationOptions();
    var stopwatch = Stopwatch.StartNew();

    // Resolve cross-references
    IReadOnlyList<ResolvedCrossReference> crossRefs = Array.Empty<ResolvedCrossReference>();
    if (options.IncludeLinkedDocs && documents.Count > 0)
    {
        var resolutionOptions = new CrossReferenceResolutionOptions
        {
            MaxDepth = options.MaxLinkDepth,
            MaxLinkedDocs = options.MaxLinkedDocs,
            IncludeFullContent = true,
            UseCache = true
        };

        var result = await _crossRefService.ResolveReferencesAsync(
            documents,
            resolutionOptions,
            cancellationToken);

        crossRefs = result.ResolvedReferences;

        if (result.UnresolvedReferences.Count > 0)
        {
            _logger.LogWarning(
                "Could not resolve {Count} cross-references",
                result.UnresolvedReferences.Count);
        }
    }

    // Build context with cross-references
    var (contextText, includedDocs, includedCrossRefs) = BuildContextWithCrossRefs(
        documents, crossRefs, options);

    // ... rest of generation logic ...
}
```

---

## Configuration Schema

The following configuration settings control cross-reference resolution:

### appsettings.json

```json
{
  "LinkResolution": {
    "MaxDepth": 2,
    "MaxLinkedDocs": 5,
    "Cache": {
      "CacheTtlMinutes": 5,
      "SlidingExpirationMinutes": 2,
      "MaxEntries": 1000
    }
  }
}
```

### Project config.json Override

```json
{
  "link_resolution": {
    "max_depth": 3,
    "max_linked_docs": 10
  }
}
```

### Configuration Mapping

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `link_resolution.max_depth` | int | 2 | Maximum BFS depth for link traversal |
| `link_resolution.max_linked_docs` | int | 5 | Maximum linked documents to include |
| `link_resolution.cache.cache_ttl_minutes` | int | 5 | Cache absolute expiration |
| `link_resolution.cache.sliding_expiration_minutes` | int | 2 | Cache sliding expiration |
| `link_resolution.cache.max_entries` | int | 1000 | Maximum cache entries |

---

## Dependencies

### Depends On

- **Phase 067**: Link Depth Following for RAG - Provides `ILinkTraversalService` and BFS patterns
- **Phase 051**: RAG Retrieval Service - Provides `IRAGRetrievalService` integration point
- **Phase 015**: Markdown Parser - Markdig AST infrastructure
- **Phase 032**: Document Repository - `IDocumentRepository` for document retrieval
- **Phase 014**: YAML Frontmatter Parsing - Frontmatter extraction for `related_docs`

### Blocks

- **Phase XXX**: Semantic link discovery (finding related docs by content similarity)
- **Phase XXX**: Link health monitoring and broken link reports
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
   dotnet test tests/CompoundDocs.McpServer.Tests/ --filter "CrossReference"
   ```

3. **Link extraction correctness**:
   ```csharp
   [Fact]
   public void ExtractLinks_FindsRelativeMarkdownLinks()
   {
       var extractor = new MarkdigLinkExtractor(
           Mock.Of<IDocumentRepository>(),
           Mock.Of<ILogger<MarkdigLinkExtractor>>());

       var content = """
           See [related issue](../problems/db-pool.md) for context.
           Also check [insight](./csharp-compounding-docs/insights/pattern.md).
           External [link](https://example.com) ignored.
           """;

       var links = extractor.ExtractLinks(content, "insights/current.md");

       Assert.Equal(2, links.Count);
       Assert.Contains(links, l => l.TargetPath == "problems/db-pool.md");
       Assert.Contains(links, l => l.TargetPath == "insights/pattern.md");
   }
   ```

4. **Related docs extraction**:
   ```csharp
   [Fact]
   public void ExtractRelatedDocs_ParsesFrontmatterArray()
   {
       var resolver = new RelatedDocsResolver(
           Mock.Of<IDocumentRepository>(),
           Mock.Of<ILogger<RelatedDocsResolver>>());

       var frontmatter = new Dictionary<string, object?>
       {
           ["related_docs"] = new List<object>
           {
               "problems/issue-1.md",
               "insights/pattern-2.md"
           }
       };

       var related = resolver.ExtractRelatedDocs(frontmatter);

       Assert.Equal(2, related.Count);
       Assert.Contains("problems/issue-1.md", related);
       Assert.Contains("insights/pattern-2.md", related);
   }
   ```

5. **Cache behavior**:
   ```csharp
   [Fact]
   public void Cache_ReturnsHitOnMatchingHash()
   {
       var cache = CreateCache();
       var contentHash = "abc123hash";

       var resolution = new CachedLinkResolution(
           MarkdownLinks: new[] { new ExtractedLink("target.md", "link", "target.md", null, 1) },
           RelatedDocPaths: new[] { "related.md" },
           LinkTargetExistence: new Dictionary<string, bool> { ["target.md"] = true },
           CachedAt: DateTimeOffset.UtcNow);

       cache.CacheLinks("doc.md", contentHash, resolution);

       var cached = cache.GetCachedLinks("doc.md", contentHash);

       Assert.NotNull(cached);
       Assert.Single(cached.MarkdownLinks);
   }

   [Fact]
   public void Cache_ReturnsMissOnDifferentHash()
   {
       var cache = CreateCache();

       cache.CacheLinks("doc.md", "hash1", CreateResolution());

       var cached = cache.GetCachedLinks("doc.md", "hash2");

       Assert.Null(cached);
   }
   ```

6. **Depth limiting**:
   ```csharp
   [Fact]
   public async Task ResolveReferences_RespectsMaxDepth()
   {
       // Arrange: A -> B -> C -> D chain
       var service = CreateServiceWithDocuments(new Dictionary<string, string[]>
       {
           ["A.md"] = new[] { "B.md" },
           ["B.md"] = new[] { "C.md" },
           ["C.md"] = new[] { "D.md" }
       });

       var sources = new[] { CreateDocument("A.md") };

       // Act: max depth 2
       var result = await service.ResolveReferencesAsync(
           sources,
           new CrossReferenceResolutionOptions { MaxDepth = 2, MaxLinkedDocs = 10 });

       // Assert: B (depth 1) and C (depth 2) included, D excluded
       Assert.Equal(2, result.ResolvedReferences.Count);
       Assert.Contains(result.ResolvedReferences, r => r.TargetPath == "B.md" && r.Depth == 1);
       Assert.Contains(result.ResolvedReferences, r => r.TargetPath == "C.md" && r.Depth == 2);
       Assert.DoesNotContain(result.ResolvedReferences, r => r.TargetPath == "D.md");
   }
   ```

7. **Circular reference handling**:
   ```csharp
   [Fact]
   public async Task ResolveReferences_HandlesCircularRefs()
   {
       // Arrange: A -> B -> C -> A cycle
       var service = CreateServiceWithDocuments(new Dictionary<string, string[]>
       {
           ["A.md"] = new[] { "B.md" },
           ["B.md"] = new[] { "C.md" },
           ["C.md"] = new[] { "A.md" }
       });

       var sources = new[] { CreateDocument("A.md") };

       // Act
       var result = await service.ResolveReferencesAsync(
           sources,
           new CrossReferenceResolutionOptions { MaxDepth = 10, MaxLinkedDocs = 10 });

       // Assert: No infinite loop, each doc appears once max
       Assert.Equal(2, result.ResolvedReferences.Count); // B and C
       Assert.DoesNotContain(result.ResolvedReferences, r => r.TargetPath == "A.md");
   }
   ```

---

## Files Created/Modified

| File | Action | Description |
|------|--------|-------------|
| `src/CompoundDocs.McpServer/Services/IMarkdownLinkExtractor.cs` | Create | Link extraction interface |
| `src/CompoundDocs.McpServer/Services/MarkdigLinkExtractor.cs` | Create | Markdig-based implementation |
| `src/CompoundDocs.McpServer/Services/IRelatedDocsResolver.cs` | Create | Related docs interface |
| `src/CompoundDocs.McpServer/Services/RelatedDocsResolver.cs` | Create | Frontmatter-based implementation |
| `src/CompoundDocs.McpServer/Services/ILinkResolutionCache.cs` | Create | Cache interface |
| `src/CompoundDocs.McpServer/Services/MemoryLinkResolutionCache.cs` | Create | In-memory cache implementation |
| `src/CompoundDocs.McpServer/Services/ICrossReferenceResolutionService.cs` | Create | Orchestration interface |
| `src/CompoundDocs.McpServer/Services/CrossReferenceResolutionService.cs` | Create | Main orchestration implementation |
| `src/CompoundDocs.McpServer/Models/ExtractedLink.cs` | Create | Link model |
| `src/CompoundDocs.McpServer/Models/CrossReferenceResolutionResult.cs` | Create | Result models |
| `src/CompoundDocs.McpServer/Options/LinkResolutionCacheOptions.cs` | Create | Cache configuration |
| `src/CompoundDocs.McpServer/Extensions/ServiceCollectionExtensions.cs` | Modify | Add service registration |
| `src/CompoundDocs.McpServer/Services/SemanticKernelRagGenerationService.cs` | Modify | Integrate cross-ref resolution |
| `tests/CompoundDocs.McpServer.Tests/Services/MarkdownLinkExtractorTests.cs` | Create | Link extraction tests |
| `tests/CompoundDocs.McpServer.Tests/Services/RelatedDocsResolverTests.cs` | Create | Related docs tests |
| `tests/CompoundDocs.McpServer.Tests/Services/LinkResolutionCacheTests.cs` | Create | Cache tests |
| `tests/CompoundDocs.McpServer.Tests/Services/CrossReferenceResolutionServiceTests.cs` | Create | Integration tests |

---

## Performance Considerations

### Caching Impact

| Scenario | Without Cache | With Cache (Warm) |
|----------|---------------|-------------------|
| Single document, 5 links | ~50ms | ~5ms |
| 3 source docs, depth 2 | ~200ms | ~20ms |
| Large graph (100 links) | ~500ms | ~50ms |

### Memory Usage

| Cache Size | Memory (Approximate) |
|------------|---------------------|
| 100 entries | ~1 MB |
| 500 entries | ~5 MB |
| 1000 entries | ~10 MB |

### Optimization Strategies

1. **Early termination**: Stop BFS when `max_linked_docs` reached
2. **Batch retrieval**: Single DB query for all documents at each depth level
3. **Content hash caching**: Invalidate only when document content changes
4. **Lazy content loading**: Load full content only for depth-1 links when needed
5. **Parallel validation**: Validate link targets concurrently

---

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| Deep graphs cause timeouts | Configurable timeout (default 5s), max_depth limit |
| Large cache memory consumption | Size limit (default 1000), sliding expiration |
| Circular references cause loops | Visited set tracking in BFS traversal |
| Missing documents break pipeline | Graceful skip with warning log, unresolved list returned |
| Stale cache after document update | Content hash in cache key, file watcher invalidation |
| Markdig parsing errors | Try-catch with logging, return empty links on error |
| Token budget exceeded | Content truncation, summary-only mode for deep links |

---

## Notes

- Cross-reference resolution combines Phase 067's link traversal with additional caching and related_docs support
- The cache uses content hash in the key, so document updates automatically invalidate stale entries
- Both markdown links and frontmatter `related_docs` fields are treated equally in the link graph
- Content inclusion is full for depth-1 links, configurable for deeper levels
- The `linked_docs` field in RAG response now includes `reference_type` to indicate link source
- Consider adding semantic link discovery (content-similarity based) as a future enhancement
- File watcher integration should call `ILinkResolutionCache.Invalidate()` on document changes
