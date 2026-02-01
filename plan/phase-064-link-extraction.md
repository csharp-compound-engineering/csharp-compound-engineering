# Phase 064: Markdown Link Extraction

> **Status**: NOT_STARTED
> **Effort Estimate**: 6-8 hours
> **Category**: Document Processing
> **Prerequisites**: Phase 015 (Markdown Parser Integration)

---

## Spec References

This phase implements comprehensive link extraction and resolution defined in:

- **spec/mcp-server.md** - [Link Resolution](../spec/mcp-server.md#link-resolution) (lines 251-290)
- **spec/skills/skill-patterns.md** - [Cross-References in Skills](../spec/skills/skill-patterns.md#cross-references-in-skills) (lines 129-155)
- **Phase 015** - Markdown Parser Integration (link extraction interfaces)
- **Phase 016** - QuikGraph Integration (graph population consumer)

---

## Objectives

1. Enhance the `ILinkExtractor` with comprehensive link type classification
2. Implement robust markdown link parsing for all link formats
3. Create internal vs external link detection and filtering
4. Build link target resolution to document IDs
5. Extract rich link metadata for RAG context expansion
6. Integrate with the document link graph for cross-reference tracking

---

## Acceptance Criteria

### Link Parsing Enhancement

- [ ] Parse standard inline links: `[text](path)`
- [ ] Parse inline links with titles: `[text](path "title")`
- [ ] Parse reference-style links: `[text][ref]` with `[ref]: path`
- [ ] Parse implicit reference links: `[ref]` with `[ref]: path`
- [ ] Parse autolinks: `<https://example.com>`
- [ ] Parse bare URLs (when enabled): `https://example.com`
- [ ] Handle escaped brackets and parentheses correctly
- [ ] Preserve link text with inline formatting (bold, italic, code)

### Internal Link Detection

- [ ] Relative paths detected: `./doc.md`, `../other/doc.md`
- [ ] Root-relative paths detected: `/docs/doc.md`
- [ ] Anchor-only links detected: `#section-name`
- [ ] Combined path and anchor links: `./doc.md#section`
- [ ] Links without extension assumed to be directories or auto-resolved
- [ ] Case-insensitive path matching on Windows, case-sensitive on Unix

### External Link Filtering

- [ ] HTTP/HTTPS URLs classified as external
- [ ] Mailto links classified as external
- [ ] Tel links classified as external
- [ ] FTP links classified as external
- [ ] Protocol-relative URLs (`//example.com`) classified as external
- [ ] Data URLs filtered out (not useful for RAG)
- [ ] JavaScript URLs filtered out (security)

### Link Target Resolution

- [ ] Resolve relative paths to absolute document paths
- [ ] Normalize path separators (forward slashes)
- [ ] Handle `..` and `.` path segments correctly
- [ ] Map resolved paths to document IDs in the database
- [ ] Return `null` for links to non-existent documents
- [ ] Support anchor extraction from resolved links

### Link Metadata Extraction

- [ ] Extract link text (display text)
- [ ] Extract link title (tooltip text)
- [ ] Extract source line number
- [ ] Extract source column/character position
- [ ] Extract surrounding context (sentence or paragraph)
- [ ] Classify link purpose (see-also, reference, citation, inline)
- [ ] Track link position within document structure (which section)

### Image Link Handling

- [ ] Filter out image links from document link extraction
- [ ] Separate `IImageLinkExtractor` for image-specific needs (future)
- [ ] Distinguish between `![alt](path)` and `[text](path)`

### Testing

- [ ] Unit tests for all inline link formats
- [ ] Unit tests for reference-style links
- [ ] Unit tests for internal/external classification
- [ ] Unit tests for path resolution edge cases
- [ ] Unit tests for malformed/edge-case links
- [ ] Integration tests with real markdown documents
- [ ] Performance tests with large documents (1000+ links)

---

## Implementation Notes

### Enhanced Link Models

Create enhanced link models in `CompoundDocs.Common/Markdown/Models/`:

```csharp
namespace CompoundDocs.Common.Markdown.Models;

/// <summary>
/// Classification of a markdown link's target type.
/// </summary>
public enum LinkTargetType
{
    /// <summary>Internal document link (relative path).</summary>
    InternalDocument,

    /// <summary>Internal anchor link within same document.</summary>
    InternalAnchor,

    /// <summary>Internal document link with anchor.</summary>
    InternalDocumentWithAnchor,

    /// <summary>External HTTP/HTTPS URL.</summary>
    ExternalHttp,

    /// <summary>Email mailto link.</summary>
    ExternalEmail,

    /// <summary>Telephone link.</summary>
    ExternalTel,

    /// <summary>Other external protocol.</summary>
    ExternalOther,

    /// <summary>Data URL (filtered).</summary>
    DataUrl,

    /// <summary>Invalid or unparseable link.</summary>
    Invalid
}

/// <summary>
/// Classification of link's semantic purpose within the document.
/// </summary>
public enum LinkPurpose
{
    /// <summary>Inline reference within prose.</summary>
    Inline,

    /// <summary>See-also or related document reference.</summary>
    SeeAlso,

    /// <summary>Citation or footnote reference.</summary>
    Citation,

    /// <summary>Navigation link (TOC, breadcrumb).</summary>
    Navigation,

    /// <summary>Unknown or unclassified purpose.</summary>
    Unknown
}

/// <summary>
/// Comprehensive information about an extracted markdown link.
/// </summary>
public record ExtractedLink
{
    /// <summary>The raw URL/path as written in the markdown.</summary>
    public required string RawUrl { get; init; }

    /// <summary>The display text of the link.</summary>
    public required string Text { get; init; }

    /// <summary>Optional title attribute (tooltip text).</summary>
    public string? Title { get; init; }

    /// <summary>Classification of the link target type.</summary>
    public required LinkTargetType TargetType { get; init; }

    /// <summary>Semantic purpose of the link.</summary>
    public LinkPurpose Purpose { get; init; } = LinkPurpose.Unknown;

    /// <summary>Source line number (0-based).</summary>
    public required int Line { get; init; }

    /// <summary>Source column number (0-based).</summary>
    public int Column { get; init; }

    /// <summary>Whether this is a reference-style link.</summary>
    public bool IsReferenceStyle { get; init; }

    /// <summary>Reference label for reference-style links.</summary>
    public string? ReferenceLabel { get; init; }

    /// <summary>Extracted anchor fragment (without #).</summary>
    public string? Anchor { get; init; }

    /// <summary>Path portion (without anchor).</summary>
    public string? PathWithoutAnchor { get; init; }

    /// <summary>The header path where this link appears.</summary>
    public string? ContainingSection { get; init; }

    /// <summary>Surrounding text context (for RAG relevance).</summary>
    public string? SurroundingContext { get; init; }
}

/// <summary>
/// Resolved link with target document information.
/// </summary>
public record ResolvedLink
{
    /// <summary>The original extracted link.</summary>
    public required ExtractedLink Source { get; init; }

    /// <summary>Resolved absolute path within the docs directory.</summary>
    public string? ResolvedPath { get; init; }

    /// <summary>Target document ID in the database (if exists).</summary>
    public string? TargetDocumentId { get; init; }

    /// <summary>Whether the target document exists.</summary>
    public bool TargetExists { get; init; }

    /// <summary>Reason for resolution failure (if any).</summary>
    public string? ResolutionError { get; init; }
}
```

### Enhanced Link Extractor Interface

```csharp
namespace CompoundDocs.Common.Markdown.Abstractions;

/// <summary>
/// Enhanced link extraction with comprehensive metadata.
/// </summary>
public interface ILinkExtractor
{
    /// <summary>
    /// Extracts all links from markdown content.
    /// </summary>
    IReadOnlyList<ExtractedLink> Extract(string markdownContent);

    /// <summary>
    /// Extracts only internal document links (for graph building).
    /// </summary>
    IReadOnlyList<ExtractedLink> ExtractInternalLinks(string markdownContent);

    /// <summary>
    /// Extracts only external links (for validation/reporting).
    /// </summary>
    IReadOnlyList<ExtractedLink> ExtractExternalLinks(string markdownContent);
}

/// <summary>
/// Resolves extracted links to actual document paths and IDs.
/// </summary>
public interface ILinkResolver
{
    /// <summary>
    /// Resolves a link relative to the source document.
    /// </summary>
    ResolvedLink Resolve(ExtractedLink link, string sourceDocumentPath);

    /// <summary>
    /// Resolves multiple links in batch for efficiency.
    /// </summary>
    IReadOnlyList<ResolvedLink> ResolveBatch(
        IEnumerable<ExtractedLink> links,
        string sourceDocumentPath);

    /// <summary>
    /// Resolves and returns only links to existing documents.
    /// </summary>
    Task<IReadOnlyList<ResolvedLink>> ResolveToExistingDocumentsAsync(
        IEnumerable<ExtractedLink> links,
        string sourceDocumentPath,
        CancellationToken cancellationToken = default);
}
```

### Link Extractor Implementation

```csharp
using Markdig;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace CompoundDocs.Common.Markdown;

public class EnhancedLinkExtractor : ILinkExtractor
{
    private static readonly HashSet<string> ExternalProtocols = new(StringComparer.OrdinalIgnoreCase)
    {
        "http", "https", "mailto", "tel", "ftp", "ftps", "file"
    };

    public IReadOnlyList<ExtractedLink> Extract(string markdownContent)
    {
        var document = Markdown.Parse(markdownContent, MarkdownPipelines.Default);
        var links = new List<ExtractedLink>();
        var sections = BuildSectionMap(document);

        foreach (var linkInline in document.Descendants<LinkInline>())
        {
            if (linkInline.IsImage)
                continue;

            var link = ExtractLinkInfo(linkInline, sections, markdownContent);
            if (link.TargetType != LinkTargetType.Invalid &&
                link.TargetType != LinkTargetType.DataUrl)
            {
                links.Add(link);
            }
        }

        return links;
    }

    public IReadOnlyList<ExtractedLink> ExtractInternalLinks(string markdownContent)
    {
        return Extract(markdownContent)
            .Where(IsInternalLink)
            .ToList();
    }

    public IReadOnlyList<ExtractedLink> ExtractExternalLinks(string markdownContent)
    {
        return Extract(markdownContent)
            .Where(l => !IsInternalLink(l))
            .ToList();
    }

    private static bool IsInternalLink(ExtractedLink link)
    {
        return link.TargetType is
            LinkTargetType.InternalDocument or
            LinkTargetType.InternalAnchor or
            LinkTargetType.InternalDocumentWithAnchor;
    }

    private ExtractedLink ExtractLinkInfo(
        LinkInline linkInline,
        Dictionary<int, string> sectionMap,
        string content)
    {
        var rawUrl = linkInline.Url ?? string.Empty;
        var text = GetLinkText(linkInline);
        var title = linkInline.Title;
        var line = linkInline.Line;
        var column = linkInline.Column;

        // Parse anchor and path
        var anchorIndex = rawUrl.IndexOf('#');
        string? anchor = null;
        string? pathWithoutAnchor = rawUrl;

        if (anchorIndex >= 0)
        {
            anchor = rawUrl[(anchorIndex + 1)..];
            pathWithoutAnchor = anchorIndex > 0 ? rawUrl[..anchorIndex] : null;
        }

        // Classify target type
        var targetType = ClassifyLinkTarget(rawUrl);

        // Determine purpose from context
        var purpose = DetermineLinkPurpose(linkInline, content);

        // Get containing section
        var containingSection = FindContainingSection(line, sectionMap);

        // Extract surrounding context
        var surroundingContext = ExtractSurroundingContext(linkInline, content);

        return new ExtractedLink
        {
            RawUrl = rawUrl,
            Text = text,
            Title = title,
            TargetType = targetType,
            Purpose = purpose,
            Line = line,
            Column = column,
            IsReferenceStyle = linkInline.IsShortcut || !string.IsNullOrEmpty(linkInline.Label),
            ReferenceLabel = linkInline.Label,
            Anchor = anchor,
            PathWithoutAnchor = pathWithoutAnchor,
            ContainingSection = containingSection,
            SurroundingContext = surroundingContext
        };
    }

    private static LinkTargetType ClassifyLinkTarget(string url)
    {
        if (string.IsNullOrEmpty(url))
            return LinkTargetType.Invalid;

        // Data URLs
        if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return LinkTargetType.DataUrl;

        // JavaScript URLs (security filter)
        if (url.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
            return LinkTargetType.Invalid;

        // Anchor-only links
        if (url.StartsWith('#'))
            return LinkTargetType.InternalAnchor;

        // Protocol-relative URLs
        if (url.StartsWith("//"))
            return LinkTargetType.ExternalHttp;

        // Check for protocol
        var colonIndex = url.IndexOf(':');
        if (colonIndex > 0 && colonIndex < url.IndexOf('/'))
        {
            var protocol = url[..colonIndex].ToLowerInvariant();

            if (protocol is "http" or "https")
                return LinkTargetType.ExternalHttp;

            if (protocol == "mailto")
                return LinkTargetType.ExternalEmail;

            if (protocol == "tel")
                return LinkTargetType.ExternalTel;

            if (ExternalProtocols.Contains(protocol))
                return LinkTargetType.ExternalOther;
        }

        // Relative paths - check for anchor
        if (url.Contains('#'))
            return LinkTargetType.InternalDocumentWithAnchor;

        return LinkTargetType.InternalDocument;
    }

    private static LinkPurpose DetermineLinkPurpose(LinkInline link, string content)
    {
        // Check if in a "See also" or "Related" section
        var lineContent = GetLineContent(content, link.Line);

        if (lineContent.StartsWith("See also:", StringComparison.OrdinalIgnoreCase) ||
            lineContent.StartsWith("Related:", StringComparison.OrdinalIgnoreCase) ||
            lineContent.StartsWith("- See", StringComparison.OrdinalIgnoreCase))
        {
            return LinkPurpose.SeeAlso;
        }

        // Check for citation patterns (footnote-style)
        if (link.IsShortcut && !string.IsNullOrEmpty(link.Label) &&
            int.TryParse(link.Label, out _))
        {
            return LinkPurpose.Citation;
        }

        // Default to inline
        return LinkPurpose.Inline;
    }

    private static string GetLinkText(LinkInline link)
    {
        var sb = new StringBuilder();
        foreach (var child in link)
        {
            if (child is LiteralInline literal)
                sb.Append(literal.Content);
            else if (child is EmphasisInline emphasis)
                sb.Append(GetEmphasisText(emphasis));
            else if (child is CodeInline code)
                sb.Append(code.Content);
        }
        return sb.ToString();
    }

    private static string GetEmphasisText(EmphasisInline emphasis)
    {
        var sb = new StringBuilder();
        foreach (var child in emphasis)
        {
            if (child is LiteralInline literal)
                sb.Append(literal.Content);
        }
        return sb.ToString();
    }

    private static Dictionary<int, string> BuildSectionMap(MarkdownDocument document)
    {
        var map = new Dictionary<int, string>();
        var headerStack = new Stack<(int Level, string Text, int Line)>();

        foreach (var heading in document.Descendants<HeadingBlock>())
        {
            while (headerStack.Count > 0 && headerStack.Peek().Level >= heading.Level)
                headerStack.Pop();

            var text = GetHeadingText(heading);
            headerStack.Push((heading.Level, text, heading.Line));

            var path = string.Join(" > ", headerStack.Reverse()
                .Select(h => $"{new string('#', h.Level)} {h.Text}"));

            map[heading.Line] = path;
        }

        return map;
    }

    private static string? FindContainingSection(int line, Dictionary<int, string> sectionMap)
    {
        var sectionLine = sectionMap.Keys
            .Where(l => l <= line)
            .OrderByDescending(l => l)
            .FirstOrDefault();

        return sectionLine > 0 ? sectionMap[sectionLine] : null;
    }

    private static string ExtractSurroundingContext(LinkInline link, string content)
    {
        var lines = content.Split('\n');
        if (link.Line >= lines.Length)
            return string.Empty;

        // Get the paragraph containing the link (simple approach: just the line)
        return lines[link.Line].Trim();
    }

    private static string GetLineContent(string content, int lineNumber)
    {
        var lines = content.Split('\n');
        return lineNumber < lines.Length ? lines[lineNumber] : string.Empty;
    }

    private static string GetHeadingText(HeadingBlock heading)
    {
        var sb = new StringBuilder();
        if (heading.Inline != null)
        {
            foreach (var inline in heading.Inline)
            {
                if (inline is LiteralInline literal)
                    sb.Append(literal.Content);
            }
        }
        return sb.ToString();
    }
}
```

### Link Resolver Implementation

```csharp
namespace CompoundDocs.Common.Markdown;

public class LinkResolver : ILinkResolver
{
    private readonly IDocumentRepository _repository;
    private readonly string _docsRootPath;

    public LinkResolver(IDocumentRepository repository, string docsRootPath)
    {
        _repository = repository;
        _docsRootPath = Path.GetFullPath(docsRootPath);
    }

    public ResolvedLink Resolve(ExtractedLink link, string sourceDocumentPath)
    {
        // External links don't need resolution
        if (!IsInternalLink(link))
        {
            return new ResolvedLink
            {
                Source = link,
                ResolvedPath = null,
                TargetDocumentId = null,
                TargetExists = false,
                ResolutionError = "External link - no resolution needed"
            };
        }

        // Anchor-only links refer to same document
        if (link.TargetType == LinkTargetType.InternalAnchor)
        {
            return new ResolvedLink
            {
                Source = link,
                ResolvedPath = sourceDocumentPath,
                TargetDocumentId = null, // Same document
                TargetExists = true,
                ResolutionError = null
            };
        }

        try
        {
            var resolvedPath = ResolvePath(link.PathWithoutAnchor!, sourceDocumentPath);
            var normalizedPath = NormalizePath(resolvedPath);

            return new ResolvedLink
            {
                Source = link,
                ResolvedPath = normalizedPath,
                TargetDocumentId = null, // Will be populated by async method
                TargetExists = File.Exists(Path.Combine(_docsRootPath, normalizedPath)),
                ResolutionError = null
            };
        }
        catch (Exception ex)
        {
            return new ResolvedLink
            {
                Source = link,
                ResolvedPath = null,
                TargetDocumentId = null,
                TargetExists = false,
                ResolutionError = ex.Message
            };
        }
    }

    public IReadOnlyList<ResolvedLink> ResolveBatch(
        IEnumerable<ExtractedLink> links,
        string sourceDocumentPath)
    {
        return links.Select(l => Resolve(l, sourceDocumentPath)).ToList();
    }

    public async Task<IReadOnlyList<ResolvedLink>> ResolveToExistingDocumentsAsync(
        IEnumerable<ExtractedLink> links,
        string sourceDocumentPath,
        CancellationToken cancellationToken = default)
    {
        var resolved = ResolveBatch(links, sourceDocumentPath);
        var results = new List<ResolvedLink>();

        foreach (var link in resolved)
        {
            if (link.ResolvedPath == null)
            {
                results.Add(link);
                continue;
            }

            var document = await _repository.GetByPathAsync(
                link.ResolvedPath,
                cancellationToken);

            results.Add(link with
            {
                TargetDocumentId = document?.Id,
                TargetExists = document != null
            });
        }

        return results;
    }

    private string ResolvePath(string linkPath, string sourceDocumentPath)
    {
        // Get the directory of the source document
        var sourceDir = Path.GetDirectoryName(sourceDocumentPath) ?? string.Empty;

        // Combine and normalize
        var combinedPath = Path.Combine(sourceDir, linkPath);
        var fullPath = Path.GetFullPath(combinedPath);

        // Ensure the path is within the docs root
        if (!fullPath.StartsWith(_docsRootPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Link target escapes docs root: {linkPath}");
        }

        // Return relative path from docs root
        return Path.GetRelativePath(_docsRootPath, fullPath);
    }

    private static string NormalizePath(string path)
    {
        // Normalize to forward slashes for consistency
        return path.Replace('\\', '/');
    }

    private static bool IsInternalLink(ExtractedLink link)
    {
        return link.TargetType is
            LinkTargetType.InternalDocument or
            LinkTargetType.InternalAnchor or
            LinkTargetType.InternalDocumentWithAnchor;
    }
}
```

### Integration with Document Link Graph

```csharp
namespace CompoundDocs.Common.Services;

/// <summary>
/// Service that coordinates link extraction and graph updates.
/// </summary>
public class DocumentLinkService
{
    private readonly ILinkExtractor _linkExtractor;
    private readonly ILinkResolver _linkResolver;
    private readonly IDocumentLinkGraph _linkGraph;
    private readonly ILogger<DocumentLinkService> _logger;

    public DocumentLinkService(
        ILinkExtractor linkExtractor,
        ILinkResolver linkResolver,
        IDocumentLinkGraph linkGraph,
        ILogger<DocumentLinkService> logger)
    {
        _linkExtractor = linkExtractor;
        _linkResolver = linkResolver;
        _linkGraph = linkGraph;
        _logger = logger;
    }

    /// <summary>
    /// Extracts links from a document and updates the link graph.
    /// </summary>
    public async Task ProcessDocumentLinksAsync(
        string relativePath,
        string content,
        CancellationToken cancellationToken = default)
    {
        // Extract internal links
        var internalLinks = _linkExtractor.ExtractInternalLinks(content);

        // Resolve to paths
        var resolved = _linkResolver.ResolveBatch(internalLinks, relativePath);

        // Get valid link targets
        var validTargets = resolved
            .Where(r => r.TargetExists && r.ResolvedPath != null)
            .Select(r => r.ResolvedPath!)
            .Distinct()
            .ToList();

        // Check for circular references before updating
        foreach (var target in validTargets)
        {
            if (_linkGraph.WouldCreateCycle(relativePath, target))
            {
                _logger.LogWarning(
                    "Circular reference detected: {Source} -> {Target}",
                    relativePath,
                    target);
            }
        }

        // Update graph
        _linkGraph.UpdateDocumentLinks(relativePath, validTargets);

        _logger.LogDebug(
            "Updated link graph for {Path}: {Count} outgoing links",
            relativePath,
            validTargets.Count);
    }

    /// <summary>
    /// Gets linked documents for RAG context expansion.
    /// </summary>
    public IReadOnlyList<string> GetLinkedDocumentsForRag(
        string relativePath,
        int maxDepth,
        int maxDocuments)
    {
        return _linkGraph.GetLinkedDocuments(relativePath, maxDepth, maxDocuments);
    }
}
```

### Dependency Injection Registration

```csharp
public static class LinkExtractionServiceCollectionExtensions
{
    public static IServiceCollection AddLinkExtraction(
        this IServiceCollection services,
        string docsRootPath)
    {
        services.AddSingleton<ILinkExtractor, EnhancedLinkExtractor>();
        services.AddSingleton<ILinkResolver>(sp =>
            new LinkResolver(
                sp.GetRequiredService<IDocumentRepository>(),
                docsRootPath));
        services.AddSingleton<DocumentLinkService>();

        return services;
    }
}
```

---

## File Structure

After completion, the following files should exist:

```
src/CompoundDocs.Common/
├── Markdown/
│   ├── Models/
│   │   ├── LinkTargetType.cs
│   │   ├── LinkPurpose.cs
│   │   ├── ExtractedLink.cs
│   │   └── ResolvedLink.cs
│   ├── Abstractions/
│   │   ├── ILinkExtractor.cs (enhanced)
│   │   └── ILinkResolver.cs
│   ├── EnhancedLinkExtractor.cs
│   ├── LinkResolver.cs
│   └── LinkExtractionServiceCollectionExtensions.cs
├── Services/
│   └── DocumentLinkService.cs

tests/CompoundDocs.Tests/
└── Markdown/
    ├── LinkExtractor/
    │   ├── InlineLinkExtractionTests.cs
    │   ├── ReferenceLinkExtractionTests.cs
    │   ├── LinkClassificationTests.cs
    │   └── LinkPurposeDetectionTests.cs
    ├── LinkResolver/
    │   ├── PathResolutionTests.cs
    │   ├── AnchorResolutionTests.cs
    │   └── SecurityBoundaryTests.cs
    └── Integration/
        └── DocumentLinkServiceTests.cs
```

---

## Dependencies

### Depends On
- **Phase 015**: Markdown Parser Integration (Markdig pipeline, base interfaces)
- **Phase 001**: Solution & Project Structure (solution and project files)

### Blocks
- **Phase 016**: QuikGraph Integration (consumes extracted links)
- RAG query tools (uses resolved links for context expansion)
- Document indexing pipeline (extracts and stores link metadata)

---

## Verification Steps

After completing this phase, verify:

1. **All link formats parsed**: Test inline, reference, autolinks, and bare URLs
2. **Classification accuracy**: Internal vs external links correctly identified
3. **Path resolution**: Relative paths correctly resolve to absolute paths
4. **Security boundaries**: Links cannot escape the docs root directory
5. **Graph integration**: Extracted links correctly populate the document graph
6. **Performance**: 1000-link document parses in under 100ms

---

## Unit Test Scenarios

```csharp
public class LinkExtractionTests
{
    [Fact]
    public void Extract_InlineLink_ReturnsLinkInfo()
    {
        var content = "Check [this document](./other.md) for details.";
        var extractor = new EnhancedLinkExtractor();

        var links = extractor.Extract(content);

        Assert.Single(links);
        Assert.Equal("./other.md", links[0].RawUrl);
        Assert.Equal("this document", links[0].Text);
        Assert.Equal(LinkTargetType.InternalDocument, links[0].TargetType);
    }

    [Fact]
    public void Extract_LinkWithTitle_ExtractsTitle()
    {
        var content = "[link](./doc.md \"Tooltip text\")";
        var extractor = new EnhancedLinkExtractor();

        var links = extractor.Extract(content);

        Assert.Single(links);
        Assert.Equal("Tooltip text", links[0].Title);
    }

    [Fact]
    public void Extract_ExternalLink_ClassifiesCorrectly()
    {
        var content = "Visit [example](https://example.com) for more.";
        var extractor = new EnhancedLinkExtractor();

        var links = extractor.Extract(content);

        Assert.Single(links);
        Assert.Equal(LinkTargetType.ExternalHttp, links[0].TargetType);
    }

    [Fact]
    public void Extract_AnchorOnlyLink_ClassifiesAsInternalAnchor()
    {
        var content = "See [section](#my-section) below.";
        var extractor = new EnhancedLinkExtractor();

        var links = extractor.Extract(content);

        Assert.Single(links);
        Assert.Equal(LinkTargetType.InternalAnchor, links[0].TargetType);
        Assert.Equal("my-section", links[0].Anchor);
    }

    [Fact]
    public void Extract_LinkWithAnchor_ExtractsBothPathAndAnchor()
    {
        var content = "See [docs](./other.md#section) for details.";
        var extractor = new EnhancedLinkExtractor();

        var links = extractor.Extract(content);

        Assert.Single(links);
        Assert.Equal(LinkTargetType.InternalDocumentWithAnchor, links[0].TargetType);
        Assert.Equal("./other.md", links[0].PathWithoutAnchor);
        Assert.Equal("section", links[0].Anchor);
    }

    [Fact]
    public void Extract_ImageLink_IsFiltered()
    {
        var content = "![image](./image.png) and [doc](./doc.md)";
        var extractor = new EnhancedLinkExtractor();

        var links = extractor.Extract(content);

        Assert.Single(links);
        Assert.Equal("./doc.md", links[0].RawUrl);
    }

    [Fact]
    public void Extract_MailtoLink_ClassifiesAsEmail()
    {
        var content = "Contact [support](mailto:support@example.com).";
        var extractor = new EnhancedLinkExtractor();

        var links = extractor.Extract(content);

        Assert.Single(links);
        Assert.Equal(LinkTargetType.ExternalEmail, links[0].TargetType);
    }

    [Fact]
    public void ExtractInternalLinks_FiltersExternalLinks()
    {
        var content = """
            [internal](./doc.md)
            [external](https://example.com)
            [anchor](#section)
            """;
        var extractor = new EnhancedLinkExtractor();

        var links = extractor.ExtractInternalLinks(content);

        Assert.Equal(2, links.Count);
        Assert.All(links, l => Assert.True(
            l.TargetType is LinkTargetType.InternalDocument or
                           LinkTargetType.InternalAnchor));
    }

    [Theory]
    [InlineData("./doc.md", "problems/issue.md", "problems/doc.md")]
    [InlineData("../other/doc.md", "problems/issue.md", "other/doc.md")]
    [InlineData("doc.md", "problems/sub/issue.md", "problems/sub/doc.md")]
    public void Resolve_RelativePath_ResolvesCorrectly(
        string linkPath, string sourcePath, string expectedPath)
    {
        var resolver = CreateResolver();
        var link = new ExtractedLink
        {
            RawUrl = linkPath,
            Text = "link",
            TargetType = LinkTargetType.InternalDocument,
            Line = 0,
            PathWithoutAnchor = linkPath
        };

        var resolved = resolver.Resolve(link, sourcePath);

        Assert.Equal(expectedPath, resolved.ResolvedPath);
    }
}
```

---

## Notes

- The `EnhancedLinkExtractor` builds on the basic `ILinkExtractor` interface from Phase 015
- Link purpose detection uses heuristics and can be extended with ML in the future
- Security is critical: links must not escape the docs root directory
- The surrounding context extraction is simple (line-based) but can be enhanced for better RAG relevance
- Performance is important as documents may contain hundreds of links
- The `DocumentLinkService` coordinates extraction, resolution, and graph updates
