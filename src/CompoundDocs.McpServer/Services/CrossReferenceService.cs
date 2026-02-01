using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using CompoundDocs.Common.Configuration;
using CompoundDocs.Common.Graph;
using Microsoft.Extensions.Logging;

namespace CompoundDocs.McpServer.Services;

/// <summary>
/// Service for resolving document cross-references.
/// Supports [[wiki-style]] and [markdown](links) references.
/// </summary>
public interface ICrossReferenceService
{
    /// <summary>
    /// Extracts all references from document content.
    /// </summary>
    /// <param name="content">The document content to parse.</param>
    /// <returns>A collection of extracted references.</returns>
    IReadOnlyList<DocumentReference> ExtractReferences(string content);

    /// <summary>
    /// Resolves a reference to an actual document path.
    /// </summary>
    /// <param name="reference">The reference to resolve.</param>
    /// <param name="sourceDocumentPath">The path of the document containing the reference.</param>
    /// <param name="tenantKey">The tenant key for scoping resolution.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The resolved document information, or null if not found.</returns>
    Task<ResolvedReference?> ResolveAsync(
        DocumentReference reference,
        string sourceDocumentPath,
        string tenantKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves all references in a document.
    /// </summary>
    /// <param name="content">The document content.</param>
    /// <param name="sourceDocumentPath">The path of the document.</param>
    /// <param name="tenantKey">The tenant key for scoping resolution.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of resolved references.</returns>
    Task<IReadOnlyList<ResolvedReference>> ResolveAllAsync(
        string content,
        string sourceDocumentPath,
        string tenantKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Builds or updates the bidirectional link graph for a document.
    /// </summary>
    /// <param name="documentPath">The document path.</param>
    /// <param name="resolvedReferences">The resolved references from the document.</param>
    void UpdateLinkGraph(string documentPath, IReadOnlyList<ResolvedReference> resolvedReferences);

    /// <summary>
    /// Gets all documents that reference the specified document.
    /// </summary>
    /// <param name="documentPath">The document path.</param>
    /// <returns>A collection of documents that reference this document (backlinks).</returns>
    IReadOnlyList<string> GetBacklinks(string documentPath);

    /// <summary>
    /// Gets all documents referenced by the specified document.
    /// </summary>
    /// <param name="documentPath">The document path.</param>
    /// <returns>A collection of documents referenced by this document (forward links).</returns>
    IReadOnlyList<string> GetForwardLinks(string documentPath);

    /// <summary>
    /// Gets broken links from a document (references that couldn't be resolved).
    /// </summary>
    /// <param name="documentPath">The document path.</param>
    /// <returns>A collection of broken link references.</returns>
    IReadOnlyList<DocumentReference> GetBrokenLinks(string documentPath);

    /// <summary>
    /// Clears the resolution cache for a specific document or all documents.
    /// </summary>
    /// <param name="documentPath">The document path, or null to clear all cache.</param>
    void ClearCache(string? documentPath = null);

    /// <summary>
    /// Configures the link resolution settings.
    /// </summary>
    /// <param name="settings">The link resolution settings.</param>
    void Configure(LinkResolutionSettings settings);
}

/// <summary>
/// Represents a reference found in document content.
/// </summary>
public sealed class DocumentReference
{
    /// <summary>
    /// The type of reference.
    /// </summary>
    public required ReferenceType Type { get; init; }

    /// <summary>
    /// The raw reference text as found in the document.
    /// </summary>
    public required string RawText { get; init; }

    /// <summary>
    /// The target of the reference (document ID, file path, or URL).
    /// </summary>
    public required string Target { get; init; }

    /// <summary>
    /// The display text for the reference (for markdown links).
    /// </summary>
    public string? DisplayText { get; init; }

    /// <summary>
    /// Optional anchor/section within the target document.
    /// </summary>
    public string? Anchor { get; init; }

    /// <summary>
    /// The line number where this reference was found.
    /// </summary>
    public int LineNumber { get; init; }

    /// <summary>
    /// The column position where this reference starts.
    /// </summary>
    public int ColumnStart { get; init; }
}

/// <summary>
/// The type of reference.
/// </summary>
public enum ReferenceType
{
    /// <summary>Wiki-style link: [[Document Name]]</summary>
    WikiLink,

    /// <summary>Markdown link: [text](path)</summary>
    MarkdownLink,

    /// <summary>Document ID reference: doc:document-id</summary>
    DocumentId,

    /// <summary>File path reference: file:./path/to/doc.md</summary>
    FilePath,

    /// <summary>External URL (not resolvable internally)</summary>
    ExternalUrl
}

/// <summary>
/// Represents a resolved reference.
/// </summary>
public sealed class ResolvedReference
{
    /// <summary>
    /// The original reference.
    /// </summary>
    public required DocumentReference Reference { get; init; }

    /// <summary>
    /// Whether the reference was successfully resolved.
    /// </summary>
    public bool IsResolved { get; init; }

    /// <summary>
    /// The resolved document path (relative to repository root).
    /// </summary>
    public string? ResolvedPath { get; init; }

    /// <summary>
    /// The document ID if the target is a known document.
    /// </summary>
    public string? DocumentId { get; init; }

    /// <summary>
    /// The title of the resolved document.
    /// </summary>
    public string? DocumentTitle { get; init; }

    /// <summary>
    /// Error message if resolution failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Creates a successfully resolved reference.
    /// </summary>
    public static ResolvedReference Resolved(
        DocumentReference reference,
        string resolvedPath,
        string? documentId = null,
        string? documentTitle = null)
    {
        return new ResolvedReference
        {
            Reference = reference,
            IsResolved = true,
            ResolvedPath = resolvedPath,
            DocumentId = documentId,
            DocumentTitle = documentTitle
        };
    }

    /// <summary>
    /// Creates an unresolved reference.
    /// </summary>
    public static ResolvedReference NotResolved(DocumentReference reference, string error)
    {
        return new ResolvedReference
        {
            Reference = reference,
            IsResolved = false,
            Error = error
        };
    }

    /// <summary>
    /// Creates a reference marked as external (not resolvable).
    /// </summary>
    public static ResolvedReference External(DocumentReference reference)
    {
        return new ResolvedReference
        {
            Reference = reference,
            IsResolved = false,
            Error = "External URL - not resolvable internally"
        };
    }
}

/// <summary>
/// Implementation of ICrossReferenceService.
/// </summary>
public sealed partial class CrossReferenceService : ICrossReferenceService
{
    private readonly DocumentLinkGraph _linkGraph;
    private readonly ILogger<CrossReferenceService> _logger;

    /// <summary>
    /// Cache of resolved references per document path.
    /// </summary>
    private readonly ConcurrentDictionary<string, CachedResolution> _resolutionCache = new();

    /// <summary>
    /// Cache of broken links per document path.
    /// </summary>
    private readonly ConcurrentDictionary<string, List<DocumentReference>> _brokenLinksCache = new();

    /// <summary>
    /// Link resolution settings.
    /// </summary>
    private LinkResolutionSettings _settings = new();

    /// <summary>
    /// Lock for thread-safe operations.
    /// </summary>
    private readonly SemaphoreSlim _lock = new(1, 1);

    // Regex patterns for reference extraction
    [GeneratedRegex(@"\[\[([^\]]+)\]\]", RegexOptions.Compiled)]
    private static partial Regex WikiLinkPattern();

    [GeneratedRegex(@"\[([^\]]*)\]\(([^)]+)\)", RegexOptions.Compiled)]
    private static partial Regex MarkdownLinkPattern();

    [GeneratedRegex(@"doc:([a-zA-Z0-9_-]+)", RegexOptions.Compiled)]
    private static partial Regex DocumentIdPattern();

    [GeneratedRegex(@"file:([^\s]+)", RegexOptions.Compiled)]
    private static partial Regex FilePathPattern();

    [GeneratedRegex(@"^https?://", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex ExternalUrlPattern();

    /// <summary>
    /// Creates a new instance of CrossReferenceService.
    /// </summary>
    /// <param name="linkGraph">The document link graph.</param>
    /// <param name="logger">Logger instance.</param>
    public CrossReferenceService(
        DocumentLinkGraph linkGraph,
        ILogger<CrossReferenceService> logger)
    {
        _linkGraph = linkGraph ?? throw new ArgumentNullException(nameof(linkGraph));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public IReadOnlyList<DocumentReference> ExtractReferences(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return [];

        var references = new List<DocumentReference>();
        var lines = content.Split('\n');

        for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var line = lines[lineIndex];
            var lineNumber = lineIndex + 1;

            // Extract wiki-style links: [[Document Name]]
            foreach (Match match in WikiLinkPattern().Matches(line))
            {
                var (target, anchor) = ParseTargetAndAnchor(match.Groups[1].Value);
                references.Add(new DocumentReference
                {
                    Type = ReferenceType.WikiLink,
                    RawText = match.Value,
                    Target = target,
                    DisplayText = target,
                    Anchor = anchor,
                    LineNumber = lineNumber,
                    ColumnStart = match.Index + 1
                });
            }

            // Extract markdown links: [text](path)
            foreach (Match match in MarkdownLinkPattern().Matches(line))
            {
                var displayText = match.Groups[1].Value;
                var target = match.Groups[2].Value;
                var (parsedTarget, anchor) = ParseTargetAndAnchor(target);

                // Determine reference type
                var refType = DetermineReferenceType(parsedTarget);

                references.Add(new DocumentReference
                {
                    Type = refType,
                    RawText = match.Value,
                    Target = parsedTarget,
                    DisplayText = displayText,
                    Anchor = anchor,
                    LineNumber = lineNumber,
                    ColumnStart = match.Index + 1
                });
            }

            // Extract document ID references: doc:document-id
            foreach (Match match in DocumentIdPattern().Matches(line))
            {
                references.Add(new DocumentReference
                {
                    Type = ReferenceType.DocumentId,
                    RawText = match.Value,
                    Target = match.Groups[1].Value,
                    LineNumber = lineNumber,
                    ColumnStart = match.Index + 1
                });
            }

            // Extract file path references: file:./path/to/doc.md
            foreach (Match match in FilePathPattern().Matches(line))
            {
                references.Add(new DocumentReference
                {
                    Type = ReferenceType.FilePath,
                    RawText = match.Value,
                    Target = match.Groups[1].Value,
                    LineNumber = lineNumber,
                    ColumnStart = match.Index + 1
                });
            }
        }

        return references;
    }

    /// <inheritdoc />
    public async Task<ResolvedReference?> ResolveAsync(
        DocumentReference reference,
        string sourceDocumentPath,
        string tenantKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);

        // External URLs are not resolvable internally
        if (reference.Type == ReferenceType.ExternalUrl)
        {
            return ResolvedReference.External(reference);
        }

        try
        {
            // Resolve based on reference type
            return reference.Type switch
            {
                ReferenceType.WikiLink => await ResolveWikiLinkAsync(reference, sourceDocumentPath, tenantKey, cancellationToken),
                ReferenceType.MarkdownLink => await ResolveMarkdownLinkAsync(reference, sourceDocumentPath, tenantKey, cancellationToken),
                ReferenceType.DocumentId => await ResolveDocumentIdAsync(reference, tenantKey, cancellationToken),
                ReferenceType.FilePath => await ResolveFilePathAsync(reference, sourceDocumentPath, cancellationToken),
                _ => ResolvedReference.NotResolved(reference, $"Unknown reference type: {reference.Type}")
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve reference '{Target}' from {Source}",
                reference.Target, sourceDocumentPath);
            return ResolvedReference.NotResolved(reference, $"Resolution error: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ResolvedReference>> ResolveAllAsync(
        string content,
        string sourceDocumentPath,
        string tenantKey,
        CancellationToken cancellationToken = default)
    {
        var references = ExtractReferences(content);
        if (references.Count == 0)
            return [];

        // Check cache
        var cacheKey = $"{tenantKey}:{sourceDocumentPath}";
        if (_resolutionCache.TryGetValue(cacheKey, out var cached) &&
            cached.ContentHash == ComputeContentHash(content))
        {
            _logger.LogDebug("Using cached resolution for {Path}", sourceDocumentPath);
            return cached.ResolvedReferences;
        }

        var results = new List<ResolvedReference>(references.Count);
        var brokenLinks = new List<DocumentReference>();

        foreach (var reference in references)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var resolved = await ResolveAsync(reference, sourceDocumentPath, tenantKey, cancellationToken);
            if (resolved != null)
            {
                results.Add(resolved);
                if (!resolved.IsResolved && resolved.Reference.Type != ReferenceType.ExternalUrl)
                {
                    brokenLinks.Add(reference);
                }
            }
        }

        // Cache the results
        _resolutionCache[cacheKey] = new CachedResolution
        {
            ContentHash = ComputeContentHash(content),
            ResolvedReferences = results,
            Timestamp = DateTimeOffset.UtcNow
        };
        _brokenLinksCache[sourceDocumentPath] = brokenLinks;

        _logger.LogDebug("Resolved {Total} references from {Path}: {Resolved} resolved, {Broken} broken",
            references.Count, sourceDocumentPath, results.Count(r => r.IsResolved), brokenLinks.Count);

        return results;
    }

    /// <inheritdoc />
    public void UpdateLinkGraph(string documentPath, IReadOnlyList<ResolvedReference> resolvedReferences)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentPath);

        // Clear existing links from this document
        _linkGraph.ClearLinksFrom(documentPath);

        // Ensure the document is in the graph
        _linkGraph.AddDocument(documentPath);

        // Add all resolved links
        foreach (var resolved in resolvedReferences.Where(r => r.IsResolved && !string.IsNullOrEmpty(r.ResolvedPath)))
        {
            _linkGraph.AddDocument(resolved.ResolvedPath!);
            _linkGraph.AddLink(documentPath, resolved.ResolvedPath!);
        }

        _logger.LogDebug("Updated link graph for {Path}: {LinkCount} links",
            documentPath, resolvedReferences.Count(r => r.IsResolved));
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetBacklinks(string documentPath)
    {
        return _linkGraph.GetIncomingLinks(documentPath);
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetForwardLinks(string documentPath)
    {
        return _linkGraph.GetLinkedDocuments(documentPath);
    }

    /// <inheritdoc />
    public IReadOnlyList<DocumentReference> GetBrokenLinks(string documentPath)
    {
        return _brokenLinksCache.TryGetValue(documentPath, out var broken)
            ? broken
            : [];
    }

    /// <inheritdoc />
    public void ClearCache(string? documentPath = null)
    {
        if (documentPath != null)
        {
            // Clear cache for specific document
            var keysToRemove = _resolutionCache.Keys
                .Where(k => k.EndsWith($":{documentPath}"))
                .ToList();

            foreach (var key in keysToRemove)
            {
                _resolutionCache.TryRemove(key, out _);
            }

            _brokenLinksCache.TryRemove(documentPath, out _);
            _logger.LogDebug("Cleared cache for document: {Path}", documentPath);
        }
        else
        {
            // Clear all caches
            _resolutionCache.Clear();
            _brokenLinksCache.Clear();
            _logger.LogDebug("Cleared all resolution caches");
        }
    }

    /// <inheritdoc />
    public void Configure(LinkResolutionSettings settings)
    {
        _settings = settings ?? new LinkResolutionSettings();
        _logger.LogDebug("Configured link resolution: MaxDepth={MaxDepth}, MaxLinkedDocs={MaxLinkedDocs}",
            _settings.MaxDepth, _settings.MaxLinkedDocs);
    }

    #region Private Resolution Methods

    private async Task<ResolvedReference> ResolveWikiLinkAsync(
        DocumentReference reference,
        string sourceDocumentPath,
        string tenantKey,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask; // Placeholder for async document lookup

        // Wiki links use the target as a document name/title
        // Try to find a document with matching title or filename
        var targetName = reference.Target.ToLowerInvariant();

        // Construct potential file paths
        var sourceDir = Path.GetDirectoryName(sourceDocumentPath) ?? ".";
        var potentialPaths = new[]
        {
            Path.Combine(sourceDir, $"{reference.Target}.md"),
            Path.Combine(sourceDir, reference.Target, "index.md"),
            Path.Combine(sourceDir, reference.Target, "README.md"),
            $"{reference.Target}.md",
            Path.Combine(reference.Target, "index.md")
        };

        foreach (var path in potentialPaths)
        {
            var normalizedPath = NormalizePath(path);
            if (File.Exists(normalizedPath))
            {
                return ResolvedReference.Resolved(reference, normalizedPath);
            }
        }

        return ResolvedReference.NotResolved(reference, $"Could not find document: {reference.Target}");
    }

    private async Task<ResolvedReference> ResolveMarkdownLinkAsync(
        DocumentReference reference,
        string sourceDocumentPath,
        string tenantKey,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask; // Placeholder for async document lookup

        // Check if it's an external URL
        if (ExternalUrlPattern().IsMatch(reference.Target))
        {
            return ResolvedReference.External(reference);
        }

        // Resolve relative to source document
        var sourceDir = Path.GetDirectoryName(sourceDocumentPath) ?? ".";
        var resolvedPath = Path.Combine(sourceDir, reference.Target);
        var normalizedPath = NormalizePath(resolvedPath);

        if (File.Exists(normalizedPath))
        {
            return ResolvedReference.Resolved(reference, normalizedPath);
        }

        // Try with .md extension if not present
        if (!reference.Target.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
        {
            var pathWithExtension = normalizedPath + ".md";
            if (File.Exists(pathWithExtension))
            {
                return ResolvedReference.Resolved(reference, pathWithExtension);
            }
        }

        return ResolvedReference.NotResolved(reference, $"File not found: {reference.Target}");
    }

    private async Task<ResolvedReference> ResolveDocumentIdAsync(
        DocumentReference reference,
        string tenantKey,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask; // Placeholder for async document lookup from repository

        // Document IDs would typically be resolved through the document repository
        // For now, return as unresolved - integration with IDocumentRepository would be needed
        return ResolvedReference.NotResolved(reference,
            "Document ID resolution requires repository access - not yet implemented");
    }

    private async Task<ResolvedReference> ResolveFilePathAsync(
        DocumentReference reference,
        string sourceDocumentPath,
        CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        var path = reference.Target;

        // Handle relative paths
        if (path.StartsWith("./") || path.StartsWith("../"))
        {
            var sourceDir = Path.GetDirectoryName(sourceDocumentPath) ?? ".";
            path = Path.Combine(sourceDir, path);
        }

        var normalizedPath = NormalizePath(path);

        if (File.Exists(normalizedPath))
        {
            return ResolvedReference.Resolved(reference, normalizedPath);
        }

        return ResolvedReference.NotResolved(reference, $"File not found: {reference.Target}");
    }

    #endregion

    #region Helper Methods

    private static (string Target, string? Anchor) ParseTargetAndAnchor(string input)
    {
        var anchorIndex = input.IndexOf('#');
        if (anchorIndex > 0)
        {
            return (input[..anchorIndex], input[(anchorIndex + 1)..]);
        }
        return (input, null);
    }

    private static ReferenceType DetermineReferenceType(string target)
    {
        if (ExternalUrlPattern().IsMatch(target))
            return ReferenceType.ExternalUrl;

        if (target.StartsWith("doc:", StringComparison.OrdinalIgnoreCase))
            return ReferenceType.DocumentId;

        if (target.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
            return ReferenceType.FilePath;

        return ReferenceType.MarkdownLink;
    }

    private static string NormalizePath(string path)
    {
        return Path.GetFullPath(path).Replace('\\', '/');
    }

    private static string ComputeContentHash(string content)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    #endregion

    /// <summary>
    /// Cached resolution result.
    /// </summary>
    private sealed class CachedResolution
    {
        public required string ContentHash { get; init; }
        public required IReadOnlyList<ResolvedReference> ResolvedReferences { get; init; }
        public required DateTimeOffset Timestamp { get; init; }
    }
}
